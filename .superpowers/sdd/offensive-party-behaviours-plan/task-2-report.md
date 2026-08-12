# Task 2 Report: Per-spawn resolver and generic persistence lifecycle

## Status

DONE_WITH_CONCERNS

Implementation is complete and committed. The solution builds successfully. Focused tests compile into the test assembly, but the NUnit adapter reports zero discovered tests in this environment, so no passing test count can be claimed.

## Requirements implemented

- Added a generic type-keyed provider registry/resolver that resolves the owning `SpawnDto` from `CsPartyData.OwningSpawnTemplateId`, respects each spawn's ordered `PriorityPartyBehaviours`, returns the first applicable result, falls through on `NotApplicable`, and stops on `ApplicableWithoutBehavior`.
- Added home-settlement patrol as the ultimate fallback for missing owners, missing spawns, null/empty configuration, and exhausted configured providers.
- Added clear composition/runtime failures for duplicate provider registrations and configured DTO types without registered providers.
- Kept `CustomSpawnsAiBehaviour` independent of concrete patrol/raid/siege DTO/provider types and preserved dominant score-sink behavior.
- Added generic `SyncData` delegation exactly once per distinct provider/fallback instance.
- Changed patrol to consume the typed patrol DTO passed by the resolver instead of performing its own `SpawnDao` lookup.
- Persisted patrol target, uninterrupted days, and rolled stable days in a saveable `PatrolState`; synchronized the party-state dictionary with key `_customSpawnsPatrolAroundClosestStates`; safely initializes a null loaded dictionary.
- Registered `PatrolState` as class definition ID 3 and its dictionary container while leaving class definition IDs 1 and 2 unchanged.
- Wired Main with patrol provider and home fallback only; raid/siege registrations remain for later tasks.

## Files

- `CustomSpawns/AI/PartyBehaviourResolver.cs` (new)
- `CustomSpawns/AI/IPriorityPartyBehaviour.cs`
- `CustomSpawns/AI/CustomSpawnsAiBehaviour.cs`
- `CustomSpawns/AI/HomeSettlementPatrolBehaviour.cs`
- `CustomSpawns/AI/PatrolAroundClosestLestInterruptedAndSwitchBehaviour.cs`
- `CustomSpawns/Main.cs`
- `CustomSpawns/Spawn/PartyComponents/CustomSpawnsTypeDefiner.cs`
- `CustomSpawns.Tests/AI/PartyBehaviourResolverTests.cs` (new)
- `CustomSpawns.Tests/AI/CustomSpawnsAiBehaviourTests.cs`
- `CustomSpawns.Tests/AI/PatrolAroundClosestLestInterruptedAndSwitchBehaviourTests.cs`

## Verification commands and output

1. `rtk dotnet build CustomSpawns.sln --configuration Release -v:minimal --artifacts-path "C:\Users\Joe\.codex\visualizations\2026\08\09\019fe867-dfd0-7e61-8031-07a6a8679ee7\customspawns-task2"`
   - Exit code: 0
   - Output: `ok dotnet build: 3 projects, 0 errors, 41 warnings (00:00:02.89)`
   - Warnings were existing nullable warnings plus unset Bannerlord game-directory/copy warnings.

2. `rtk proxy dotnet test CustomSpawns.Tests\CustomSpawns.Tests.csproj --configuration Release --no-build --artifacts-path "C:\Users\Joe\.codex\visualizations\2026\08\09\019fe867-dfd0-7e61-8031-07a6a8679ee7\customspawns-task2" --filter "FullyQualifiedName~CustomSpawns.Tests.AI.PartyBehaviourResolverTests|FullyQualifiedName~CustomSpawns.Tests.AI.CustomSpawnsAiBehaviourTests|FullyQualifiedName~CustomSpawns.Tests.AI.PatrolAroundClosestLestInterruptedAndSwitchBehaviourTests" --logger "console;verbosity=normal"`
   - Exit code: 0
   - Test files matched: 1
   - Discovered: 0
   - Passed: 0
   - Adapter output: `NUnit couldn't find any tests` / `No test matches the given testcase filter`.

3. `rtk proxy dotnet test CustomSpawns.Tests\CustomSpawns.Tests.csproj --configuration Release --no-build --artifacts-path "C:\Users\Joe\.codex\visualizations\2026\08\09\019fe867-dfd0-7e61-8031-07a6a8679ee7\customspawns-task2" --list-tests --logger "console;verbosity=detailed"`
   - Exit code: 0
   - Discovered: 0
   - Adapter output: `NUnit couldn't find any tests`.

4. `rtk proxy ilspycmd -l c "C:\Users\Joe\.codex\visualizations\2026\08\09\019fe867-dfd0-7e61-8031-07a6a8679ee7\customspawns-task2\bin\CustomSpawns.Tests\release\CustomSpawns.Tests.dll"`
   - Exit code: 0
   - Confirmed compiled classes include `CustomSpawns.Tests.AI.CustomSpawnsAiBehaviourTests`, `CustomSpawns.Tests.AI.PartyBehaviourResolverTests`, and `CustomSpawns.Tests.AI.PatrolAroundClosestLestInterruptedAndSwitchBehaviourTests`.

5. `rtk git diff --check`
   - Exit code: 0
   - Output: empty.

6. `rtk git status --short` after commit
   - Only unrelated pre-existing untracked paths remain: `CustomSpawns/FodyWeavers.xsd`, `Logs/`, `ModuleData/`, and `SubModule.xml`.

## Commit

`97f05ea` — `Add per-spawn party behaviour resolver`

## Self-review

- Verified campaign behavior only consumes `PartyBehaviourResolver` and does not name concrete configurations/providers.
- Verified per-spawn order is read at resolution time rather than provider registration order.
- Verified `NotApplicable` continues, applicable results stop, and fallback executes only after missing/exhausted configuration.
- Verified unknown configured DTOs throw instead of silently disappearing.
- Verified provider state synchronization is generic and deduplicated by provider reference.
- Verified loaded patrol state retains its rolled stable duration and null loaded dictionaries are repaired.
- Verified saveable field IDs 1/2/3 and class definition ID 3 do not change existing IDs.
- Verified only Task 2 files were committed; protected unrelated untracked files were not staged.

## Concerns

- Focused tests could not be executed because NUnit Adapter 6.0.0.0 discovered zero tests, even though the compiled DLL contains the three focused test fixture classes. This appears to be test-host/adapter infrastructure rather than compilation, but the controller should rerun the focused filters in its normal test environment and record actual discovered/passed counts.
- This intermediate Task 2 wiring intentionally registers patrol only. Configured raid/siege DTOs now fail clearly until Tasks 3 and 4 register their providers, as required by the clarified brief.
