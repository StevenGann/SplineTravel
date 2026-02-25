using SplineTravel.Core.Geometry;

namespace SplineTravel.Core.GCode;

/// <summary>
/// Machine state after a command. Ported from VB6 typCurrentState.
/// </summary>
public struct GCodeState
{
    public double SpeedMmPerSec { get; set; }
    public Vector3 Pos { get; set; }
    public double EPos { get; set; }
    public bool MoveRelative { get; set; }
    public bool ExtrusionRelative { get; set; }

    public GCodeState Clone()
    {
        return new GCodeState
        {
            SpeedMmPerSec = SpeedMmPerSec,
            Pos = Pos,
            EPos = EPos,
            MoveRelative = MoveRelative,
            ExtrusionRelative = ExtrusionRelative
        };
    }
}
