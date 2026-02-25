using SplineTravel.Core.GCode;

namespace SplineTravel.Core.Processing;

public sealed class MoveGroupInfo
{
    public MoveGroupType Type { get; set; }
    public List<GCodeCommand> Commands { get; } = new();
    public bool RetractInjected { get; set; }
    public bool UnretractInjected { get; set; }
}
