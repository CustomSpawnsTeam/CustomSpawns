# Task 4 report — Bannerlord v1.4.6 retarget

## Changes

- Retargeted `supported-game-versions.txt` from `v1.3.15` to `v1.4.6`.
- Updated `CustomSpawnsDiplomacyModel` for the v1.4.6 `DiplomacyModel` contract:
  removed the two obsolete ally-related war-score overrides and delegated
  `WarDeclarationScorePenaltyAgainstTradePartners`.
- Replaced v1.4.6-incompatible writes to read-only `MapEventParty`
  renown/influence results with the public, equivalent battle reward actions.
  The behavior accumulates its custom amounts and applies each action once,
  preserving the prior aggregate reward behavior without reflection or private
  API access.

## Verification

```text
dotnet build .\CustomSpawns\CustomSpawns.csproj --configuration Release -p:OverrideGameVersion=v1.4.6 -v:minimal
Succeeded: 0 errors, 139 existing/environment warnings.

dotnet build .\CustomSpawns.Tests\CustomSpawns.Tests.csproj --configuration Release -p:OverrideGameVersion=v1.4.6 -v:minimal
Succeeded: 0 errors, 41 existing/environment warnings.

dotnet build .\CustomSpawns.sln --configuration Release -p:OverrideGameVersion=v1.4.6 -v:minimal
Failed with the repository's pre-existing solution-level mode: 0 warnings and 0 errors.
The individual production and test project builds above succeeded.

dotnet test .\CustomSpawns.Tests\CustomSpawns.Tests.csproj --configuration Release -p:OverrideGameVersion=v1.4.6 --no-build --filter "FullyQualifiedName~DiplomacyReliabilityTests|FullyQualifiedName~CustomSpawnsDiplomacyModelTests|FullyQualifiedName~PartySizeCalculatedSubjectTests|FullyQualifiedName~CustomSpawnSafePassageBarterableTests|FullyQualifiedName~CustomSpawnsPartyComponentPersistenceTests"
Succeeded. The local runner reports binlog-only mode, so test counts are unavailable.

git diff --check
Succeeded.

git grep -n -i harmony -- ':!changelog.txt'
No tracked source or project-file references found.
```

## Scope notes

- The existing `changelog.txt` was not edited and no 1.3 compatibility is claimed.
- The build-generated untracked `CustomSpawns/FodyWeavers.xsd` was removed and not committed.

## Review fix round 1

- Replaced the premature direct calls to `GainRenownAction` and
  `GainKingdomInfluenceAction` in `OnPlayerBattleEndEvent` with the public
  `MapEventParty.SetRenownInfluenceAndMoraleOnBattleEnd` aggregate setter.
- The v1.4.6 source calculates a player battle's `ExplainedNumber` rewards
  before dispatching `OnPlayerBattleEndEvent`, then commits them afterward.
  The behavior now copies the calculated renown and influence aggregates,
  adds its configured totals, and preserves the calculated morale aggregate.
  Bannerlord consequently commits all rewards once through its normal path,
  including its existing null-leader handling.
- No focused map-event unit test was added because the required map-event
  construction and normal commit path are internal Bannerlord APIs. The
  existing focused diplomacy, party-size/safe-passage, and lifecycle suite was
  rerun successfully in the local binlog-only test runner.

```text
dotnet build .\CustomSpawns\CustomSpawns.csproj --configuration Release -p:OverrideGameVersion=v1.4.6 -v:minimal
Succeeded: 0 errors, 138 existing/environment warnings.

dotnet build .\CustomSpawns.Tests\CustomSpawns.Tests.csproj --configuration Release -p:OverrideGameVersion=v1.4.6 -v:minimal
Succeeded: 0 errors, 41 existing/environment warnings.

dotnet test .\CustomSpawns.Tests\CustomSpawns.Tests.csproj --configuration Release -p:OverrideGameVersion=v1.4.6 --no-build --filter "FullyQualifiedName~DiplomacyReliabilityTests|FullyQualifiedName~CustomSpawnsDiplomacyModelTests|FullyQualifiedName~PartySizeCalculatedSubjectTests|FullyQualifiedName~CustomSpawnSafePassageBarterableTests|FullyQualifiedName~CustomSpawnsPartyComponentPersistenceTests"
Succeeded. Counts unavailable in binlog-only mode.

git diff --check
Succeeded.
```
