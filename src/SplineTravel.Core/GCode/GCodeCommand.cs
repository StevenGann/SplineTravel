using SplineTravel.Core.Geometry;
using SplineTravel.Core.Precision;

namespace SplineTravel.Core.GCode;

/// <summary>
/// A single parsed G-code command with state before/after. Ported from VB6 clsGCommand.
/// </summary>
public sealed class GCodeCommand
{
    private const double Eps = 1e-100;

    public string RawLine { get; set; } = "";
    public GCodeState StateBefore { get; set; }
    public GCodeState StateAfter { get; set; }
    public GCodeCommandType CommandType { get; set; }
    public Dictionary<char, double> Arguments { get; } = new();
    public GeometricMove? Move { get; private set; }

    public bool IsEmpty => CommandType == GCodeCommandType.Empty;
    public bool IsMove => CommandType == GCodeCommandType.G0 || CommandType == GCodeCommandType.G1;

    public bool IsBuildMove
    {
        get
        {
            if (!IsMove) return false;
            var dist = Vector3.Distance(StateBefore.Pos, StateAfter.Pos);
            if (dist < Eps) return false;
            if (StateAfter.EPos - StateBefore.EPos < Eps) return false;
            return true;
        }
    }

    public bool IsTravelMove
    {
        get
        {
            if (!IsMove) return false;
            var dist = Vector3.Distance(StateBefore.Pos, StateAfter.Pos);
            if (dist < Eps) return false;
            if (Math.Abs(StateAfter.EPos - StateBefore.EPos) > Eps) return false;
            return true;
        }
    }

    public bool IsExtruderMove
    {
        get
        {
            if (!IsMove) return false;
            var dist = Vector3.Distance(StateBefore.Pos, StateAfter.Pos);
            if (dist > Eps) return false;
            if (Math.Abs(StateAfter.EPos - StateBefore.EPos) < Eps) return false;
            return true;
        }
    }

    public bool IsRetract => IsMove && (StateAfter.EPos - StateBefore.EPos) < -Eps;
    public double EChange => StateAfter.EPos - StateBefore.EPos;
    public double ExecTime => Move?.Time ?? 0;

    /// <summary>Recompute state after from state before and arguments. Optionally preserve delta E.</summary>
    public void RecomputeStates(GCodeState? prevStateAfter, bool preserveDeltaE = false, bool keepStateBefore = false)
    {
        var oldDeltaE = StateAfter.EPos - StateBefore.EPos;
        if (prevStateAfter.HasValue && !keepStateBefore)
            StateBefore = prevStateAfter.Value.Clone();
        var after = StateBefore.Clone();

        if (Arguments.TryGetValue('X', out var x))
        {
            if (StateBefore.MoveRelative) after.Pos = new Vector3(after.Pos.X + x, after.Pos.Y, after.Pos.Z);
            else after.Pos = new Vector3(x, after.Pos.Y, after.Pos.Z);
        }
        if (Arguments.TryGetValue('Y', out var y))
        {
            if (StateBefore.MoveRelative) after.Pos = new Vector3(after.Pos.X, after.Pos.Y + y, after.Pos.Z);
            else after.Pos = new Vector3(after.Pos.X, y, after.Pos.Z);
        }
        if (Arguments.TryGetValue('Z', out var z))
        {
            if (StateBefore.MoveRelative) after.Pos = new Vector3(after.Pos.X, after.Pos.Y, after.Pos.Z + z);
            else after.Pos = new Vector3(after.Pos.X, after.Pos.Y, z);
        }
        if (Arguments.TryGetValue('E', out var e))
        {
            if (StateBefore.ExtrusionRelative)
                after.EPos = StateBefore.EPos + e;
            else
                after.EPos = e;
            if (preserveDeltaE)
            {
                after.EPos = StateBefore.EPos + oldDeltaE;
                Arguments['E'] = after.EPos;
            }
        }
        if (Arguments.TryGetValue('F', out var f))
            after.SpeedMmPerSec = f / 60.0;

        switch (CommandType)
        {
            case GCodeCommandType.G90: after.MoveRelative = false; break;
            case GCodeCommandType.G91: after.MoveRelative = true; break;
            case GCodeCommandType.M82: after.ExtrusionRelative = false; break;
            case GCodeCommandType.M83: after.ExtrusionRelative = true; break;
            case GCodeCommandType.G92:
                if (Arguments.TryGetValue('E', out var e92)) after.EPos = e92;
                if (Arguments.TryGetValue('X', out var x92)) after.Pos = new Vector3(x92, after.Pos.Y, after.Pos.Z);
                if (Arguments.TryGetValue('Y', out var y92)) after.Pos = new Vector3(after.Pos.X, y92, after.Pos.Z);
                if (Arguments.TryGetValue('Z', out var z92)) after.Pos = new Vector3(after.Pos.X, after.Pos.Y, z92);
                break;
        }

        StateAfter = after;
        if (IsMove)
            ConstructMove();
    }

    public void ConstructMove()
    {
        if (!IsMove) { Move = null; return; }
        Move = new GeometricMove
        {
            P1 = StateBefore.Pos,
            P2 = StateAfter.Pos,
            Extrusion = StateAfter.EPos - StateBefore.EPos
        };
        if (Move.TravelDist > Eps)
            Move.Speed = StateAfter.SpeedMmPerSec;
        else if (Math.Abs(Move.Extrusion) > Eps)
            Move.Time = Math.Abs(Move.Extrusion) / StateAfter.SpeedMmPerSec;
        else
            Move = null;
    }

    public Vector3 GetEnterSpeed()
    {
        var delta = StateAfter.Pos - StateBefore.Pos;
        var len = delta.Length;
        if (len <= 1e-100) return new Vector3(0, 0, 0);
        return delta.Normalized() * StateAfter.SpeedMmPerSec;
    }

    public Vector3 GetExitSpeed() => GetEnterSpeed();

    public void SetMove(GeometricMove newMove, PrecisionSettings precision, ref double eError)
    {
        if (!IsMove && !IsEmpty) throw new InvalidOperationException("Not a move command");
        RawLine = newMove.GenerateGCode(StateBefore, precision, ref eError);
        ParseLine(RawLine);
    }

    /// <summary>Parse RawLine into CommandType and Arguments.</summary>
    public void ParseLine(string? line)
    {
        Arguments.Clear();
        CommandType = GCodeCommandType.Empty;
        if (string.IsNullOrWhiteSpace(line)) return;

        var commentIdx = line.IndexOf(';');
        if (commentIdx >= 0) line = line[..commentIdx];
        var starIdx = line.LastIndexOf('*');
        if (starIdx >= 0) line = line[..starIdx];
        line = line.Trim();
        if (line.Length == 0) return;

        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length; i++)
        {
            var w = words[i];
            if (w.Length == 0) continue;
            var letter = char.ToUpperInvariant(w[0]);
            if (letter < 'A' || letter > 'Z') continue;
            var numPart = w.Length > 1 ? w[1..] : "0";
            var val = double.TryParse(numPart, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
            if (i == 0)
                CommandType = (GCodeCommandType)((int)val * 0x100 + letter);
            else
                Arguments[letter] = val;
        }
        if (IsMove) ConstructMove();
    }

    public void RegenerateString()
    {
        if (IsEmpty) return;
        var letter = (char)((int)CommandType & 0xFF);
        var num = ((int)CommandType & 0xFF00) / 0x100;
        RawLine = letter + num.ToString(System.Globalization.CultureInfo.InvariantCulture);
        foreach (var (k, v) in Arguments.OrderBy(x => x.Key))
            RawLine += " " + k + v.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
    }
}
