# SplineTravel

A G-code post-processor for 3D printing that replaces straight-line travel moves with smooth curved (Bezier spline) moves and provides seam concealment.

**This repository is a complete rewrite in C#/.NET 10.** The original SplineTravel was written in Visual Basic 6 by **DeepSOIC**. The algorithms and behavior are adapted from that implementation. Original project: [DeepSOIC/SplineTravel](https://github.com/DeepSOIC/SplineTravel), [Hackaday](https://hackaday.io/project/7045-splinetravel).

## What it does

- **Spline travel:** Replaces straight travel moves with cubic Bezier curves fitted to entry/exit speed and acceleration limits, reducing full stops.
- **Straight travel (optional):** Retract, Z-hop, linear move, Z-hop down, unretract.
- **Seam concealment:** When the start and end of an extrusion loop are close, injects retract/unretract to hide the seam (similar to Slic3r "Wipe while retracting").

## PrusaSlicer integration

Configure in **Print settings - Output options - Post-processing scripts** with the path to `splinetravel` (or `splinetravel.exe` on Windows). PrusaSlicer passes the G-code file path as the last argument; SplineTravel modifies the file in place. See [PrusaSlicer post-processing docs](https://help.prusa3d.com/article/post-processing-scripts_283913).

## Build and run

Requires .NET 10 SDK. If you only have .NET 8, change `TargetFramework` to `net8.0` in `src/SplineTravel.Core/SplineTravel.Core.csproj` and `src/SplineTravel.Cli/SplineTravel.Cli.csproj`.

```bash
dotnet build
dotnet run --project src/SplineTravel.Cli -- input.gcode
```

Single-file executable (e.g. for PrusaSlicer):

```bash
dotnet publish src/SplineTravel.Cli -c Release -r win-x64 --self-contained
```

## Usage

- **In-place (PrusaSlicer):** `splinetravel /path/to/file.gcode`
- **Custom output:** `splinetravel input.gcode --output output.gcode`
- **Config:** Use `--config splinetravel.json` or place `splinetravel.json` next to the executable.

## License

See LICENSE. Original VB6 implementation by DeepSOIC.
