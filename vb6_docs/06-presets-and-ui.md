# SplineTravel VB6 - Presets and UI

## Preset Storage

**Location:** `presets\` folder under executable directory.

**Path:** `App.path + "presets\"` (from `mdlFiles.PresetsPaths`).

**Format:** INI-style, one property per line: `ControlName.Property = value`

**Example:**
```ini
txtRetract.Text = 1.5
txtAcceleration.Text = 800
txtCurveJerk.Text = 2
chkSeamConceal.Value = 1
optTravelSpline.Value = -1
optTravelStraight.Value = 0
```

Values containing special characters (spaces, %, non-printable) are escaped with `%XXXX` (4 hex digits) via `EscapeString`/`unEscapeString`.

## GetConfigString / ApplyConfigStr

### GetConfigString(includeFilenames)

**Purpose:** Serialize form controls to a config string.

**Logic:**
- Iterate all controls in `Me` (form).
- Skip controls with `Tag = "!f"` if `includeFilenames` is False (e.g. txtFNIn, txtFNOut).
- TextBox: `ControlName.Text = EscapeString(value)`
- CheckBox: `ControlName.Value = 0|1`
- OptionButton: `ControlName.Value = True|False`
- Append each line with vbNewLine.

**Output:** Multi-line string suitable for writing to preset file.

### ApplyConfigStr(configStr, suppressErrorMessages, includeFilenames)

**Purpose:** Deserialize config string into form controls.

**Logic:**
- Split by vbNewLine.
- For each non-empty line: split by `=` (limit 2); left = object/property, right = value.
- Split left by `.` (limit 2) → object name, property name.
- Use `CallByName(Me, objName, VbGet)` to get control; `CallByName(obj, propName, VbLet, unEscapeString(value))` to set.
- Skip controls with `Tag = "!f"` if `includeFilenames` is False.

**Errors:** Wrong format → throw `errWrongConfigLine`. On error: MsgError (unless `suppressErrorMessages`); resume with Abort/Retry/Ignore.

## clsBlokada Usage

**Purpose:** Block event handlers during preset list refresh.

**Usage:**
- `keeper = pm.block.block` — block; keeper holds lock.
- When keeper goes out of scope, `Class_Terminate` calls `Decr` on master.
- In `cmbPreset_Click`: if `pm.block` (isBlocked), exit immediately to avoid recursive updates.

**Pattern:** `RefillPresets` wraps in `keeper = pm.block.block`; `keeper.Unblock` or let keeper go out of scope when done.

## UI Controls Map

| Control | Purpose | Default |
|---------|---------|---------|
| `txtFNIn` | Input G-code path | — |
| `txtFNOut` | Output G-code path | — |
| `cmbPreset` | Preset dropdown | — |
| `cmdSaveAs` | Save preset as... | — |
| `cmdDelete` | Delete preset | — |
| `optTravelSpline` | Spline travel mode | Selected |
| `optTravelStraight` | Straight travel mode | — |
| `chkSeamConceal` | Enable seam concealment | Checked |
| `txtRetract` | Retraction length (mm) | 1.5 |
| `txtAcceleration` | Spline acceleration (mm/s²) | 800 |
| `txtCurveJerk` | Curve tessellation jerk (mm/s) | 2 |
| `txtSpeedLimit` | Spline speed limit (mm/s) | 200 |
| `txtEAccel` | E acceleration for retraction (mm/s²) | 1000 |
| `txtEJerk` | E jerk for retraction (mm/s) | 8 |
| `txtZJerk` | Z jerk for hopping | 0 |
| `txtLoopTol` | Loop detection tolerance (mm) | 0.3 |
| `txtRSpeedSC` | Speed of concealed retraction (mm/s) | 8 |
| `txtZHop` | Z-hop height (mm) | 1 |
| `txtSpeedStraight` | Straight travel speed (mm/s) | 200 |
| `txtRSpeedStraight` | Straight retract speed (mm/s) | 300 |
| `txtNotes` | User notes (multi-line) | — |
| `cmdProcessFile` | Go button | — |

## ChangeWasMade / purgeModified

**ChangeWasMade:** Called when any editable control changes. Sets `pm.curPresetIsModified = True` and appends `*` to preset name in dropdown if not already present.

**purgeModified:** Removes `*` from preset name in dropdown; sets `curPresetIsModified = False`.

**Dirty state:** When switching presets with unsaved changes, user is prompted to discard or save.

## Preset Lifecycle

1. **Form_Load:** `mdlPrecision.InitModule`
2. **Form_Activate:** `RefillPresets`
3. **Load preset:** `LoadPreset(FilePath)` → ResetSettings → ApplyConfigStr → purgeModified → SelectPreset
4. **Save preset:** `SavePreset "(last used)"` on Go click; `SavePreset "(since last close)"` on Form_QueryUnload
5. **Save as:** InputBox for name → `WritePresetFile` → update curPresetFN, RefillPresets
