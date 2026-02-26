using SplineTravel.Core.GCode;
using Xunit;

namespace SplineTravel.Tests;

/// <summary>
/// Tests basic G-code parsing and state propagation behavior for <see cref="GCodeParser"/>.
/// </summary>
public class GCodeParserTests
{
    [Fact]
    public void Parse_simple_g1_parses_position_and_feed()
    {
        var chain = GCodeParser.Parse(new StringReader("G1 X10 Y20 Z0.5 F1200\n"));
        Assert.Single(chain.Commands);
        var cmd = chain.Commands[0];
        Assert.Equal(GCodeCommandType.G1, cmd.CommandType);
        Assert.Equal(10, cmd.StateAfter.Pos.X);
        Assert.Equal(20, cmd.StateAfter.Pos.Y);
        Assert.Equal(0.5, cmd.StateAfter.Pos.Z);
        Assert.Equal(20, cmd.StateAfter.SpeedMmPerSec); // F1200 mm/min = 20 mm/s
    }

    [Fact]
    public void Parse_empty_and_comments_ignored()
    {
        var chain = GCodeParser.Parse(new StringReader("; comment\nG90\n\nG1 X1\n"));
        Assert.Equal(4, chain.Commands.Count); // one command per line including empty
        Assert.Equal(GCodeCommandType.G90, chain.Commands[1].CommandType);
        Assert.Equal(GCodeCommandType.G1, chain.Commands[3].CommandType);
    }

    [Fact]
    public void State_propagates_along_chain()
    {
        var chain = GCodeParser.Parse(new StringReader("G90\nG1 X0 Y0 Z0\nG1 X10 Y0 Z0 F600\n"));
        Assert.Equal(10, chain.Commands[2].StateAfter.Pos.X);
        Assert.Equal(10, chain.Commands[2].StateAfter.SpeedMmPerSec);
    }
}
