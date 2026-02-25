# SplineTravel VB6 - Module and Class Reference

## Modules

### mainForm.frm (Form)

**Purpose:** Main UI: presets, travel options, seam concealment, I/O paths.

**Key members:**
- `cmdProcessFile_Click` — entry point; calls `mdlWorker.Process`
- `GetConfigString(includeFilenames)` — serialize form controls to string (ControlName.Property = value)
- `ApplyConfigStr(configStr, suppressErrorMessages, includeFilenames)` — deserialize to form controls
- `LoadPreset(FilePath)`, `SavePreset(presetName)`, `WritePresetFile`, `ReadPresetFile`
- `RefillPresets`, `SelectPreset`, `ChangeWasMade`, `purgeModified`

**UI controls:** `txtFNIn`, `txtFNOut`, `cmbPreset`, `optTravelSpline`, `optTravelStraight`, `chkSeamConceal`, `txtRetract`, `txtAcceleration`, `txtCurveJerk`, `txtSpeedLimit`, `txtEAccel`, `txtEJerk`, `txtZJerk`, `txtLoopTol`, `txtRSpeedSC`, `txtZHop`, `txtSpeedStraight`, `txtRSpeedStraight`, `txtNotes`.

### mdlWorker.bas (Module)

**Purpose:** Core processing pipeline.

**Key members:**
- `Process(FNIn, FNOut, cfg)` — main pipeline: read → group → seam concealment → travel replace → write
- `ReadGCodeFile(path)` — read G-code into `clsChain`, create `clsGCommand` per line
- `timeToDoEvents()` — throttled DoEvents for UI responsiveness

**Private types:** `eChainType`, `eRetractBlenderState`, `typMoveChain`, `typTravelMoveRef`.

### mdlCommon.bas (Module)

**Purpose:** Shared types and helpers.

**Key members:**
- `typCurrentState` — Speed, Pos (typVector3D), Epos, MoveRelative, ExtrusionRelative
- `Pi` — 3.14159265358979
- `vtStr(val)` — Trim(Str(val))
- `prepad(st, toLength, padChar)` — left-pad string
- `EscapeString(st)`, `unEscapeString(st)` — percent-encode/decode for preset values

### mdlErrors.bas (Module)

**Purpose:** Error handling.

**Key members:**
- `Throw(er, Source, extraMessage)` — raise error or re-raise
- `PushError`, `PopError` — save/restore Err for cleanup
- `MsgError(Message, Style)` — show error message box
- `eErrors` — enum: errZeroTimeMove, errTooSlow, errInvalidCommand, errNotInChain, etc.

### mdlFiles.bas (Module)

**Purpose:** Preset paths and file utilities.

**Key members:**
- `PresetsPaths()` — returns `[App.path + "presets\"]`
- `getListOfFiles(paths(), matchString)` — list files in folders
- `getFileTitle(path)` — filename without extension
- `GetFileName(strPath)`, `CropExt(path)`, `ValFolder(strFolder)`

### mdlPrecision.bas (Module)

**Purpose:** Rounding and precision thresholds.

**Key members:**
- `posDecimals`, `extrDecimals`, `speedDecimals` — decimal places
- `posConfusion`, `extrConfusion`, `speedConfusion` — equality thresholds
- `RelConfusion` — 1e-12
- `InitModule`, `updateConfusions`
- `Round(Value, Decimals)`

### Vector3D.bas (Module)

**Purpose:** `typVector3D` and static math functions.

**Key members:**
- `typVector3D` — X, Y, Z (Double)
- `Dist(point1, point2)` — Euclidean distance
- `Length(vec)` — vector length
- `Subtracted`, `Multed`, `Combi2`, `Combi3`, `Combi4` — vector math
- `Normalized(vec)`, `Dot(vec1, vec2)`
- `makeClsVector(vec)` — create clsVector3D from typVector3D

---

## Classes

### clsChain.cls

**Purpose:** Doubly linked list of `clsGCommand`.

**Key members:**
- `first`, `last`, `size`
- `Add(cmd, Before, After)` — insert command
- `withdrawChain(cmdFrom, cmdTo, preserveLinks)` — extract range into new chain
- `withdraw(cmd, keepRefs)` — remove single command
- `MakeLink(cmd1, cmd2)` — set prev/next between commands
- `delete` — clear chain, unlink all commands
- `verify()` — debug integrity check

**Notes:** Supports inter-chain links (prev/next across different chains). `wrap`, `unwrapMe` are internal.

### clsGCommand.cls

**Purpose:** Parsed G-code command with state and move classification.

**Key members:**
- `strLine` — original or regenerated G-code line
- `arguments(65 To 90)` — letter/value pairs (A–Z), indexed by Asc(letter)
- `myCmdType` — eGCommand enum (G0, G1, G4, M82, M83, G90, G91, G92)
- `prevCommand`, `nextCommand`, `inChain`

**State:**
- `CompleteStateBefore`, `CompleteStateAfter` — typCurrentState
- `RecomputeStates(preserveDeltaE, keepStateBefore)` — propagate state from prev command

**Classification:**
- `isBuildMove` — XY motion + positive E
- `isTravelMove` — XY motion, no E
- `isExtruderMove` — E change only
- `isRetract` — negative E change

**Methods:**
- `ParseString(throwIfInvalid)` — parse strLine into arguments and myCmdType
- `constructMove`, `getMove`, `setMove(newMove, EError)`
- `split(timePoint, EError)` — split move at time
- `getPrevMove`, `getNextMove` — navigate to adjacent move commands
- `regenerateString` — rebuild strLine from arguments
- `execTime`, `Echange`, `PosChange`, `ZChange`, `getEnterSpeed`, `getExitSpeed`

### clsGMove.cls

**Purpose:** Geometric move: p1, p2, time, extrusion.

**Key members:**
- `p1`, `p2` — clsVector3D (start/end positions)
- `time` — duration (seconds)
- `Extrusion` — filament length; positive = extrude, negative = retract

**Properties:**
- `traveldist`, `Speed`, `ExtrusionSpeed`, `FeedRate`
- `isValid`, `isPause` — dwell when traveldist and Extrusion both near zero

**Methods:**
- `GenerateGCode(CurrentState, EError)` — output G1 (or G4 P for dwell)
- `split(timePoint, Part1, Part2)` — split move at time; returns True if split

### clsTravelGenerator.cls

**Purpose:** Bezier fitting and spline tessellation.

**Inputs:**
- `p1`, `p2` — start/end positions
- `inSpeed`, `outSpeed` — entry/exit velocity vectors
- `speedLimit`, `acceleration`, `CurveJerk`, `ZJerk`, `Retract`, `RetractAcceleration`, `RetractJerk`
- `bRetract`, `bUnretract` — whether to include retract/unretract in spline

**Methods:**
- `FitBezier(moveTimeResult)` — iterative fitting; returns clsBezier and move time
- `GenerateMoveTrainForBezier(arrMoves, bz, TimeOfMove)` — jerk-limited tessellation into clsGMove array

**Notes:** Uses `clsRetractCurve` for retraction over time; steps along Bezier parameter t with `shrink_interval` to limit jerk.

### clsBezier.cls

**Purpose:** Cubic Bezier curve (P0..P3 poles).

**Key members:**
- `Pole(0..3)` — typVector3D control points
- `DerivJerk` — jerk limit for tessellation

**Methods:**
- `getValue(t)`, `getDeriv(t)`, `getDeriv2(t)` — position, velocity, acceleration at parameter t (0..1)
- `shrink_interval(prev_t, cur_t)` — shrink step to satisfy jerk limit; modifies cur_t
- `recompute` — update t_of_low_a, max_a for acceleration
- `getNextBreakpoint(prev_t)` — next t breakpoint

**Math:** B(t) = (1-t)³P0 + 3(1-t)²tP1 + 3(1-t)t²P2 + t³P3

### clsRetractCurve.cls

**Purpose:** Retraction as a function of time; parabolic accelerate/decelerate.

**Inputs:**
- `MoveTime` — total travel time
- `RetractLen` — retraction length
- `RetractA` — acceleration
- `bRetract`, `bUnretract` — enable/disable
- `DerivJerk` — jerk limit for tessellation

**Methods:**
- `getValue(t)` — cumulative retraction at parameter t (0..1)
- `getDeriv(t)`, `getDeriv2(t)` — derivatives w.r.t. t
- `shrink_interval(prev_t, cur_t)` — shrink step for jerk
- `ActualRetract` — actual retraction length (may be reduced if time insufficient)

**Phases:** t_start1..t_mid1 (accel), t_mid1..t_end1 (decel), plateau, t_start2..t_mid2 (accel), t_mid2..t_end2 (decel).

### clsVector3D.cls

**Purpose:** Mutable 3D vector with methods.

**Key members:**
- `X`, `Y`, `Z` — Double

**Methods:**
- `Length`, `Added`, `Subtrd`, `Multed`, `Mult`, `Normalized`, `Dot`
- `copyFrom`, `copyFromT`, `Round`, `Copy`
- `asTypVector3D` — convert to typVector3D
- `SubtrdT` — subtract typVector3D

### clsBlokada.cls

**Purpose:** Block/unblock mechanism (RAII-style locking).

**Usage:**
- `keeper = master.block` — increment block count; keeper holds lock
- `keeper.Unblock` — decrement; or let keeper go out of scope (Class_Terminate calls Decr)
- `isBlocked` — True when blockCount > 0

**Notes:** Used in mainForm to block preset dropdown events during refill.

### StringAccumulator.cls

**Purpose:** Efficient string concatenation.

**Key members:**
- `content` — get/set string
- `Append(StringToAppend)`
- `Backspace(numCharsToErase)`
- `Length` — number of characters
- `Clear` — reset
