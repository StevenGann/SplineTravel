namespace SplineTravel.Core.GCode;

/// <summary>
/// Ordered list of G-code commands (replaces the VB6 <c>clsChain</c> type).
/// Provides basic navigation and grouping helpers.
/// </summary>
public sealed class GCodeChain
{
    private readonly List<GCodeCommand> _commands = new();

    public IReadOnlyList<GCodeCommand> Commands => _commands;
    public int Count => _commands.Count;
    public GCodeCommand? First => _commands.Count > 0 ? _commands[0] : null;
    public GCodeCommand? Last => _commands.Count > 0 ? _commands[^1] : null;

    public void Add(GCodeCommand cmd) => _commands.Add(cmd);

    public void AddAfter(GCodeCommand after, GCodeCommand cmd)
    {
        var idx = _commands.IndexOf(after);
        if (idx < 0) throw new ArgumentException("Command not in chain");
        _commands.Insert(idx + 1, cmd);
    }

    public GCodeCommand? GetPrev(GCodeCommand cmd)
    {
        var i = _commands.IndexOf(cmd);
        return i > 0 ? _commands[i - 1] : null;
    }

    public GCodeCommand? GetNext(GCodeCommand cmd)
    {
        var i = _commands.IndexOf(cmd);
        return i >= 0 && i < _commands.Count - 1 ? _commands[i + 1] : null;
    }

    public void RemoveRange(int fromIndex, int count)
    {
        _commands.RemoveRange(fromIndex, count);
    }

    public void Clear() => _commands.Clear();

    /// <summary>
    /// Yields contiguous move groups classified as other, build, or travel.
    /// The underlying command list is not modified.
    /// </summary>
    public IEnumerable<MoveGroup> GetMoveGroups()
    {
        if (_commands.Count == 0) yield break;

        MoveGroupType currentType = MoveGroupType.Other;
        int start = 0;
        GCodeCommand? firstMove = null;
        GCodeCommand? lastMove = null;

        for (var i = 0; i < _commands.Count; i++)
        {
            var cmd = _commands[i];
            var cmdType = GetCommandGroupType(cmd);
            if (cmdType == MoveGroupType.Other)
            {
                if (currentType != MoveGroupType.Other)
                {
                    yield return new MoveGroup(currentType, start, i - 1, firstMove!, lastMove!);
                    start = i;
                    firstMove = null;
                    lastMove = null;
                }
                currentType = MoveGroupType.Other;
                continue;
            }
            if (cmdType != currentType)
            {
                if (currentType != MoveGroupType.Other && firstMove != null)
                    yield return new MoveGroup(currentType, start, i - 1, firstMove, lastMove!);
                start = i;
                currentType = cmdType;
                firstMove = cmd;
                lastMove = cmd;
            }
            else
            {
                lastMove = cmd;
            }
        }
        if (firstMove != null && lastMove != null)
            yield return new MoveGroup(currentType, start, _commands.Count - 1, firstMove, lastMove);
    }

    private static MoveGroupType GetCommandGroupType(GCodeCommand cmd)
    {
        if (cmd.IsBuildMove) return MoveGroupType.Build;
        if (cmd.IsTravelMove || cmd.IsExtruderMove) return MoveGroupType.Travel;
        return MoveGroupType.Other;
    }
}

/// <summary>
/// Classification of a contiguous run of commands used for higher-level processing.
/// </summary>
public enum MoveGroupType
{
    /// <summary>Non-move and setup commands (G90/G91, M82/M83, comments, etc.).</summary>
    Other,
    /// <summary>Extruding moves that contribute to the printed model.</summary>
    Build,
    /// <summary>Travel and pure-extruder moves (retract / unretract) between build segments.</summary>
    Travel
}

/// <summary>
/// Summary information about a group of commands in a <see cref="GCodeChain"/>.
/// </summary>
public readonly record struct MoveGroup(
    MoveGroupType Type,
    int StartIndex,
    int EndIndex,
    GCodeCommand FirstMove,
    GCodeCommand LastMove)
{
    /// <summary>Number of commands in this group.</summary>
    public int Length => EndIndex - StartIndex + 1;
}
