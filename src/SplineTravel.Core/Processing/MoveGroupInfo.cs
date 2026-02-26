using SplineTravel.Core.GCode;

namespace SplineTravel.Core.Processing;

/// <summary>
/// Represents a contiguous group of G-code commands classified by move type
/// (other / build / travel) used by <see cref="SplineTravelProcessor"/>.
/// </summary>
public sealed class MoveGroupInfo
{
    /// <summary>
    /// Group classification: other commands, build moves, or travel / extruder moves.
    /// </summary>
    public MoveGroupType Type { get; set; }

    /// <summary>
    /// Commands that belong to this group, in original file order.
    /// </summary>
    public List<GCodeCommand> Commands { get; } = new();

    /// <summary>
    /// True if this build group already has a retract injected at the end
    /// (so adjacent travel groups should not add an extra retract).
    /// </summary>
    public bool RetractInjected { get; set; }

    /// <summary>
    /// True if this build group already has an unretract injected at the start
    /// (so adjacent travel groups should not add an extra unretract).
    /// </summary>
    public bool UnretractInjected { get; set; }
}
