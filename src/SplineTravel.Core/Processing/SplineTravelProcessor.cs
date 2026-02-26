using SplineTravel.Core.GCode;
using SplineTravel.Core.Geometry;
using SplineTravel.Core.Precision;

namespace SplineTravel.Core.Processing;

/// <summary>
/// Main pipeline: read G-code, group it into build / travel / other segments,
/// optionally apply seam concealment, replace travel moves, and write the result.
/// Ported from the original VB6 <c>mdlWorker.Process</c>.
/// </summary>
public static class SplineTravelProcessor
{
    private const double Eps = 1e-100;

    /// <summary>
    /// Processes a G-code stream according to the supplied options and precision
    /// and writes the transformed program to the output stream.
    /// </summary>
    /// <param name="input">Source G-code text.</param>
    /// <param name="output">Destination for processed G-code text.</param>
    /// <param name="options">Spline/straight travel and seam concealment settings.</param>
    /// <param name="precision">Decimal and tolerance settings for numeric output.</param>
    public static void Process(TextReader input, TextWriter output, ProcessingOptions options, PrecisionSettings precision)
    {
        var chain = GCodeParser.Parse(input);
        var groups = BuildGroups(chain);

        if (options.SeamConcealment)
            ApplySeamConcealment(groups, options, precision);

        bool spline = options.UseSplineTravel;
        bool straight = options.UseStraightTravel;
        if (!spline) spline = !straight;

        for (int g = 0; g < groups.Count; g++)
        {
            var group = groups[g];
            if (group.Type != MoveGroupType.Travel) continue;

            GCodeCommand? prevBuildEnd = g > 0 && groups[g - 1].Type == MoveGroupType.Build
                ? groups[g - 1].Commands[^1]
                : null;
            GCodeCommand? nextBuildStart = g < groups.Count - 1 && groups[g + 1].Type == MoveGroupType.Build
                ? groups[g + 1].Commands[0]
                : null;

            if (prevBuildEnd == null || nextBuildStart == null) continue;

            bool retractInjected = g > 0 && groups[g - 1].RetractInjected;
            bool unretractInjected = g < groups.Count - 1 && groups[g + 1].UnretractInjected;

            group.Commands.Clear();

            if (spline)
            {
                var gen = new TravelSplineGenerator
                {
                    P1 = prevBuildEnd.StateAfter.Pos,
                    P2 = nextBuildStart.StateBefore.Pos,
                    InSpeed = prevBuildEnd.GetExitSpeed(),
                    OutSpeed = nextBuildStart.GetEnterSpeed(),
                    Acceleration = options.Acceleration,
                    CurveJerk = options.CurveJerk,
                    SpeedLimit = options.SpeedLimit,
                    Retract = options.RetractLength,
                    RetractAcceleration = options.EAcceleration,
                    RetractJerk = options.EJerk,
                    ZJerk = options.ZJerk,
                    BRetract = !retractInjected,
                    BUnretract = !unretractInjected
                };
                var bz = gen.FitBezier(out double moveTime);
                var moves = gen.GenerateMoveTrainForBezier(bz, moveTime);

                GCodeState state = prevBuildEnd.StateAfter;
                double eError = 0;
                foreach (var move in moves)
                {
                    var cmd = new GCodeCommand { StateBefore = state };
                    cmd.RawLine = move.GenerateGCode(state, precision, ref eError);
                    cmd.ParseLine(cmd.RawLine);
                    cmd.RecomputeStates(state, false, false);
                    state = cmd.StateAfter;
                    group.Commands.Add(cmd);
                }
            }
            else if (straight)
            {
                var state = prevBuildEnd.StateAfter;
                double eError = 0;

                if (options.RetractLength > Eps && !retractInjected)
                {
                    var retractMove = new GeometricMove
                    {
                        P1 = state.Pos, P2 = state.Pos,
                        Extrusion = -options.RetractLength,
                        Time = options.RetractLength / options.RetractSpeedStraight
                    };
                    var cmd = new GCodeCommand { StateBefore = state };
                    cmd.RawLine = retractMove.GenerateGCode(state, precision, ref eError);
                    cmd.ParseLine(cmd.RawLine);
                    cmd.RecomputeStates(state, false, false);
                    state = cmd.StateAfter;
                    group.Commands.Add(cmd);
                }

                if (options.ZHop > Eps)
                {
                    var zUp = new GeometricMove
                    {
                        P1 = state.Pos,
                        P2 = new Vector3(state.Pos.X, state.Pos.Y, state.Pos.Z + options.ZHop),
                        Time = options.ZHop / options.SpeedStraight
                    };
                    var cmd = new GCodeCommand { StateBefore = state };
                    cmd.RawLine = zUp.GenerateGCode(state, precision, ref eError);
                    cmd.ParseLine(cmd.RawLine);
                    cmd.RecomputeStates(state, false, false);
                    state = cmd.StateAfter;
                    group.Commands.Add(cmd);
                }

                var dest = nextBuildStart.StateBefore.Pos;
                var mainMove = new GeometricMove
                {
                    P1 = state.Pos,
                    P2 = new Vector3(dest.X, dest.Y, dest.Z + options.ZHop),
                    Speed = options.SpeedStraight
                };
                var mainCmd = new GCodeCommand { StateBefore = state };
                mainCmd.RawLine = mainMove.GenerateGCode(state, precision, ref eError);
                mainCmd.ParseLine(mainCmd.RawLine);
                mainCmd.RecomputeStates(state, false, false);
                state = mainCmd.StateAfter;
                group.Commands.Add(mainCmd);

                if (options.ZHop > Eps)
                {
                    var zDown = new GeometricMove
                    {
                        P1 = state.Pos,
                        P2 = dest,
                        Speed = options.SpeedStraight
                    };
                    var cmd = new GCodeCommand { StateBefore = state };
                    cmd.RawLine = zDown.GenerateGCode(state, precision, ref eError);
                    cmd.ParseLine(cmd.RawLine);
                    cmd.RecomputeStates(state, false, false);
                    state = cmd.StateAfter;
                    group.Commands.Add(cmd);
                }

                if (options.RetractLength > Eps && !unretractInjected)
                {
                    var unretractMove = new GeometricMove
                    {
                        P1 = state.Pos, P2 = state.Pos,
                        Extrusion = options.RetractLength,
                        Time = options.RetractLength / options.RetractSpeedStraight
                    };
                    var cmd = new GCodeCommand { StateBefore = state };
                    cmd.RawLine = unretractMove.GenerateGCode(state, precision, ref eError);
                    cmd.ParseLine(cmd.RawLine);
                    cmd.RecomputeStates(state, false, false);
                    group.Commands.Add(cmd);
                }
            }
        }

        foreach (var grp in groups)
            foreach (var cmd in grp.Commands)
                output.WriteLine(cmd.RawLine);
    }

    private static List<MoveGroupInfo> BuildGroups(GCodeChain chain)
    {
        var result = new List<MoveGroupInfo>();
        var list = chain.Commands;
        if (list.Count == 0) return result;

        MoveGroupType currentType = GetCmdType(list[0]);
        var current = new MoveGroupInfo { Type = currentType };
        current.Commands.Add(list[0]);

        for (int i = 1; i < list.Count; i++)
        {
            var cmd = list[i];
            var ty = GetCmdType(cmd);
            if (ty != currentType)
            {
                result.Add(current);
                current = new MoveGroupInfo { Type = ty };
                current.Commands.Add(cmd);
                currentType = ty;
            }
            else
                current.Commands.Add(cmd);
        }
        result.Add(current);
        return result;
    }

    private static MoveGroupType GetCmdType(GCodeCommand cmd)
    {
        if (cmd.IsBuildMove) return MoveGroupType.Build;
        if (cmd.IsTravelMove || cmd.IsExtruderMove) return MoveGroupType.Travel;
        return MoveGroupType.Other;
    }

    private static void ApplySeamConcealment(List<MoveGroupInfo> groups, ProcessingOptions options, PrecisionSettings precision)
    {
        double loopTol = options.LoopTolerance;

        for (int g = 0; g < groups.Count; g++)
        {
            if (groups[g].Type != MoveGroupType.Build) continue;
            var commands = groups[g].Commands;
            if (commands.Count == 0) continue;

            var first = commands[0];
            var last = commands[^1];
            var p1 = first.StateBefore.Pos;
            var p2 = last.StateAfter.Pos;
            if (Vector3.Distance(p1, p2) > loopTol) continue;

            // Mark so travel replacement skips retract/unretract for this build group's adjacent travels.
            groups[g].RetractInjected = true;
            groups[g].UnretractInjected = true;
        }
    }
}
