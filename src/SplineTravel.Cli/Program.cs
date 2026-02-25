using System.Text.Json;
using SplineTravel.Core.GCode;
using SplineTravel.Core.Precision;
using SplineTravel.Core.Processing;

namespace SplineTravel.Cli;

static class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: splinetravel <input.gcode> [--output <file>] [--config <file>] [--mode spline|straight]");
            Console.Error.WriteLine("  For PrusaSlicer: pass only the G-code file path (modifies in place).");
            return 1;
        }

        string inputPath = args[^1];
        string? outputPath = null;
        string? configPath = null;
        string? mode = null;

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--output" && i + 1 < args.Length)
            { outputPath = args[++i]; continue; }
            if (args[i] == "--config" && i + 1 < args.Length)
            { configPath = args[++i]; continue; }
            if (args[i] == "--mode" && i + 1 < args.Length)
            { mode = args[++i]; continue; }
        }

        var options = LoadOptions(configPath ?? Path.Combine(Path.GetDirectoryName(inputPath) ?? ".", "splinetravel.json"));
        if (string.Equals(mode, "straight", StringComparison.OrdinalIgnoreCase))
        {
            options.UseSplineTravel = false;
            options.UseStraightTravel = true;
        }
        else if (string.Equals(mode, "spline", StringComparison.OrdinalIgnoreCase))
        {
            options.UseSplineTravel = true;
            options.UseStraightTravel = false;
        }

        var precision = new PrecisionSettings();

        try
        {
            if (outputPath == null)
                outputPath = inputPath;

            using var input = new StreamReader(inputPath);
            string content = input.ReadToEnd();

            using var inputReader = new StringReader(content);
            using var outputWriter = new StringWriter();
            SplineTravelProcessor.Process(inputReader, outputWriter, options, precision);
            string result = outputWriter.ToString();

            File.WriteAllText(outputPath, result);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    static ProcessingOptions LoadOptions(string path)
    {
        var options = new ProcessingOptions();
        if (!File.Exists(path)) return options;
        try
        {
            var json = File.ReadAllText(path);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("UseSplineTravel", out var v)) options.UseSplineTravel = v.GetBoolean();
            if (root.TryGetProperty("UseStraightTravel", out v)) options.UseStraightTravel = v.GetBoolean();
            if (root.TryGetProperty("SeamConcealment", out v)) options.SeamConcealment = v.GetBoolean();
            if (root.TryGetProperty("RetractLength", out var n)) options.RetractLength = n.GetDouble();
            if (root.TryGetProperty("Acceleration", out n)) options.Acceleration = n.GetDouble();
            if (root.TryGetProperty("CurveJerk", out n)) options.CurveJerk = n.GetDouble();
            if (root.TryGetProperty("SpeedLimit", out n)) options.SpeedLimit = n.GetDouble();
            if (root.TryGetProperty("EAcceleration", out n)) options.EAcceleration = n.GetDouble();
            if (root.TryGetProperty("EJerk", out n)) options.EJerk = n.GetDouble();
            if (root.TryGetProperty("ZJerk", out n)) options.ZJerk = n.GetDouble();
            if (root.TryGetProperty("LoopTolerance", out n)) options.LoopTolerance = n.GetDouble();
            if (root.TryGetProperty("SeamConcealRetractSpeed", out n)) options.SeamConcealRetractSpeed = n.GetDouble();
            if (root.TryGetProperty("ZHop", out n)) options.ZHop = n.GetDouble();
            if (root.TryGetProperty("SpeedStraight", out n)) options.SpeedStraight = n.GetDouble();
            if (root.TryGetProperty("RetractSpeedStraight", out n)) options.RetractSpeedStraight = n.GetDouble();
        }
        catch { /* use defaults */ }
        return options;
    }
}
