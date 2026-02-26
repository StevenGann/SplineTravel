using System.Text.Json;
using SplineTravel.Core.GCode;
using SplineTravel.Core.Precision;
using SplineTravel.Core.Processing;

namespace SplineTravel.Cli;

/// <summary>
/// Console entry point for SplineTravel:
/// parses arguments, loads configuration, runs the processing pipeline,
/// and supports PrusaSlicer post-processing (in-place) mode.
/// </summary>
static class Program
{
    private static bool _verbose;
    private static StreamWriter? _logFile;

    /// <summary>
    /// Entry point. The last argument is interpreted as the input G-code path
    /// (matching PrusaSlicer post-processing behavior).
    /// </summary>
    /// <param name="args">
    /// <c>&lt;input.gcode&gt; [--output &lt;file&gt;] [--config &lt;file&gt;] [--mode spline|straight] [--verbose]</c>.
    /// </param>
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: splinetravel <input.gcode> [--output <file>] [--config <file>] [--mode spline|straight] [--verbose]");
            Console.Error.WriteLine("  For PrusaSlicer: pass only the G-code file path (modifies in place).");
            Console.Error.WriteLine("  Use --verbose for detailed logs to stderr (helps debug post-processing failures).");
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
            if (args[i] == "--verbose" || args[i] == "-v")
            { _verbose = true; continue; }
        }

        LogVerbose($"Input: {inputPath}");
        LogVerbose($"Output: {(outputPath ?? "(in-place)")}");
        LogVerbose($"Config: {(configPath ?? "auto (splinetravel.json next to EXE)")}");

        var configPathResolved = configPath ?? GetDefaultConfigPath();
        var options = LoadOrCreateOptions(configPathResolved);

        _verbose = _verbose || options.Verbose;
        OpenLogFile(options);
        try
        {
            if (string.Equals(mode, "straight", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSplineTravel = false;
                options.UseStraightTravel = true;
                LogVerbose("Mode: straight travel");
            }
            else if (string.Equals(mode, "spline", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSplineTravel = true;
                options.UseStraightTravel = false;
                LogVerbose("Mode: spline travel");
            }
            else
            {
                LogVerbose($"Mode: spline={options.UseSplineTravel}, straight={options.UseStraightTravel}");
            }

            var precision = new PrecisionSettings();

            if (outputPath == null)
                outputPath = inputPath;

            LogVerbose("Opening input file...");
            using var input = new StreamReader(inputPath);
            string content = input.ReadToEnd();
            LogVerbose($"Read {content.Length:N0} chars from input.");

            LogVerbose("Processing G-code...");
            using var inputReader = new StringReader(content);
            using var outputWriter = new StringWriter();
            SplineTravelProcessor.Process(inputReader, outputWriter, options, precision);
            string result = outputWriter.ToString();
            LogVerbose($"Generated {result.Length:N0} chars output.");

            LogVerbose($"Writing to {outputPath}...");
            File.WriteAllText(outputPath, result);
            LogVerbose("Done.");
            return 0;
        }
        catch (Exception ex)
        {
            LogError(ex);
            return 1;
        }
        finally
        {
            _logFile?.Dispose();
            _logFile = null;
        }
    }

    /// <summary>
    /// Opens the log file when Verbose is true.
    /// </summary>
    static void OpenLogFile(ProcessingOptions options)
    {
        if (!_verbose) return;
        var path = string.IsNullOrWhiteSpace(options.LogFile)
            ? Path.Combine(Path.GetDirectoryName(Environment.ProcessPath ?? AppContext.BaseDirectory) ?? ".", "splinetravel.log")
            : options.LogFile!;
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            _logFile = new StreamWriter(path, append: true) { AutoFlush = true };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[splinetravel] Warning: could not open log file {path}: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes a message to stderr and log file when <see cref="_verbose"/> is true.
    /// </summary>
    static void LogVerbose(string message)
    {
        if (!_verbose) return;
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [splinetravel] {message}";
        Console.Error.WriteLine(line);
        try { _logFile?.WriteLine(line); } catch { /* ignore */ }
    }

    /// <summary>
    /// Writes full exception details to stderr and log file.
    /// </summary>
    static void LogError(Exception ex)
    {
        Console.Error.WriteLine($"[splinetravel] ERROR: {ex.Message}");
        if (!string.IsNullOrEmpty(ex.StackTrace))
            Console.Error.WriteLine($"[splinetravel] Stack trace:\n{ex.StackTrace}");
        if (ex.InnerException != null)
        {
            Console.Error.WriteLine($"[splinetravel] Inner: {ex.InnerException.Message}");
            if (!string.IsNullOrEmpty(ex.InnerException.StackTrace))
                Console.Error.WriteLine($"[splinetravel] Inner stack trace:\n{ex.InnerException.StackTrace}");
        }
        try
        {
            if (_logFile != null)
            {
                _logFile.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {ex.Message}");
                if (!string.IsNullOrEmpty(ex.StackTrace))
                    _logFile.WriteLine(ex.StackTrace);
            }
        }
        catch { /* ignore */ }
    }

    /// <summary>
    /// Returns the default config path: splinetravel.json next to the executable.
    /// </summary>
    static string GetDefaultConfigPath()
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath ?? AppContext.BaseDirectory);
        return Path.Combine(exeDir ?? ".", "splinetravel.json");
    }

    /// <summary>
    /// Loads <see cref="ProcessingOptions"/> from a JSON file if it exists.
    /// If the file does not exist, creates a default config file and returns defaults.
    /// </summary>
    /// <param name="path">Path to a JSON config file.</param>
    static ProcessingOptions LoadOrCreateOptions(string path)
    {
        var options = new ProcessingOptions();
        if (!File.Exists(path))
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(options, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                Console.Error.WriteLine($"[splinetravel] Created default config: {path}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[splinetravel] Warning: could not create config at {path}: {ex.Message}");
            }
            return options;
        }
        try
        {
            var json = File.ReadAllText(path);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("Verbose", out var v)) options.Verbose = v.GetBoolean();
            if (root.TryGetProperty("LogFile", out var s) && s.ValueKind == JsonValueKind.String)
            {
                var val = s.GetString();
                options.LogFile = string.IsNullOrWhiteSpace(val) ? null : val;
            }
            if (root.TryGetProperty("UseSplineTravel", out v)) options.UseSplineTravel = v.GetBoolean();
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
