using SplineTravel.Core.Geometry;
using SplineTravel.Core.Precision;

namespace SplineTravel.Core.GCode;

/// <summary>
/// A single geometric move (p1, p2, time, extrusion). Ported from VB6 clsGMove.
/// </summary>
public sealed class GeometricMove
{
    private const double Eps = 1e-100;

    public Vector3 P1 { get; set; }
    public Vector3 P2 { get; set; }
    public double Time { get; set; }
    /// <summary>Filament length; positive = extrude, negative = retract.</summary>
    public double Extrusion { get; set; }

    public double TravelDist => Vector3.Distance(P1, P2);

    public double Speed
    {
        get
        {
            if (Time <= Eps) throw new InvalidOperationException("Zero time move");
            return TravelDist / Time;
        }
        set
        {
            if (TravelDist <= Eps) throw new InvalidOperationException("Zero distance move");
            Time = TravelDist / value;
        }
    }

    public double FeedRate => Time > Eps ? (TravelDist > Eps ? TravelDist / Time : Math.Abs(Extrusion) / Time) : throw new InvalidOperationException("Zero time");
    public double ExtrusionSpeed => Time > Eps ? Math.Abs(Extrusion) / Time : throw new InvalidOperationException("Zero time");

    public bool IsPause => TravelDist < Eps && Math.Abs(Extrusion) < Eps;

    /// <summary>Generate G1 or G4 line. Updates eError for relative E rounding.</summary>
    public string GenerateGCode(GCodeState currentState, PrecisionSettings precision, ref double eError)
    {
        if (Time < Eps) throw new InvalidOperationException("Invalid move: zero time");
        if (IsPause)
            return "G4 P" + PrecisionSettings.Round(Time * 1000, 0);

        var delta = P2 - P1;
        var writePos = currentState.MoveRelative ? delta : P2;
        var writePosRounded = new Vector3(
            PrecisionSettings.Round(writePos.X, precision.PosDecimals),
            PrecisionSettings.Round(writePos.Y, precision.PosDecimals),
            PrecisionSettings.Round(writePos.Z, precision.PosDecimals));
        var deltaFromState = P2 - currentState.Pos;

        var parts = new List<string>();
        if (Math.Abs(deltaFromState.X) > precision.PosConfusion)
            parts.Add("X" + writePosRounded.X.ToString("F" + precision.PosDecimals).TrimEnd('0').TrimEnd('.'));
        if (Math.Abs(deltaFromState.Y) > precision.PosConfusion)
            parts.Add("Y" + writePosRounded.Y.ToString("F" + precision.PosDecimals).TrimEnd('0').TrimEnd('.'));
        if (Math.Abs(deltaFromState.Z) > precision.PosConfusion)
            parts.Add("Z" + writePosRounded.Z.ToString("F" + precision.PosDecimals).TrimEnd('0').TrimEnd('.'));

        if (Time > 0 && precision.SpeedDecimals >= 0 && Math.Abs(FeedRate - currentState.SpeedMmPerSec) > precision.SpeedConfusion)
            parts.Add("F" + PrecisionSettings.Round(FeedRate * 60, precision.SpeedDecimals));

        if (Math.Abs(Extrusion) > precision.ExtrConfusion)
        {
            double wrE;
            if (currentState.ExtrusionRelative)
            {
                wrE = Extrusion + eError;
                eError = wrE - PrecisionSettings.Round(wrE, precision.ExtrDecimals);
                wrE = PrecisionSettings.Round(wrE, precision.ExtrDecimals);
            }
            else
            {
                wrE = PrecisionSettings.Round(currentState.EPos + Extrusion, precision.ExtrDecimals);
            }
            parts.Add("E" + wrE.ToString("F" + precision.ExtrDecimals).TrimEnd('0').TrimEnd('.'));
        }

        return parts.Count > 0 ? "G1 " + string.Join(" ", parts) : "G1";
    }

    /// <summary>Split move at timePoint; returns (part1, part2) or null if no split.</summary>
    public (GeometricMove part1, GeometricMove part2)? Split(double timePoint)
    {
        if (timePoint <= Eps) return null;
        if (timePoint >= Time - Eps) return null;
        var t = timePoint / Time;
        var s = 1 - t;
        var mid = P1 + (P2 - P1) * t;
        return (
            new GeometricMove { P1 = P1, P2 = mid, Time = t * Time, Extrusion = t * Extrusion },
            new GeometricMove { P1 = mid, P2 = P2, Time = s * Time, Extrusion = s * Extrusion }
        );
    }
}
