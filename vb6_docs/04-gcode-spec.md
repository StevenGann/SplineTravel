# SplineTravel VB6 - G-code Handling

## Supported Commands

| Command | Code | Description |
|---------|------|-------------|
| G0 | G0_quickMove | Rapid move |
| G1 | G1_controlledMove | Linear move (with F and E) |
| G4 | G4_dwell | Dwell (pause) |
| G21 | G21_setUnitMm | Set units to mm |
| G90 | G90_absolutePos | Absolute positioning |
| G91 | G91_relativePos | Relative positioning |
| G92 | G92_overridePos | Set current position (X/Y/Z/E) |
| M82 | M82_absoluteE | Absolute extrusion |
| M83 | M83_relativeE | Relative extrusion |

Command encoding in `clsGCommand`: `myCmdType = number * 0x100 + letter`, e.g. G1 = 0x147.

## Parsing

**Location:** `clsGCommand.ParseString`

**Steps:**

1. Strip comment: everything after `;` is removed.
2. Strip checksum: everything after last `*` is removed.
3. Split remaining text by space into words.
4. First word: command. Parse as `letter + number` (e.g. G1 → letter G, number 1). Sets `myCmdType`.
5. Subsequent words: parse as `letter + value`. Store in `arguments(Asc(letter))` with `.specified = True` and `.Value = value`.

**Format:** Each word must start with A–Z; value is parsed from remainder. Example: `G1 X10.5 Y20 F1200` → G1, X=10.5, Y=20, F=1200.

**Empty lines:** After stripping, if empty → `myCmdType = egcEmpty`, no arguments.

## typCurrentState

Represents machine state after a command:

```vb
Type typCurrentState
  Speed As Double         ' mm/s (feedrate / 60)
  Pos As typVector3D      ' X, Y, Z position
  Epos As Double          ' extrusion accumulator (filament length)
  MoveRelative As Boolean ' G91 mode
  ExtrusionRelative As Boolean  ' M83 mode
End Type
```

**State propagation:** `RecomputeStates` copies `stateBefore` from `prevCommand.CompleteStateAfter`, then applies current command changes (X, Y, Z, E, F, G90/G91, M82/M83, G92).

## Command Classification

| Type | Condition | Notes |
|------|-----------|-------|
| Build move | `isMove` and XY travel and positive E | Extrusion move |
| Travel move | `isMove` and XY travel and no E | Non-extruding move |
| Extruder move | `isMove` and no XY travel and E change | Retract/unretract |
| Retract | `isMove` and negative E change | May also have XY |
| Other | G4, M82, M83, G90, G91, G92, etc. | Setup commands |

**Implementation:** `isBuildMove`, `isTravelMove`, `isExtruderMove` compare `stateBefore` vs `stateAfter` (travel distance, E delta). Thresholds use `1E-100` for "zero" checks.

## Output: clsGMove.GenerateGCode

**Inputs:** `CurrentState` (typCurrentState), `EError` (ByRef, rounding tracker).

**Logic:**

1. If pause (traveldist and Extrusion both near zero): output `G4 P<time_ms>`.
2. Else (move):
   - Compute delta `d = p2 - p1` (or `p2` for absolute).
   - Write X/Y/Z only if change exceeds `posConfusion`.
   - Write F only if feed rate change exceeds `speedConfusion`.
   - Write E: if relative, add EError and round; update EError for next command. If absolute, round `CurrentState.Epos + Extrusion`.

**Format:** `G1 X... Y... Z... E... F...` or `G4 P...`

**Precision:** Uses `mdlPrecision.posDecimals`, `extrDecimals`, `speedDecimals` for rounding; `posConfusion`, `extrConfusion`, `speedConfusion` for "unchanged" checks.

## E Rounding Error Tracking (EError)

For relative extrusion, rounding can accumulate. `EError` is passed ByRef:

- **Input:** Add to extrusion before rounding.
- **Output:** Store rounding residual in EError for next command.

Formula: `wrE = Me.Extrusion + EError`; `EError = wrE - Round(wrE, extrDecimals)`.

## Position and Feedrate

- **G90:** X/Y/Z are absolute; write target position.
- **G91:** X/Y/Z are relative; write delta from previous position.
- **F:** Feedrate in mm/min; stored as Speed (mm/s) internally; output as `F = Speed * 60`.
- **M82/M83:** E absolute vs relative; affects how E is output and how Epos is updated.
