namespace SplineTravel.Core.GCode;

/// <summary>
/// Parses G-code file into a chain of commands with propagated state.
/// </summary>
public static class GCodeParser
{
    public static GCodeChain Parse(TextReader reader)
    {
        var chain = new GCodeChain();
        GCodeState state = default;
        state.MoveRelative = false;
        state.ExtrusionRelative = true; // M83 is common default in slicers
        GCodeCommand? prev = null;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var cmd = new GCodeCommand { RawLine = line ?? "" };
            cmd.ParseLine(cmd.RawLine);
            cmd.StateBefore = prev != null ? prev.StateAfter.Clone() : state;
            cmd.RecomputeStates(prev?.StateAfter, preserveDeltaE: false, keepStateBefore: false);
            chain.Add(cmd);
            prev = cmd;
        }
        return chain;
    }

    public static GCodeChain ParseFile(string path)
    {
        using var reader = new StreamReader(path);
        return Parse(reader);
    }
}
