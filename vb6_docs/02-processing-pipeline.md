# SplineTravel VB6 - Processing Pipeline Detail

## High-Level Flowchart

```mermaid
flowchart TD
    Start([Process Start])
    Read[Read G-code file into clsChain]
    Classify[Classify commands: Build / Travel / Other]
    Split[Split chain into move groups]
    Seam{Seam concealment enabled?}
    SeamLoop[For each build group: detect loops, inject retract/unretract]
    Travel{Travel mode?}
    Spline[Spline travel: FitBezier + GenerateMoveTrainForBezier]
    Straight[Straight travel: retract, Z-hop, move, Z-hop down, unretract]
    Write[Write groups to output file]
    Cleanup[Cleanup: delete chains]
    Done([Done])

    Start --> Read
    Read --> Classify
    Classify --> Split
    Split --> Seam

    Seam -->|Yes| SeamLoop
    Seam -->|No| Travel
    SeamLoop --> Travel

    Travel -->|Spline| Spline
    Travel -->|Straight| Straight
    Spline --> Write
    Straight --> Write
    Write --> Cleanup
    Cleanup --> Done
```

## Group Detection Algorithm

1. Initialize `moveGroups(0)` as `ectOther` (dummy group for setup commands).
2. Walk `chain` from first to last command.
3. For each command:
   - Compute `curCmdType`: Build, Travel, or Other.
   - If `curCmdType <> Other`:
     - If `curCmdType <> moveGroups(nMoveGroups-1).chType`: start new group, set `firstMoveRef` and `lastMoveRef`.
     - Else: update `lastMoveRef` for current group.
4. After the pass, `moveGroups` holds 0..n groups in sequence.

## Splitting the Chain

```mermaid
flowchart LR
    subgraph Original [Original Chain]
        C0[C0] --> C1[C1] --> C2[C2] --> C3[C3] --> C4[C4] --> C5[C5]
    end

    subgraph Groups [After Split]
        G0[Group 0: Other]
        G1[Group 1: Build]
        G2[Group 2: Travel]
        G3[Group 3: Build]
    end

    Original -.-> Groups
```

For each group `iGroup`, withdraw from `c1` to `c2`:

- `c1` = first command of group (or `chain.first` for group 0)
- `c2` = last command of group (or `chain.last` for last group)

`chain.withdrawChain(c1, c2, preserveLinks:=True)` returns a new chain containing those commands; the original chain is updated to remove them.

## Seam Concealment Loop

```mermaid
flowchart TD
    subgraph SeamConcealment [Seam Concealment]
        ForEach[For each build group]
        Dist{Dist first, last ≤ loopTol?}
        IsLoop[Yes: treat as closed loop]
        Unretract[Inject unretract at start of first move]
        AddCopy1[Add copy of first move at end]
        Retract[Inject retract at end of last move]
        AddCopy2[Add copy of last move at end]
        Recompute[Recompute states, regenerate E values]
    end

    ForEach --> Dist
    Dist -->|Yes| IsLoop
    IsLoop --> Unretract
    Unretract --> AddCopy1
    AddCopy1 --> Retract
    Retract --> AddCopy2
    AddCopy2 --> Recompute
    Dist -->|No| ForEach
    Recompute --> ForEach
```

**Parameters:**

- `loopTol` — loop detection tolerance (default 0.3 mm)
- `retractTime` — `retract / retractSpeed` (for concealed retraction)
- `retractSpeed` — speed of concealed retraction (default 8 mm/s)

**Logic (simplified):**

1. Compare `firstMoveRef.CompleteStateBefore.Pos` with `lastMoveRef.CompleteStateAfter.Pos`.
2. If distance ≤ `loopTol`, treat as a closed loop.
3. Walk moves in the group; track remaining time for unretract/retract.
4. Inject unretract at start (first part of first move), retract at end (last part of last move).
5. Split moves if needed; duplicate moves at end to preserve extrusion.
6. Recompute all states with `preserveDeltaE:=True` and regenerate G-code strings.

## Spline Travel Branch

```mermaid
flowchart TD
    subgraph SplineTravel [Spline Travel]
        DeleteOld[Delete existing travel chain]
        Setup[Setup clsTravelGenerator]
        FitBezier[FitBezier: iterative acceleration fitting]
        GenMoves[GenerateMoveTrainForBezier: jerk-limited tessellation]
        CreateCmds[Create clsGCommand per segment]
        LinkChains[Link new chain to prev/next build chains]
    end

    DeleteOld --> Setup
    Setup --> FitBezier
    FitBezier --> GenMoves
    GenMoves --> CreateCmds
    CreateCmds --> LinkChains
```

**Setup values:** acceleration, CurveJerk, speedLimit, Retract, RetractAcceleration, RetractJerk, ZJerk.

**Bezier fitting:** See [05-algorithms.md](05-algorithms.md).

**Output:** Array of `clsGMove` → converted to `clsGCommand` → written as G1 lines.

## Straight Travel Branch

```mermaid
flowchart TD
    subgraph StraightTravel [Straight Travel]
        DeleteOld[Delete existing travel chain]
        Retract{Retract needed?}
        AddRetract[Add retract move]
        ZHop{Z-hop > 0?}
        AddZUp[Add Z-hop up move]
        AddMain[Add main XY move]
        AddZDown[Add Z-hop down move]
        Unretract{Unretract needed?}
        AddUnretract[Add unretract move]
        LinkChains[Link chain to prev/next build chains]
    end

    DeleteOld --> Retract
    Retract -->|Yes| AddRetract
    Retract -->|No| ZHop
    AddRetract --> ZHop
    ZHop -->|Yes| AddZUp
    ZHop -->|No| AddMain
    AddZUp --> AddMain
    AddMain --> AddZDown
    AddZDown --> Unretract
    Unretract -->|Yes| AddUnretract
    Unretract -->|No| LinkChains
    AddUnretract --> LinkChains
```

**Sequence:**

1. Retract (optional): E-only move, negative extrusion at `txtRSpeedStraight`
2. Z-hop up (optional): Z-only move, Z += ZHop, speed = `txtSpeedStraight`
3. Main move: XY travel (Z includes Z-hop), speed = `txtSpeedStraight`
4. Z-hop down (optional): Z-only move, Z back to target
5. Unretract (optional): E-only move, positive extrusion at `txtRSpeedStraight`

## Output Phase

```mermaid
flowchart LR
    subgraph Output [Output Phase]
        Open[Open output file]
        Loop[For each move group in order]
        WriteLine[Print cmd.strLine for each cmd in group]
        Close[Close file]
    end

    Open --> Loop
    Loop --> WriteLine
    WriteLine --> Loop
    Loop --> Close
```

For each group, iterate `chain.first` → `chain.last` via `nextCommand`, and write `cmd.strLine` for each line.
