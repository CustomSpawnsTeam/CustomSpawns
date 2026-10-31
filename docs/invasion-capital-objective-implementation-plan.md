# Invasion capital objective implementation plan

Status: historical implementation plan. The current behavior is documented in
[Losing the foothold](invasion-logistics.md#losing-the-foothold). Later changes introduced an explicit
mobilizing phase, nearby castle-village gathering regardless of ownership, and an indefinite relief
wait while the capital remains besieged. Those rules supersede the original defence timeout and
friendly-fortification assumptions below. Daily recovery also remobilizes before checking the
assembled force; a saved siege deadline is inert once the objective is besieging. The loss scenarios
below remain verification cases; a successful live playtest must be recorded separately.

## Problem

An invasion kingdom needs a temporary kingdom-wide objective when its capital is threatened:

- When its capital is besieged, gather a relief army and defend it.
- When its capital falls, gather an army and retake it.
- Do not march on a force the assembled army cannot defeat.
- Do not wait for every summoned party once the force already assembled is sufficient.
- After three days without sufficient strength, seek the closest viable replacement capital that
  satisfies the original invasion doctrine and the configured siege requirements.
- If no settlement is viable, release the kingdom to vanilla AI and reconsider once per day while
  the invasion owns no capital.
- If an attack objective does not start a siege within seven days, release the kingdom to vanilla AI
  for that objective episode.

This addresses the deadlock in which the last invasion army moves towards a captured capital, the
former besieger disbands inside it, and neither side can move or attack because the new garrison and
occupying lords are too strong. Gathering away from the target gives the former besiegers room to
leave. The three-day retarget and seven-day attack timeout bound any new deadlock.

This feature is separate from the existing per-party invasion gathering described in
`docs/invasion-gathering-proposal.md`. Ordinary invasion siege behavior may still maintain several
party objectives. A capital objective is deliberately one invasion-level objective with one native
army commander.

## Required behavior

### Capital defence

1. When an invasion-owned capital gains a hostile `SiegeEvent`, create a `Defence/Gathering`
   objective against that capital.
2. Select one commander and use Bannerlord's native army gathering. Native gathering chooses a
   friendly, non-besieged fortification behind the objective, which supplies the desired staging
   point roughly a few villages or one or two fortifications away.
3. Compare the force that has actually assembled with the forces at and near the capital.
4. If it is sufficient before three days expire, stop waiting for unarrived parties and advance at
   once.
5. If it is still insufficient at three days, find the closest immediately viable replacement
   capital using the original invasion requirements.
6. If no replacement is viable, clear the forced objective and let vanilla AI act.
7. If the hostile siege ends while the invasion still owns the capital, complete the defence
   objective.

Defence strength is:

```text
friendly = assembled army
         + capital garrison
         + active allied combatant lords inside the capital

enemy    = besieger camp
         + additional hostile combat parties near the capital

ready    = friendly >= enemy * MinimumAttackerToRoamingHostilesRatio
```

Militia is explicitly excluded. It does not sally with the relief force. Garrison, occupant, camp,
and nearby-party observations must be deduplicated.

### Capital attack

1. When the invasion loses its designated capital, replace any defence objective with an
   `Attack/Gathering` objective against the lost capital.
2. Give the new target a three-day gathering deadline and a per-target seven-day attack deadline.
3. If the assembled force becomes sufficient, advance immediately.
4. If it remains insufficient at three days, exclude the failed target and select the closest
   immediately viable alternative.
5. An alternative that already passed all strength gates starts in `Attack/Advancing`; it does not
   begin another gathering wait.
6. Once the invasion kingdom starts a matching siege at the target, mark the objective `Besieging`
   and clear the seven-day deadline.
7. If no matching siege starts within seven days of assigning that target, mark the objective
   `Dormant/AttackTimedOut` and let vanilla AI act.
8. If the objective target is captured and the invasion owns no existing capital, register it as
   the new capital and complete the objective.

Attack strength uses both configured XML ratios:

```text
settlement defenders = garrison
                      + militia
                      + active hostile combatant occupants

target ready  = assembled army >= settlement defenders * MinimumAttackerToTargetRatio
field ready   = assembled army >= nearby hostile field force
                                 * MinimumAttackerToRoamingHostilesRatio

ready         = target ready && field ready
```

Militia is included here because it defends the walls during a siege.

### No viable target and daily reconsideration

`Dormant/NoViableTarget` means the current numbers permit no valid conquest. The daily update may
reconsider only when all of these are true:

- the invasion owns no current capital;
- no gathering, advancing, or besieging capital objective is active;
- the dormant reason is `NoViableTarget`.

The daily evaluation rebuilds current strengths and candidates. If a candidate is now viable, it
starts directly in `Attack/Advancing` with a fresh seven-day deadline.

`Dormant/AttackTimedOut` is not restarted by the daily check. That distinction prevents the
seven-day timeout from becoming an hourly or daily abandon/reissue loop. A later ownership episode
or a new explicit capital event may create a new objective.

## Architecture

Most of the implementation belongs in plain domain classes. Domain code must have no `TaleWorlds.*`
references, XML models, save attributes, logger calls, or adapter enums.

```text
XML and Bannerlord state
        |
        v
Adapters: normalize configuration and observe the world
        |
        v
Plain domain settings, observations, candidates, and invasion state
        |
        v
Domain rules: strength, selection, commander choice, state transitions
        |
        v
Plain objective directive
        |
        v
Adapters: native army actions and AIBehaviorData
```

### Domain files

Add under `CustomSpawns/Invasions/CapitalObjectives/`:

- `InvasionCapitalObjective.cs`
- `InvasionCapitalObjectiveCoordinator.cs`
- `CapitalObjectiveStrengthRules.cs`
- `CapitalObjectiveCommanderSelector.cs`
- `CapitalObjectiveObservation.cs`
- `CapitalObjectiveDirective.cs`
- `InvasionAssaultRequirements.cs`
- `IInvasionAssaultConfigurationProvider.cs`
- `IInvasionCapitalObjectiveWorld.cs`

Add or extract under a non-adapter AI namespace, preferably `CustomSpawns/AI/Offensives/`:

- `SiegeTargetSelector.cs`
- `SiegeTargetFacts.cs`
- `SiegeTargetCriteria.cs`
- `SiegeTargetSelection.cs`

The exact number of files may be reduced where a type is private to one collaborator. Do not create
one-method wrapper classes or mapper interfaces that do not protect the domain from a framework
type. Public rule holders are sealed instance collaborators; do not add public static helpers.

### Adapter files

Add under `CustomSpawns/Invasions/Adapters/`:

- `BannerlordInvasionAssaultConfigurationProvider.cs`
- `BannerlordInvasionCapitalObjectiveWorld.cs`
- `BannerlordCapitalObjectiveArmy.cs`, only if native army mutation is too large to keep in the
  world adapter.

Retain separate thin orders:

- `InvasionCapitalDefenceOrder.cs`
- `InvasionCapitalAttackOrder.cs`
- `InvasionCapitalOrder.cs` for shared eligibility, target resolution, and navigation only.

`InvasionCampaignBehaviour` remains the campaign event and tick integration point. Prefer having it
invoke the coordinator instead of registering another campaign behavior with a captured `InvasionState`
reference, because `InvasionCampaignBehaviour` replaces `State` during save loading.

## XML configuration resolution

The capital objective must not parse XML. CustomSpawns already reads, adapts, validates, and caches
the effective behavior configuration before campaign AI evaluates it.

The existing load path is:

```text
PriorityPartyBehaviour XML
    -> PartyBehaviourDataReader
    -> PriorityPartyBehaviourConfig
    -> PartyBehaviourDtoAdapter
    -> BesiegeClosestSettlementConfigAdapter
    -> BesiegeClosestSettlementData
    -> IPartyBehaviourDao
```

`BesiegeClosestSettlementConfigAdapter` has already applied omitted-value defaults:

```text
MinimumAttackerToTargetRatio           1.5
MinimumAttackerToRoamingHostilesRatio  1.2
TargetTypes                            Town + Castle
```

An XML override is therefore already represented in `BesiegeClosestSettlementData`; capital code
must never repeat these constants.

### Runtime lookup

The source of truth is `Invasion.SpawnTemplateId`, not a commander's current party component. An
invasion lord can be rebuilt or adopted through a different code path and may no longer expose the
founding `CsPartyData`.

The lookup chain is:

```text
Invasion.SpawnTemplateId
    -> SpawnPartyBehaviourLookup.FindBehaviours(spawnTemplateId)
       -> ISpawnDao.FindByPartyTemplateId(spawnTemplateId)
       -> SpawnDto.PriorityPartyBehaviourTemplateId
       -> IPartyBehaviourDao.FindById(templateId)
       -> PartyBehaviourDto.PriorityPartyBehaviours
    -> OfType<BesiegeClosestSettlementData>()
```

The adapter chooses the first entry, in XML/template order, whose `TargetTypes` contains `Town`.
`InvasionRuntimeConfigurationValidator` already guarantees at least one such entry for an invasion.
If several qualify, use the first deterministically and log one warning; do not union target types or
merge ratios because that would invent new XML semantics. If none exists after a runtime data change,
return no requirements and release the objective to vanilla AI.

The same adapter reads the invasion doctrine from the `SpawnDto.Invasion` attached to that spawn:

- `RequiresFirstCoastalConquest`
- `SupplyLine`

It returns only a plain domain value:

```csharp
public sealed class InvasionAssaultRequirements
{
    public IReadOnlyList<SiegeTargetKind> TargetKinds { get; }
    public float MinimumTargetRatio { get; }
    public float MinimumRoamingRatio { get; }
    public bool RequiresOriginalCoastalTarget { get; }
    public InvasionSupplyLineKind SupplyLine { get; }
}
```

`SpawnSettlementType` currently lives in `Data.Adapter`; the domain must not reference it. Map it to
a new domain `SiegeTargetKind` without moving the existing enum as part of this change.

The domain-owned configuration port is:

```csharp
public interface IInvasionAssaultConfigurationProvider
{
    InvasionAssaultRequirements? Find(string spawnTemplateId);
}
```

## Domain state

`Invasion` owns one optional `InvasionCapitalObjective`. Adapters never set its properties directly.

```csharp
public enum InvasionCapitalObjectiveKind
{
    Defence = 0,
    Attack = 1
}

public enum InvasionCapitalObjectivePhase
{
    Gathering = 0,
    Advancing = 1,
    Besieging = 2,
    Dormant = 3
}

public enum InvasionCapitalObjectiveDormantReason
{
    None = 0,
    NoViableTarget = 1,
    AttackTimedOut = 2
}
```

```csharp
public sealed class InvasionCapitalObjective
{
    public InvasionCapitalObjectiveKind Kind { get; private set; }
    public InvasionCapitalObjectivePhase Phase { get; private set; }
    public string? TargetSettlementId { get; private set; }
    public double? GatherDeadlineDays { get; private set; }
    public double? AttackDeadlineDays { get; private set; }
    public InvasionCapitalObjectiveDormantReason DormantReason { get; private set; }

    public void MarkReady();
    public void RetargetForAttack(string settlementId, double nowDays);
    public void MarkBesieging();
    public void WaitForViableTarget();
    public void TimeOutAttack();
}
```

Construction should use named creation methods or constructors that establish valid combinations:

- Defence has a target and gather deadline but no attack deadline.
- Attack gathering has a target and both deadlines.
- Advancing has a target and no gather deadline.
- Besieging is valid only for Attack and has no timeout deadline.
- Dormant has no target or deadline and must have a non-`None` dormant reason.

`Invasion` exposes aggregate operations such as:

```csharp
public void BeginCapitalDefence(string settlementId, double nowDays);
public void BeginCapitalAttack(string settlementId, double nowDays);
public void AdvanceCapitalObjective();
public void RetargetCapitalAttack(string settlementId, double nowDays);
public void MarkCapitalSiegeStarted();
public void WaitForViableCapitalTarget();
public void TimeOutCapitalAttack();
public void CompleteCapitalObjective();
```

The three- and seven-day durations belong to the domain, alongside the transition rules they bound.
They are not XML options unless a later requirement explicitly makes them configurable.

## Shared domain siege selector

The ordinary siege behavior and capital fallback should share one pure selector. The adapter builds
facts; the selector decides eligibility, ratios, and ordering.

```csharp
public readonly struct SiegeTargetFacts
{
    public string SettlementId { get; }
    public SiegeTargetKind Kind { get; }
    public bool IsActive { get; }
    public bool IsHostile { get; }
    public bool HasCompatibleOrJoinableSiege { get; }
    public bool MatchesStrategicRequirement { get; }
    public bool IsReachable { get; }
    public float Distance { get; }
    public float DefenderStrength { get; }
    public float NearbyHostileStrength { get; }
}
```

```csharp
public readonly struct SiegeTargetCriteria
{
    public IReadOnlyList<SiegeTargetKind> AllowedKinds { get; }
    public string? ExcludedSettlementId { get; }
    public float AttackerStrength { get; }
    public float MinimumTargetRatio { get; }
    public float MinimumRoamingRatio { get; }
    public SiegeTargetSelectionMode Mode { get; }
}
```

```csharp
public enum SiegeTargetSelectionMode
{
    GatherBeforeAssault = 0,
    RequireViableNow = 1
}
```

`SiegeTargetSelector.FindClosest` applies:

1. active and hostile;
2. allowed settlement type;
3. not the excluded target;
4. compatible or joinable siege;
5. original strategic/coastal requirement;
6. reachable path;
7. target strength ratio when mode is `RequireViableNow`;
8. nearby-hostile ratio when mode is `RequireViableNow`;
9. smallest stable distance.

`GatherBeforeAssault` deliberately skips both strength gates, preserving the current ordinary
invasion behavior that may select a hard target and gather before attacking. Capital fallback and
daily recovery use `RequireViableNow` because they may only issue an immediate attack against a
settlement the assembled army can presently take.

To avoid an expensive `MobileParty.All` sweep for obviously invalid candidates, the selector may
expose a first structural/target-strength assessment followed by final selection after the adapter
has populated nearby-hostile strength. Both decisions remain domain methods; the optimization must
not move eligibility rules back into the adapter.

### Coastal doctrine

Do not reuse `GetInitialCoastalTargetFilter` unchanged for fallback. It currently first forces the
lost historical capital and otherwise disables the coastal filter once the invasion owns a supply
base. Both conflict with selecting a replacement capital under the original doctrine.

For a fallback target, derive `MatchesStrategicRequirement` from the original configuration:

- when `RequiresFirstCoastalConquest` is true and the configured supply line depends on a base, the
  candidate must satisfy the existing `InvasionSupplyBases.IsSupplyBase` classification;
- a coastal supply base remains the existing port-or-campaign-sea-adjacency definition;
- when the original invasion did not require that first coastal conquest, do not add a new coastal
  restriction.

The coastal and path helpers remain Bannerlord adapters. The fact that their result is required is
a domain criterion.

## Domain force assessment

Add a sealed `CapitalObjectiveStrengthRules` collaborator with instance methods. It receives only
plain strength snapshots.

```csharp
public readonly struct CapitalDefenceForces
{
    public float AssembledFieldStrength { get; }
    public float GarrisonAndOccupantStrength { get; }
    public float BesiegerAndNearbyHostileStrength { get; }
}

public readonly struct CapitalAttackForces
{
    public float AssembledFieldStrength { get; }
    public float SettlementDefenderStrength { get; }
    public float NearbyHostileStrength { get; }
}
```

```csharp
public sealed class CapitalObjectiveStrengthRules
{
    public bool CanDefend(CapitalDefenceForces forces, float minimumRoamingRatio);

    public bool CanAttack(CapitalAttackForces forces, float minimumTargetRatio,
        float minimumRoamingRatio);
}
```

The ratio primitive should be shared with `OffensiveStrengthRules` or extracted from it so target
selection, ordinary sieges, and capital objectives cannot disagree about zero-strength handling.
Do not create a public static utility.

### What counts as assembled

Use only the army leader and currently attached parties:

```text
Army.CalculateCurrentStrength()
Army.TotalManCount
Army.TotalHealthyMembers
```

Do not use `Army.Parties.Sum(...)` for readiness. `Army.Parties` includes called parties still
travelling to the army, which would make the force advance on paper strength that has not arrived.
A commander without an army contributes its own party and legitimate attached escorts.

## Domain coordinator

`InvasionCapitalObjectiveCoordinator` owns the transition sequence. It is invoked hourly for active
objectives and daily for dormant recovery.

The coordinator consumes:

- the `Invasion` aggregate;
- `InvasionAssaultRequirements`;
- a plain `CapitalObjectiveObservation`;
- plain commander candidates;
- plain `SiegeTargetFacts` when retargeting is required.

The observation contains current time, ownership, siege status, matching-kingdom-siege status,
assembled force data, and the defence/attack strength snapshots. It contains no `Settlement`,
`MobileParty`, `Army`, `Kingdom`, or `CampaignTime`.

### Hourly transition order

Apply transitions in this order so ownership changes override stale defence state:

1. If the designated capital is no longer owned, begin or continue Attack against the lost capital.
2. Otherwise, if the owned capital is besieged, begin or continue Defence.
3. If Defence is active and the siege has ended while ownership remains, complete it.
4. If Gathering and the relevant strength rule passes, mark the objective Advancing immediately.
5. If Gathering reaches its three-day deadline while insufficient, select the closest viable
   alternative with `RequireViableNow`, excluding the failed target.
6. If an alternative exists, retarget to `Attack/Advancing` and reset the per-target seven-day
   deadline.
7. If none exists, enter `Dormant/NoViableTarget`.
8. If an Attack/Advancing target has a matching invasion-kingdom siege, mark it Besieging.
9. If an Attack/Advancing target reaches its seven-day deadline first, enter
   `Dormant/AttackTimedOut`.
10. If a Besieging target is captured, register it as capital where appropriate and complete the
    objective.

### Daily transition

Only `Dormant/NoViableTarget` is reconsidered. The coordinator first verifies that the invasion
owns no current capital. It then selects with fresh force and target facts. A viable candidate starts
`Attack/Advancing` because it has already passed both ratios.

## World observation port and adapter

The domain owns method-based ports; plain structs carry data.

```csharp
public interface IInvasionCapitalObjectiveWorld
{
    CapitalObjectiveObservation Observe(string invasionId, string targetSettlementId,
        string? commanderPartyId);

    IReadOnlyList<CapitalObjectiveCommanderCandidate> FindCommanderCandidates(string invasionId);

    IReadOnlyList<SiegeTargetFacts> FindTargetFacts(string invasionId,
        string commanderPartyId, InvasionAssaultRequirements requirements,
        string? excludedSettlementId);

    void BeginGathering(string commanderPartyId, string targetSettlementId);

    void BeginAdvance(string commanderPartyId);
}
```

The final interface may be split by the coordinator and order-query consumers if one interface
becomes a mixed read/write surface. It must not expose framework types or property bags.

`BannerlordInvasionCapitalObjectiveWorld` performs:

- string ID to Bannerlord object resolution;
- army, party, settlement, ownership, and siege observation;
- current-strength calculations;
- garrison, militia, occupant, camp, and nearby-party classification;
- deduplication of camp and nearby forces;
- coastal classification;
- land/naval reachability checks;
- distance calculation;
- native army creation, invitations, and release of unarrived parties;
- selected settlement ID back to `Settlement` resolution where execution requires it.

The adapter supplies facts such as `IsHostile`, `IsReachable`, and
`HasCompatibleOrJoinableSiege`; it does not decide whether those facts are sufficient.

An existing siege is joinable for the capital objective when it is run by the invasion kingdom or
the forced objective army. Do not restrict this to parties that still carry the original
`CustomSpawnsPartyComponent`.

## Commander and native army

The capital objective needs one large army rather than one gathering intent per lord.

The adapter describes eligible parties with a plain snapshot. A pure
`CapitalObjectiveCommanderSelector` chooses:

1. the strongest eligible existing invasion-kingdom army leader;
2. otherwise, the strongest eligible invasion lord capable of forming an army.

Do not persist a `MobileParty`; persist or expose only its string ID while the objective is active.
Re-select if the commander disappears, is imprisoned, joins an incompatible army, or can no longer
lead the objective.

Only the selected commander receives the dominant capital order. Native `AIBehaviorData` with
`WillGatherArmy = true` creates or reuses an army and summons members. The existing
`InvasionArmyManagementModel` remains responsible for the influence eligibility lift and zero
invasion army membership/cohesion costs.

### Advancing before all summoned parties arrive

Changing `WillGatherArmy` to false is not sufficient by itself. Native
`Army.IsWaitingForArmyMembers` continues to consider called-but-unattached parties, and native siege
or ownership callbacks can send the army back to its gathering settlement.

When the domain transitions to Advancing, the Bannerlord adapter must remove still-unattached called
parties from that army's invitation roster while preserving the leader and attached army. Do not use
reflection to alter Bannerlord's private gathering timer. This makes the native waiting ratio equal
to the force that actually advances and implements the rule: enough force moves now and does not wait
for the rest.

## Thin capital orders

Replace `IInvasionCapitalRetakeQuery` with a query returning a plain directive:

```csharp
public readonly struct InvasionCapitalObjectiveDirective
{
    public string KingdomId { get; }
    public string CommanderPartyId { get; }
    public string TargetSettlementId { get; }
    public InvasionCapitalObjectiveKind Kind { get; }
    public InvasionCapitalObjectivePhase Phase { get; }
}
```

```csharp
public interface IInvasionCapitalObjectiveQuery
{
    InvasionCapitalObjectiveDirective? FindForKingdom(string kingdomId);
}
```

`InvasionCapitalOrder` should contain only:

- common party eligibility;
- commander ID matching;
- target settlement resolution;
- navigation calculation;
- construction of `AIBehaviorData` from the derived order's behavior.

The derived orders map domain intent:

```text
Defence/Gathering -> DefendSettlement,  WillGatherArmy = true
Defence/Advancing -> DefendSettlement,  WillGatherArmy = false
Attack/Gathering  -> BesiegeSettlement, WillGatherArmy = true
Attack/Advancing  -> BesiegeSettlement, WillGatherArmy = false
Dormant           -> no capital order
Besieging         -> no dominant capital order; native siege behavior owns execution
```

Orders no longer read `InvasionCapitalRepository` independently. The objective is the single source
of truth for target, kind, phase, and commander. Defence and attack remain separate classes and stay
in the existing composite order.

## Capital registration and relocation

Current code deliberately freezes the first capital through `CapitalSettlementId ??=` and
`InvasionCapitalRepository.Register`. Add a narrowly named operation such as:

```csharp
public void RelocateCapitalAfterObjectiveCapture(string invasionId, string settlementId);
```

Call it only when the captured settlement equals the active replacement-capital objective. It must
update `Invasion.CapitalSettlementId` and the capital repository consistently.

If the original capital is still owned when an alternative is captured, preserve the original
capital and complete the temporary objective rather than silently relocating a functioning capital.
The replacement becomes capital only when the invasion currently owns no designated capital.

## Save compatibility

Domain types contain no TaleWorlds save attributes. Boundary DTOs map them.

Keep `[SaveableField(12)] CapitalRetakeDeadlineDays` as a legacy migration field so saves created by
the partial implementation remain readable. Stop using it as active state and write its empty
sentinel for new saves.

Add to `InvasionSaveData`:

```csharp
[SaveableField(13)]
public InvasionCapitalObjectiveSaveData? CapitalObjective;
```

The new save DTO contains:

```text
[1] int Kind
[2] int Phase
[3] string TargetSettlementId       empty means null
[4] double GatherDeadlineDays       -1 means null
[5] double AttackDeadlineDays       -1 means null
[6] int DormantReason
[7] string CommanderPartyId         empty means unresolved/reselect
```

Register the next unused saveable type-definer ID. `FromState` deep-copies the objective. `ToState`
validates enum values and state invariants before restoration; invalid objective data is discarded
and vanilla AI remains available.

For a save containing only legacy field 12, restore an `Attack/Advancing` objective against the
historical capital with field 12 as its attack deadline. Do not invent a three-day deadline that the
old save never recorded.

## Logging

Logging remains at the coordinator/adapter boundary, never in pure rule classes. Log state
transitions and the once-daily dormant evaluation, not every hourly score calculation.

Each line should include:

- invasion spawn template and kingdom;
- previous and next kind, phase, and dormant reason;
- commander, army, and target;
- leader troops, healthy troops, strength, and party size limit;
- attached party count, called party count, assembled troops, assembled strength, and summed
  attached-party limits;
- called-but-unattached troops and strength as diagnostics only;
- garrison troops, strength, and party limit;
- militia strength and whether it was excluded from this calculation;
- friendly occupant strength;
- besieger camp strength and additional nearby hostile strength;
- configured target and roaming ratios;
- calculated ratios;
- gather and attack deadlines;
- decision: gather, advance, retarget, abandon-no-target, abandon-timeout, siege-started, or complete.

This logging must cover native or rebuilt invasion lords that do not carry
`CustomSpawnsPartyComponent`; the existing `AiDecisionLoggerBehaviour` alone cannot do that.

## Existing behavior to refactor carefully

### `BannerlordBesiegeClosestSettlementAdapter`

Extract only the domain decisions required for shared target selection. The adapter remains
responsible for observing Bannerlord state. Reuse or move its existing defender-strength and
nearby-hostile observation so capital code does not implement a second formula.

The ordinary `BesiegeClosestSettlementBehaviour` should continue passing its existing configuration
and use selector mode `GatherBeforeAssault`. This preserves its current ability to commit to a hard
target before the full force arrives.

### `InvasionCoastalSiegeTargetPolicy`

Keep ordinary landing behavior intact. Remove the lost-capital hard filter only after the new capital
objective owns lost-capital targeting. The fallback selector must apply original doctrine through
plain target facts rather than asking the stateful initial-landing filter.

### `InvasionState.RequiresConfiguredPartySteering`

Do not switch all invasion lord parties back to the existing configured per-party behavior during a
capital objective. The kingdom objective directs one native army commander. Other lords remain under
native army invitation and vanilla steering unless they join that army.

### Partial retake implementation

Replace rather than extend:

- `IInvasionCapitalRetakeQuery`;
- `CapitalRetakeDeadlineDays` as live state;
- always-true `WillGatherArmy` on both capital orders;
- attack targeting only the historical capital;
- owner-change-only activation.

Retain field 12 solely for save migration.

## Ordered implementation

### Step 1: pin current behavior with tests

Add tests for the current XML lookup, existing siege candidate semantics, capital order eligibility,
and save contract before restructuring.

Verify:

- the effective behavior lookup returns XML overrides and omitted-value defaults;
- the existing ordinary invasion selector may choose a target before it is strong enough;
- current save field IDs are unchanged.

### Step 2: extract the shared domain selector

Introduce domain target kinds, facts, criteria, selection mode, and `SiegeTargetSelector`. Refactor
`BannerlordBesiegeClosestSettlementAdapter.FindClosestSiegeTarget` to build facts and delegate the
decision.

Verify:

- ordinary siege tests remain green in `GatherBeforeAssault` mode;
- `RequireViableNow` rejects insufficient target and roaming ratios;
- excluded, unreachable, wrong-type, friendly, inactive, and incompatible-siege candidates are
  rejected;
- closest viable candidate wins.

### Step 3: add configuration provider

Implement the spawn-template-to-effective-siege-configuration lookup and map it to
`InvasionAssaultRequirements`.

Verify:

- DADG-style explicit ratios are returned unchanged;
- omitted ratios produce the already-adapted 1.5/1.2 values;
- the first town-capable behavior wins deterministically;
- multiple qualifying entries log one warning;
- missing runtime configuration releases the objective instead of throwing during AI evaluation.

### Step 4: add objective aggregate and strength rules

Add the pure objective state, invariants, clocks, force snapshots, and readiness rules. Attach the
objective to `Invasion` through aggregate methods.

Verify:

- defence excludes militia by construction;
- defence includes supplied garrison and occupant strength;
- attack includes supplied militia in settlement defenders;
- attack requires both configured ratios;
- every invalid state combination is rejected or impossible to construct.

### Step 5: add commander selection and coordinator

Add pure commander selection and the hourly/daily transition sequence.

Verify:

- one strongest existing army leader is preferred;
- otherwise one eligible lord is selected;
- readiness before three days advances immediately;
- exact three-day expiry retargets;
- no alternative enters `NoViableTarget`;
- daily recovery runs only for `NoViableTarget` with no owned capital;
- a matching siege before seven days enters Besieging;
- exact seven-day expiry enters `AttackTimedOut`;
- `AttackTimedOut` does not daily restart;
- capital loss converts Defence into Attack with fresh clocks.

### Step 6: implement Bannerlord world adapter

Build observations, target facts, and native army operations. Reuse existing coastal, navigation,
strength, and siege helpers where their semantics match.

Verify with adapter tests:

- only attached parties count as assembled;
- called-but-unattached parties are diagnostic only;
- garrison/occupants/camp/nearby forces are deduplicated;
- defence excludes militia and attack includes it;
- fallback coastal doctrine comes from original invasion configuration;
- every fallback candidate requires a reachable route;
- invasion-kingdom sieges are joinable even for rebuilt/native lord parties.

### Step 7: replace capital order queries

Expose the directive query and make attack/defence orders thin translations. Update the composite
registration in `Main`.

Verify:

- only the selected commander receives the capital order;
- Gathering maps to `WillGatherArmy = true`;
- Advancing maps to `WillGatherArmy = false`;
- Dormant and Besieging do not override vanilla AI;
- defence and attack remain mutually exclusive.

### Step 8: integrate hourly/daily campaign updates

Have `InvasionCampaignBehaviour` invoke the coordinator. Reconcile objective state after loading and
after settlement owner changes; use hourly observation for siege start/end because ownership events
alone do not describe the full objective lifecycle.

Verify:

- loaded state uses the current `InvasionState` instance;
- a newly besieged capital creates Defence;
- a fallen capital creates Attack;
- an ended defence siege clears the objective;
- siege start and target capture are observed without duplicate transitions.

### Step 9: native early advance

When the coordinator transitions to Advancing, remove unarrived invitations and preserve the
assembled army.

Verify:

- the commander stops waiting immediately when ready;
- attached parties remain in the army;
- parties still travelling are released to vanilla behavior;
- native callbacks do not send the advancing army back to gather.

### Step 10: persistence and migration

Add the objective save DTO, type registration, field 13 mapping, and legacy field 12 migration.

Verify:

- every objective phase/reason/deadline round-trips;
- commander loss after load causes re-selection, not a broken objective;
- legacy retake saves load as an advancing attack;
- invalid save data safely drops the objective;
- existing save field and type IDs are unchanged.

### Step 11: logging

Add transition and daily-recheck logging with troop, strength, party-limit, garrison, militia,
enemy, ratio, deadline, and decision fields.

Verify:

- native/rebuilt invasion lords appear in logs;
- unchanged hourly state does not spam the log;
- every abandon and retarget states why it happened.

### Step 12: remove superseded code

Delete only code made obsolete by the completed implementation: the retake boolean query, live use
of field 12, historical-capital-only attack filter, and unconditional gathering flags. Do not remove
unrelated pre-existing dead code.

Verify:

- no remaining reader treats `CapitalRetakeDeadlineDays` as active state;
- target truth comes only from `InvasionCapitalObjective`;
- ordinary per-party invasion gathering remains unchanged.

## Test matrix

Pure domain tests must cover:

- readiness one tick before and exactly at ratio thresholds;
- zero enemy strength handling;
- militia excluded from defence and included in attack input;
- target ratio passing while roaming ratio fails, and vice versa;
- early advance before three days;
- exact three-day insufficient transition;
- closest viable alternative with original target types;
- coastal alternative requirement;
- original target exclusion after failed gathering;
- no candidate to `NoViableTarget`;
- daily recovery gates;
- exact seven-day timeout;
- matching siege completion of the deadline;
- no timeout reissue loop;
- defence siege ending;
- defence converting to attack on ownership loss;
- replacement capital registration;
- commander selection and re-selection;
- objective state invariants;
- save round-trip and legacy migration.

Adapter tests must cover:

- XML lookup and mapping;
- multiple siege configuration handling;
- settlement type mapping;
- path and coastal facts;
- compatible and kingdom-joinable siege facts;
- attached versus called army membership;
- strength component classification and deduplication;
- attack/defence directive mapping;
- release of unarrived invitations;
- transition logging fields.

## Build and playtest verification

Run the supported CustomSpawns verification commands:

```powershell
dotnet test CustomSpawns.Tests/CustomSpawns.Tests.csproj `
  -p:BuildForWindows=false -p:BuildForWindowsStore=false

dotnet build CustomSpawns/CustomSpawns.csproj `
  -p:BuildForWindows=false -p:BuildForWindowsStore=false
```

Then build the consuming DADG solution so the DLL used by `Launcher.Standalone` is refreshed.

Campaign playtest scenarios:

1. Besieger weaker than assembled army plus garrison: the commander advances immediately without
   waiting for every invited lord.
2. Besieger stronger than current relief force: the army gathers at a nearby safe fortification.
3. Defence remains insufficient for three days: the objective selects a valid replacement capital.
4. Lost capital is immediately retakeable: the gathered army advances and establishes a siege.
5. Lost capital is too strong but another configured target is viable: the army attacks the closest
   valid alternative.
6. Coastal-first invasion: no inland alternative is selected.
7. No viable target: capital override ends, vanilla resumes, and daily logging reports the failed
   evaluation without hourly spam.
8. Numbers later change: the daily check starts a new attack.
9. Target is not besieged within seven days: the attack override ends and does not immediately
   restart.
10. Alternative is captured while no capital is held: it becomes the new invasion capital.

The playtest log must make each decision explainable from current troops, strength, party limits,
garrison, militia treatment, enemies, configured ratios, and elapsed deadlines.

## Completion criteria

The implementation is complete when:

- the domain owns all timing, readiness, selection, and transition rules;
- no domain type references `TaleWorlds.*`, XML models, logging, or save attributes;
- XML overrides and existing defaults are consumed through the effective behavior configuration;
- exactly one native army commander is forced for a capital objective;
- sufficient assembled force advances without waiting for unarrived invitations;
- defence excludes militia and includes the garrison;
- attack includes militia and applies both configured ratios;
- three-day retarget, no-target daily recovery, and seven-day timeout behave as specified;
- replacement targets preserve original target type and coastal doctrine;
- ordinary invasion gathering behavior is not regressed;
- save compatibility and migration tests pass;
- targeted tests and the CustomSpawns build pass;
- the deadlock scenario is reproduced before the change and no longer reproduces after it.
