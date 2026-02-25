# SplineTravel C# Implementation

This document describes the C#/.NET rewrite of SplineTravel. The algorithms are adapted from the original VB6 implementation by **DeepSOIC** ([GitHub](https://github.com/DeepSOIC/SplineTravel), [Hackaday](https://hackaday.io/project/7045-splinetravel)).

## Structure

- **src/SplineTravel.Core** — Library: G-code parsing, state propagation, Bezier fitting, retract profile, travel replacement, seam concealment.
- **src/SplineTravel.Cli** — Console app for PrusaSlicer post-processing and manual use.
- **tests/SplineTravel.Tests** — Unit tests.

## PrusaSlicer

Configure **Print settings → Output options → Post-processing scripts** with the path to `splinetravel` (or `splinetravel.exe`). PrusaSlicer passes the G-code file path as the last argument; the tool modifies the file in place.

## Configuration

Place `splinetravel.json` next to the executable or use `--config <path>`. Example:

```json
{
  "UseSplineTravel": true,
  "RetractLength": 1.5,
  "Acceleration": 800,
  "CurveJerk": 2,
  "SpeedLimit": 200,
  "LoopTolerance": 0.3,
  "ZHop": 1
}
```

## Target framework

The project targets **.NET 10**. If you only have the .NET 8 SDK, change `TargetFramework` to `net8.0` in `SplineTravel.Core.csproj` and `SplineTravel.Cli.csproj`.
