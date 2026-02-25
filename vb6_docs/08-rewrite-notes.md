# SplineTravel VB6 - Rewrite Considerations

## Suggested Target Languages

| Language | Pros | Cons |
|----------|------|------|
| **Python** | Simple, readable; good for scripts; easy CLI/PrusaSlicer integration | Slower for large G-code files |
| **C#** | Cross-platform (.NET); strong typing; good tooling | Requires .NET runtime |
| **Rust** | Fast; memory-safe; no runtime | Steeper learning curve |
| **Go** | Simple; single binary; good performance | Less common for this domain |

**Recommendation:** Python for quick CLI/post-processing; C# or Rust if performance and single-binary distribution matter.

## PrusaSlicer Post-Processing Contract

From [PrusaSlicer docs](https://help.prusa3d.com/article/post-processing-scripts_283913):

1. Script receives the **absolute path** to a **temporary** G-code file as the **last argument**.
2. Script must **read** from that file, **modify** it in place, and **write** the result back to the same file.
3. Script can be any executable (Perl, Python, Ruby, Bash, etc.).

**CLI usage:**
```bash
SplineTravel.exe /path/to/temp.gcode
# or
python splinetravel.py /path/to/temp.gcode
```

**Environment variables (optional):**
- `SLIC3R_PP_HOST` — "File", "PrusaLink", "OctoPrint", etc.
- `SLIC3R_PP_OUTPUT_NAME` — final output filename

## Mapping VB6 to Target Language

| VB6 | Python | C# |
|-----|--------|-----|
| Module | Module / namespace | Static class / namespace |
| Class | Class | Class |
| typVector3D | tuple / dataclass / namedtuple | struct / record |
| clsVector3D | class with X,Y,Z | class / struct |
| clsChain | list / deque | List\<GCommand\> or linked list |
| typCurrentState | dataclass | struct / record |
| Err.Raise / On Error | try/except | try/catch |
| ByRef | Return tuple / inout | ref / out |
| Optional param | Optional with default | Optional\<T\> / default |

## Headless / CLI Mode

Current VB6 app is GUI-only. For PrusaSlicer post-processing, the rewrite should:

1. Support CLI: `splinetravel input.gcode` or `splinetravel input.gcode output.gcode`
2. For PrusaSlicer: if one argument, use same path for in/out (modify in place)
3. Load config from file (e.g. `splinetravel.json`) or command-line args
4. Optionally keep a GUI for interactive use

## Test Vectors

**Verify Epos consistency:** The `cmdVerify` flow in mainForm compares:
- Original file: `chain1.last.CompleteStateAfter.Epos`
- Processed file: `chain2.last.CompleteStateAfter.Epos`

For a correct post-processor, these should match (or be within tolerance).

**Test approach:**
1. Use existing G-code samples from slicers (PrusaSlicer, Cura, etc.)
2. Run SplineTravel (VB6 or rewrite) on sample
3. Compare Epos at end
4. Verify printer can execute output (dry run in simulator if available)

## Potential Improvements

| Improvement | Description |
|-------------|-------------|
| Arc support (G2/G3) | VB6 treats all moves as linear; arcs could be preserved or approximated |
| Config format | JSON or YAML instead of INI; more structured, easier to parse |
| Parallel processing | Process multiple files or groups in parallel (if language supports) |
| Unit tests | Test Bezier fitting, retract curve, G-code parsing in isolation |
| Logging | Replace Debug.Print with proper logging |
| Validation | Validate G-code before processing; report line numbers on errors |
| Seam tolerance | Expose loopTol and retract time as CLI/config options |
