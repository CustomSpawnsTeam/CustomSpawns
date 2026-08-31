# Task 5 report — bounded occupation garrison

## Delivered API

- `OccupationGarrisonAllocation` is a pure primitive-ID/snapshot rule. It plans
  healthy nonhero transfers only from the captured participant IDs, with one
  donor-wide initial loss budget, native garrison count/capacity/current strength,
  native militia-party strength, deterministic ranged/non-ranged alternation,
  and exact source/destination XP-cap preflight.
- `InvasionGarrisonTransfer(DynamicSpawnData)` exposes
  `TryApply(InvasionCaptureContext, Town, InvasionGroupState, InvasionData)`.
  It returns `true` after a valid zero, partial, or successful allocation and
  returns `false` for readiness failure or a terminal cancellation. The caller
  must invoke it before Task 4 food relief and the one `CompleteOccupation`.
- All expected malformed/XP-ineligible candidates are removed during preflight.
  Once roster mutation begins, unexpected exceptions or postcondition mismatch
  cancel the existing capture context, log the event, suppress later replay and
  food/completion, and do not attempt automatic repair.

## Verification

`OccupationGarrisonAllocationTests` covers adequate native garrison, strength
targets, capacity and donor scarcity, participant/hero/wounded exclusions,
deterministic ranged/non-ranged selection, XP conservation/caps, empty XP
intervals, and sequential same-character destination XP/count prediction.

The focused staged test command completed successfully before the final
militia-strength adapter correction:

```text
run-customspawns-focused-baseline.ps1 ...
  -Filter FullyQualifiedName~CustomSpawns.Tests.Invasions.OccupationGarrisonAllocationTests
  -TimeoutSeconds 180
```

Log: `validation-logs/customspawns-focused-baseline-20260831-200024.log`.
It used `Y:` as the staged GameFolder and reported the installed CustomSpawns
module unchanged. Per accelerated orchestration, no suite was rerun after the
small final adapter-only correction; the consolidated final run must compile it.

## Limits

Pure tests do not establish runtime roster-call exception behavior, owner-clan
wages, desertion/recruitment, food ordering, or sustained garrison balance.
Task 8 remains responsible for the disposable-save measurements.
