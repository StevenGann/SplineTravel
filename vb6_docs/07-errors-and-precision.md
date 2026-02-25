# SplineTravel VB6 - Errors and Precision

## mdlErrors

### Throw(er, Source, extraMessage)

**Purpose:** Raise an error by number and optional source/message.

**Usage:**
- `Throw errTooSlow` — raise with default message
- `Throw errInvalidArgument, "ProcedureName", extraMessage:="..."` — raise with custom source and message
- `Throw` (no args) — re-raise last error (analog of C++ `throw;`)

**Implementation:** If `er = 0`, calls `ErrRaise` (re-raise). Else uses `Select Case` to map `eErrors` to message strings, then `Err.Raise er, Source, Message`.

### PushError / PopError

**Purpose:** Save and restore error info for cleanup.

**Usage:**
```vb
On Error GoTo eh
' ... do work ...
Exit Sub
eh:
  PushError
  Close f
  PopError
  Throw
```

**Logic:**
- `PushError`: read Err into vtError; push onto ErrorStack; increment nInStack.
- `PopError`: pop from ErrorStack; decrement nInStack; optionally raise via Err.Raise.

### MsgError(Message, Style, Assertion)

**Purpose:** Show error message box and return user response.

**Logic:** If `errCancel`, return vbCancel. Else build message from Err.Description or Message; show MsgBox; return result.

### Error Codes (eErrors)

| Code | Value | Meaning |
|------|-------|---------|
| errZeroTimeMove | 12345 | Zero or negative time for move |
| errTooSlow | 12346 | Entry/exit speed too small for spline fitting |
| errClassNotInitialized | 12347 | Class not properly initialized |
| errInvalidCommand | 12348 | Invalid G-code command |
| errNotInChain | 12349 | Command not in chain |
| errAlreadyInChain | 12350 | Command already in chain |
| errVerificationFailed | 12351 | Chain verification failed |
| errWrongConfigLine | 12352 | Invalid config line format |
| errCancel | 32755 | User canceled |
| errIndexOutOfRange | 9 | Index out of range |
| errInvalidArgument | 5 | Invalid argument |
| errWrongType | 13 | Type mismatch |

## Precision Constants (mdlPrecision)

### Decimals

| Constant | Default | Purpose |
|----------|---------|---------|
| posDecimals | 3 | Decimal places for X/Y/Z output |
| extrDecimals | 3 | Decimal places for E output |
| speedDecimals | -1 | Decimal places for F output (-1 = no rounding) |

### Confusion Thresholds

| Constant | Formula | Purpose |
|----------|---------|---------|
| posConfusion | 10^(-posDecimals-1) | Threshold for "unchanged" position |
| extrConfusion | 10^(-extrDecimals-1) | Threshold for "unchanged" extrusion |
| speedConfusion | 10^(-speedDecimals-1) | Threshold for "unchanged" speed |
| RelConfusion | 1e-12 | Floating-point comparison tolerance |

**Usage:** Skip writing X/Y/Z/E/F if change is below confusion threshold. Use RelConfusion for t-step comparisons and loop termination.

## VB6 Error Pattern

**Standard pattern:**
```vb
On Error GoTo eh
' ... work ...
Exit Sub
eh:
  MsgError
  ' cleanup ...
  Throw
```

**Cleanup with PushError/PopError:**
```vb
cleanup:
  PushError
  Close f
  chain.delete
  PopError
  Throw
```

**Resume options:** MsgError can return vbAbort, vbRetry, vbIgnore. ApplyConfigStr uses Abort/Retry/Ignore to allow user to skip bad config lines.
