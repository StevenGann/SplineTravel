namespace SplineTravel.Core.GCode;

/// <summary>
/// Writes a G-code chain to a stream.
/// </summary>
public static class GCodeWriter
{
    public static void Write(GCodeChain chain, TextWriter writer)
    {
        foreach (var cmd in chain.Commands)
            writer.WriteLine(cmd.RawLine);
    }

    public static void WriteToFile(GCodeChain chain, string path)
    {
        using var writer = new StreamWriter(path);
        Write(chain, writer);
    }
}
