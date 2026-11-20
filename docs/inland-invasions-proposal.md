# Inland invasions: revolts and overland incursions

A design proposal, kept as the record of how the supply line was arrived at. The supply line it
proposes was implemented -- as `Coastal`, `Any` and `None` -- but much of the rest of the document
has since been overtaken: reinforcements no longer land as parties of their own, waves are paid into
the capital's garrison instead, supplies no longer decide whether reinforcements come, and the
per-rule concurrent-party setting it quotes no longer exists. Its code and line references therefore
describe the branch as it was before that redesign, not as it is now; `invasion-logistics.md`
describes what actually ships.

The request: *"propose a way to spawn invasions that aren't coastal, like revolts that can take a
town."*

Revised after review. The direction is now: a supply line that is **Coastal**, **Free** or **None**;
targets that may be **towns**, **castles** or both; and the behaviour carried by a **strategy object**
rather than by conditionals at each call site. Section 3 is rewritten to that. The two parts of the
direction land very differently -- the supply line is a clean strategy, castle targets are not a
strategy value at all -- and 3.0 says why before the designs.

The short answer is that the invasion feature is far less coastal than its vocabulary suggests.
Spawn placement has no water predicate at all, and the capture / foothold / occupation / kingdom
ledger has none either. "Coastal" lives in exactly three hard gates and one configuration flag, all
of them on the *logistics* side -- where reinforcements may arrive and which held town gets fed.
An inland invasion is therefore mostly a question of what replaces the sea lane, not of how the
first party gets onto the map.

---

## 1. Where "coastal" is actually baked in

### 1.1 Hard requirements in code (three, all in logistics)

**(a) The reinforcement clocks only wind on a coastal capture.**
`CustomSpawns/Invasions/InvasionReinforcements.cs:123-124`

```csharp
IReadOnlyList<InvasionReinforcementRule>? rules = _invasionReinforcementQuery.GetRules(group);
if (rules == null || !_invasionReinforcementQuery.IsCoastalTown(capture.SettlementId)) return null;
```

An invasion that takes only inland towns never winds a single clock, so it never receives a single
reinforcement. The port is `IInvasionReinforcementQuery.IsCoastalTown(string settlementId)`
(`InvasionReinforcements.cs:87`), implemented at
`Invasions/Adapters/InvasionReinforcementArrivals.cs:199-200`.

**(b) A landing site must be a coastal town.**
`Invasions/Adapters/InvasionReinforcementArrivals.cs:274-281`

```csharp
return foothold?.InvasionClanId == group.ClanId && settlement.IsTown &&
       group.HoldsClan(settlement.OwnerClan?.StringId) && settlement.SiegeEvent == null &&
       _invasionCoastalTownResolver.IsCoastalTown(settlement);
```

Feeds `GetLandingSites` (`:202-204`), and an empty list is
`InvasionReinforcementOutcome.NoLandingSite` (`InvasionReinforcements.cs:235-236`). Note that this
is the *only* reason the predicate is applied twice: (a) decides whether the invasion has a supply
line at all, (b) decides which of its towns a given arrival uses.

**(c) Ongoing food support requires a live coastal town.**
`Invasions/InvasionFoodSupportRules.cs:11` takes `isLiveCoastalTown`, and
`Invasions/Adapters/InvasionOccupations.cs:113-116` supplies it:

```csharp
configuration != null && _invasionCoastalTownResolver.IsCoastalTown(town.Settlement),
```

So an inland foothold starves: `FoodStockFloor` / `DailyFoodBonus` never apply to it.

### 1.2 Soft requirement: one configuration flag, and its hidden second job

`RequiresFirstCoastalConquest` (`Data/Model/InvasionConfig.cs:9-10`,
`Data/Dto/InvasionData.cs:19`) is read in exactly one place,
`InvasionReinforcementArrivals.FindInitialCoastalTargetFilter`. It returns `null` unless the flag is
set; it then restricts targets to supply bases until the invasion holds its capital, and to the
capital alone once that capital has been lost. It defaults to `false`.

That same filter being non-null is also what makes the group behave as one force:

- `Invasions/Adapters/InvasionCoastalSiegeTargetPolicy.cs:102-110` (`GetLandingForce`) treats the
  whole group as a single attacking force *only while the filter exists*;
- `:72-85` (`GetTargetDistance`) then sums gate distance across the whole group;
- `:90-100` (`GetAttackingForce`) is what garrison strength is weighed against;
- `:150-171` (`IsEligibleCoastalTarget`) requires the filter plus
  `_mapPathQuery.HasReachableLandGate`.

The coupling is real but no longer a problem, because the two now switch together in the right
direction. The mod only steers an invasion's parties while it does not hold its capital -- before
the first landing, or after losing it -- and both are exactly the moments the parties should gather
and pool strength. Once the capital is held, every party is on vanilla AI and this filter is never
consulted for it. A landlocked design still has to decide what its own equivalent of "the capital"
is, but it does not have to separate the two switches.

### 1.3 The classification machinery itself

- `Invasions/InvasionCoastalTownClassification.cs` -- pure: `InvasionCoastalFaceTerrain`,
  `IsMaritimeFace`, `IsSettlementAdjacentToSea` (`:43`, requires the settlement face to be
  `RuralArea`, then BFS for a maritime neighbour).
- `Invasions/Adapters/InvasionCoastalTownResolver.cs` -- `settlement.IsTown` (`:26`),
  `settlement.HasPort` shortcut (`:30`), else a 360-probe navmesh BFS with a cache; `ClearCache()`
  (`:49`) is called on load from `InvasionCampaignBehaviour.SyncData`.

### 1.4 Configuration defaults, not requirements

The shipped content is the reason the feature *reads* as seaborne, not the code. The only live
`<Invasion>` blocks are CalradiaAtWar's two
(`Modules/CalradiaAtWar/CustomSpawns/CustomDailySpawn.xml`, around lines 968 and 1014), and both
place their force with `overriden_spawn_culture_*` -- `Culture.khuzait`, `Culture.aserai`,
`Culture.vlandia`, `Culture.sturgia` -- **not** with a coastal settlement list. The Makkonian
invasion does not even set `RequiresFirstCoastalConquest`; only Humar does. Their spawn message
("have landed at [spawnPlace]") is flavour text over a culture-random settlement draw.

`docs/invasion-logistics.md:31` says no spawn in *this* repository carries an `Invasion` block,
which is still true; the blocks live in the submod.

### 1.5 What has no coastal predicate at all

- **Spawn placement.** `Spawn/SpawnBehaviour.cs:315-356` (`GetSpawnSettlement`) is the whole path:
  `OverridenSpawnSettlements` → `OverridenSpawnCultures` → hideouts. No water, port or terrain test
  anywhere. `Spawn/MobilePartySpawnFactory.cs:12-17` always places at
  `homeSettlement.GatePosition` with radius 10. `Utils/CampaignUtils.cs` has no maritime helper.
- **The capture ledger.** `Invasions/InvasionState.cs` -- the word "coastal" appears once, in the
  comment on `Schedules` (`:164`), and nowhere in logic.
- **`Invasions/Adapters/InvasionSieges.cs`** -- requires `IsTown` and `BySiege`; the only coastal
  thing is the log message text at `ObserveOwnerChange`.
- **`InvasionOccupations.ApplyReady` / `PromoteCapturer`** (`:44-107`), including the
  kingdom-founding branch at `:100-104`.
- **`InvasionKingdomRetirement`**, **`InvasionMembership`**, **`InvasionCaptureCorrelation`**.
- **`Spawn/SpawnGroupService.SpawnInvasionReinforcement`** (`:50-61`) already accepts any
  `Settlement`. An overland arrival needs a different *eligibility predicate*, not a different
  spawn path.

**Conclusion.** The reusable-as-is surface is everything except logistics: membership, captures,
footholds, occupation (minus its food gate), kingdom founding and retirement, lord removal, save
data, placement. One predicate and one flag are the whole coastal story.

---

## 2. What a landlocked invasion actually needs

### 2.1 Where the first party comes from

Nothing. This already works. An inland invasion is spawned by listing inland settlements in
`overriden_spawn_settlement_*` or by naming an inland culture in `overriden_spawn_culture_*`
(`Data/Model/Spawn.cs:132-203`, `SpawnBehaviour.cs:315-356`). No code change is required to put an
invasion force in the middle of the map.

A *revolt* is a narrower claim: the force must appear at a specific settlement chosen by that
settlement's condition (loyalty, security, culture mismatch), not at a fixed list or a random
member of a culture. That is the one genuinely new placement rule in this proposal.

### 2.2 Where reinforcements come from with no sea lane

This is the real work. Three candidate answers:

1. **Any held town is a supply base.** Reinforcements march out of a town the invasion holds. The
   fiction is local recruitment and sympathisers; mechanically it is the existing landing path with
   the coastal predicate dropped. Cost: one predicate.
2. **A configured home settlement is the supply base.** Reinforcements enter at a named border
   settlement and march to the front. Cost: a new config element, plus the arrivals no longer
   spawn where they are needed, which the existing `BesiegeClosestSettlement` behaviour then has to
   walk them across.
3. **A map-edge entry point.** Bannerlord has no map-edge concept usable here, and the existing
   factory places parties at `GatePosition`. This would need genuinely new placement code and is
   not worth it.

(1) is the minimum that works, and it is the only one that keeps the existing
`StartSchedule`/`GetLandingSites`/food triangle intact.

### 2.3 Which gate is which question

Worth separating, because the three hard gates are not three decisions:

| Gate | Question it answers | Asked through |
| --- | --- | --- |
| `InvasionReinforcements.cs:124` | May this capture start the supply clocks? | `IInvasionReinforcementQuery.IsCoastalTown` (domain port) |
| `IsEligibleLandingSettlement` (`:274-281`) | Which held town does this arrival use? | private adapter predicate |
| `InvasionOccupations.cs:114` | Which held town gets fed? | `_invasionCoastalTownResolver` directly |
| `FindInitialCoastalTargetFilter` | May this party march inland yet? | config flag, returned as a `Func<Settlement, bool>?` |
| `GetLandingForce` (`InvasionCoastalSiegeTargetPolicy.cs:102-110`) | Do these parties fight as one force? | **the same filter's nullness** |

The first three answer one question -- "is it on the sea" -- which is why one predicate replaces all
three. But each is asked through a *different door*, and that is the fact that decides the shape of
section 3: a strategy object plugged in today would need three insertion points, which is exactly the
scattering it is meant to prevent. Gates 4 and 5 are one expression serving two purposes, which is
tolerable only because both purposes now want the same answer (see 1.2).

---

## 3. Designs

### 3.0 Is a strategy object possible here?

Yes for the supply line, no for castle targets, and the honest answer to *"if that's not possible,
refactor"* is that a prerequisite refactor is required either way. Three findings, in order of how
much they constrain the work.

**(a) The strategy is small, because five gates are two questions.** Every coastal decision in the
feature reduces to:

```csharp
// Invasions/IInvasionSupplyLine.cs -- pure, no TaleWorlds
internal interface IInvasionSupplyLine
{
    // Serves gates 1, 2 and 3. The adapter resolves the facts; the rule reads them.
    bool IsSupplyBase(InvasionSettlementFacts settlement);

    // False for None: no clocks are ever wound, so nothing else need know.
    bool SuppliesReinforcements { get; }
}
```

Three implementations, each a handful of lines: `CoastalSupplyLine` (`facts.IsCoastal`),
`AnySupplyLine` (`facts.IsHeldFortification`), `NoSupplyLine` (`true`, no base to depend on). This fits the
existing hexagonal split rather than fighting it: `InvasionFoodSupportRules` and
`InvasionCoastalTownClassification` already take resolved facts as plain values and keep the
navmesh on the adapter side. A strategy taking `InvasionSettlementFacts` is the same move.

**(b) The three doors must become one door first.** Today the same question is asked three different
ways -- a domain port method, a private adapter predicate, and a direct resolver call (see the table
in 2.3). A strategy introduced over that has three insertion points and three branches, which is
the scattering the pattern is supposed to remove. So step one is a pure refactor with no behaviour
change: collapse all three onto one port member, `IsSupplyBase`. **This is the refactor the direction
calls for, and it is a rename plus two redirected call sites.**

**(c) `None` cannot ship until gate 5 is split, and that is a pre-existing bug.**
`RequiresFirstCoastalConquest` does two unrelated jobs: it restricts the first target, *and* the same
filter being non-null is what makes an invasion gather and pool its strength
(`InvasionCoastalSiegeTargetPolicy.cs:102-110`). An invasion with no first-conquest requirement --
which `Free` and `None` typically are -- therefore gets its force pooling silently switched off, and
its parties besiege one at a time and lose. This already affects any coastal invasion configured
with the flag off. It needs its own `<GatherBeforeFirstSiege>`, decided independently of supply line.

**On "switch on the fly".** Recommend doctrine **immutable per invasion**, resolved once from the
invasion's `SpawnTemplateId` at load -- which means nothing new is saved and no save migrates. The
behaviour that genuinely does change mid-campaign (an invasion becoming a kingdom, or losing its
last port, which drives the party-template transition noted at `InvasionMembership.cs:78`) is driven
by *facts the strategy queries*, not by the strategy changing. Fixed doctrine over changing facts
covers every case in scope; mutable doctrine would add a save field for no case anyone has named.

### Design A -- Supply line as a strategy with three values

**Config surface** -- one new element in `InvasionConfig`:

```xml
<Invasion>
    <SupplyLine>Any</SupplyLine>   <!-- Coastal (default) | Any | None -->
    <Supplies><FoodStockFloor>100</FoodStockFloor><DailyFoodBonus>5</DailyFoodBonus></Supplies>
    <Reinforcements>
        <Rule><Id>main</Id><PartyTemplateId>cs_rebel_levy</PartyTemplateId>
              <IntervalInDays>7</IntervalInDays><MaxConcurrentParties>3</MaxConcurrentParties></Rule>
    </Reinforcements>
</Invasion>
```

Default `Coastal`, so every existing configuration keeps its behaviour.

| Value | Clocks wind at | Landing sites | Fed settlements |
| --- | --- | --- | --- |
| `Coastal` | first coastal town held | held coastal towns | held coastal towns |
| `Any` | first fortification held | any held fortification | any held fortification |
| `None` | any capture | anything held | everything held |

`None` is an invasion with no logistics to lose. Everything it holds supplies it equally, so its
configured reinforcements keep arriving wherever it is, and it is never sent back to retake a base --
which is what switches off the force-take-capital restriction and the pooling that rides on it.

**Step 1 -- the enabling refactor (no behaviour change, independently verifiable): DONE.**

- `InvasionReinforcements.cs`: the port member is `IsSupplyBase(string settlementId)`; the class
  comment and `Schedules` (`InvasionState.cs:175`) carry supply vocabulary.
- `InvasionFoodSupportRules.IsEligibleForOngoingSupport`: the parameter is `isLiveSupplyBase`.
- `InvasionReinforcementArrivals.IsEligibleLandingSettlement` and
  `InvasionOccupations.IsEligibleForFood` route through `InvasionSupplyBases`;
  `InvasionOccupations` no longer depends on `InvasionCoastalTownResolver`, which is now reached
  from `InvasionSupplyBases` alone.
- The duplicate `settlement.IsTown` tests at the landing and first-conquest call sites are gone
  (commit `402514f`): what kind of settlement may be a base is the supply question's to answer.
- `InvasionReinforcementOutcome.NoLandingSite` keeps its name (saves and logs reference it).

**Step 2 -- the strategy: DONE (commit `e33515c`).**

- `Invasions/InvasionSupplyLines.cs` (new, one file rather than two): `InvasionSettlementFacts`
  (`IsTown`, `IsCastle`, `IsCoastalTown`), `IInvasionSupplyLine`, the three implementations, and the
  name lookup. `IsCoastalTown` is resolved lazily behind a `Func<bool>`: the landing scan asks it of
  every settlement on the map, and the sea test walks the navmesh, so `Any` and `None` must not pay
  for an answer they never read.
- Deviation: no `SuppliesReinforcements` member. `IsSupplyBase` answers all three logistics gates; the
  second member is `DependsOnASupplyBase`, which answers whether there is a base worth going back for.
- Deviation: the facts are `IsTown`/`IsCastle`/`IsCoastalTown`, not `IsCoastal`/`IsHeldFortification`.
  Being held is already tested by the callers, and the sea test only ever admits towns.
- `InvasionReinforcements`: the port member is `IsSupplyBase(Invasion, string)` -- doctrine is
  per-invasion, so the question needs to know which invasion is asking.
- `InvasionSupplyBases.IsSupplyBase(Settlement?, InvasionData?)` resolves the doctrine and builds the
  facts. It remains the only consumer of `InvasionCoastalTownResolver`.
- `Data/Model/InvasionConfig.cs`: `[XmlElement("SupplyLine")] public string? SupplyLine`, with an
  unknown value rejected as an `ArgumentException` in `InvasionConfigValidator`.
- `Data/Dto/InvasionData.cs` + `Data/Adapter/InvasionDtoAdapter.cs`: threaded through as a trailing
  optional parameter, so existing call sites are untouched.
- Assumption, not asked for: `Any` puts no contiguity requirement on the fortifications it feeds from.
- Follow-up (same step, after review): the second value is `Any`, not `Free`, and `None` no longer
  starves. Everything it holds is a supply base, so its configured reinforcements keep arriving, and
  `DependsOnASupplyBase` being false makes `FindInitialCoastalTargetFilter` return null whatever
  `RequiresFirstCoastalConquest` says -- which drops both the capital-only target restriction and the
  pooling in `InvasionCoastalSiegeTargetPolicy`.
- Still not doctrine-aware: `InvasionState.RequiresCustomSpawnsSteering` takes every party off vanilla
  AI when the capital is not held, with no configuration access (it is Bannerlord-free domain). Under
  `None` the steering still changes hands. Making it doctrine-aware needs a new port.

**Untouched:** placement, `InvasionState`, `InvasionSieges`, `InvasionCaptureCorrelation`,
`InvasionKingdomRetirement`, `InvasionMembership`, `InvasionLordRemoval`, `InvasionSaveData`,
`InvasionCoastalTownResolver`, `InvasionCoastalTownClassification`, `SpawnGroupService`, `Spawner`,
the AI offensive layer.

### Design A2 -- Castle targets (not a strategy value)

Castle targeting has to be called out separately, because putting it behind the supply-line strategy
would be misleading: **the strategy would never be asked.** A captured castle is dropped before any
invasion logic runs.

| Guard | Effect on a captured castle |
| --- | --- |
| `InvasionSieges.cs:41` (`town?.IsTown != true`) | no capture record is opened |
| `InvasionSieges.cs:121` (`!settlement.IsTown`) | no foothold, no ownership tracking |
| `InvasionCoastalTownResolver.cs:26` (`!settlement.IsTown`) | a coastal castle classifies as inland |
| `GetStrengthBudget` (`InvasionReinforcementArrivals.cs:233`, `fief.IsTown`) | held castles do not raise the strength ceiling |
| `InvasionOccupations` / food model | typed on `Town` -- a castle has a `Town` component, so this is fine as-is |

The AI layer already offers castles as siege candidates
(`BannerlordBesiegeClosestSettlementAdapter.cs:172,238`), so an invasion party *can* besiege and take
a castle today -- the result is simply invisible to the invasion ledger. Castle support is therefore
**four guards and one counter**, plus a decision about whether a castle may be the seat a kingdom is
founded on (`PromoteCapturer`, `SetInitialHomeSettlement` at `InvasionOccupations.cs:100-104`).

One strategic argument for it, which bears directly on the companion loyalty proposal: **castles
cannot rebel.** Vanilla's `RebellionsCampaignBehavior` only fires on towns. A castle foothold
sidesteps the revolt spiral that `docs/invasion-loyalty-proposal.md` exists to fight, at the cost of
far less prosperity and no port. An invasion allowed to take castles has a cheap, stable base.

If a target-kind setting is wanted it should be its own element -- `<Targets>Towns|Castles|Both</Targets>`,
read by the siege target policy -- and not a member of `IInvasionSupplyLine`, which answers a
logistics question, not a targeting one. Two small interfaces beat one that means two things.

### Design B -- Revolt (origin at a discontented settlement)

Design A with a different *origin rule*. A revolt's force appears at the settlement that is
revolting, and because `BesiegeClosestSettlement` picks the nearest valid town
(`AI/Offensives/BesiegeClosestSettlementBehaviour.cs`, validated at
`InvasionRuntimeConfigurationValidator.cs:94-99`), a party spawned at the gate of the town it hates
*already* besieges that town. The targeting comes free.

**Config surface** -- one new attribute family on the spawn, alongside the existing override
families (`Data/Model/Spawn.cs:132-203` already reads `overriden_spawn_*` as wildcard attributes,
so this fits the schema as it stands):

```xml
<RegularBanditDailySpawnData
 party_template = "PartyTemplate.cs_yorkist_rising"
 spawn_clan = "Faction.cs_yorkist_rebels"
 revolt_in_kingdom = "Faction.sturgia"
 revolt_max_loyalty = "35">
    <PriorityPartyBehaviourTemplateId>dadg-revolt</PriorityPartyBehaviourTemplateId>
    <Invasion>
        <SupplyLine>Overland</SupplyLine>
        ...
    </Invasion>
</RegularBanditDailySpawnData>
```

`revolt_in_kingdom` scopes which faction can be revolted against; `revolt_max_loyalty` is the
threshold below which a town qualifies. Placement picks the qualifying town with the lowest
loyalty, tie-broken by the existing random pick.

**Domain changes:**

- `Spawn/RevoltOriginRules.cs` (new, pure, ~15 lines): given a list of
  `(settlementId, loyalty)` and a threshold, return the eligible ids in ascending loyalty. Pure
  arithmetic belongs here rather than in `SpawnBehaviour`.

**Adapter changes:**

- `SpawnBehaviour.GetSpawnSettlement` (`:315-356`): one new branch *before* the existing
  settlement/culture branches, reading the revolt scope from the DTO and projecting
  `Town.Loyalty` into `RevoltOriginRules`. Returns `null` when nothing qualifies, which the existing
  loop already treats as "do not spawn today" -- so a revolt simply does not fire until a town is
  unhappy enough.
- `Data/Model/Spawn.cs`, `Data/Dto/SpawnDto.cs`, `Data/Adapter/SpawnDtoAdapter.cs`: the two new
  attributes.

**The founding-clan question.** The branch's rule is one clan, at most one invasion, enforced three
times: `ValidateFixedInvasionClan` refuses `OverridenClanSpawns` and `InheritClanFromSettlement`
(`InvasionRuntimeConfigurationValidator.cs:37-53`), `FindInvasionsSharingAClan` refuses a second
spawn on the same clan (`:65-80`), and `InvasionState.InvasionIndex` is keyed by `ClanId`
(`InvasionState.cs:11-12`). That rules out the two tempting options:

- *The settlement's original culture's clan* -- this is `InheritClanFromSettlement` by another
  name, explicitly refused, and for the stated reason: the invading faction would be whoever
  happens to own the target.
- *A new clan created per revolt* -- a group's identity is its founding clan id, and that id is read
  back from the save and from `Clan.FindFirst` in six places; a clan that does not exist at config
  load cannot be validated against the one-clan rule at all.

So: **a revolt names a fixed `spawn_clan` in its configuration, exactly as an invasion does today.**
One configured rebel clan per revolt spawn. Multiple simultaneous revolts mean multiple configured
rebel clans and multiple spawn entries. This is a content cost, and it is the honest one -- the
alternative is unwinding the identity rule.

The expansion machinery still applies: once the revolt outgrows one clan's war-party limit,
`InvasionReinforcementArrivals.FoundClan` (`:149-184`) creates sister clans
(`founder.StringId + "_" + group.ClanIds.Count`) inside the kingdom, which is exactly a revolt
gathering further houses to its cause.

### Design C -- A general "invasion origin" strategy interface: still rejected, for a narrower reason

The first draft of this document rejected *all* strategy interfaces here as single-use abstraction
per `AGENTS.md`. That reasoning no longer holds for the supply line: with three values serving five
call sites, `IInvasionSupplyLine` is not single-use, and Design A adopts it.

It still holds for **origin**. A revolt differs from an incursion only in *where the first party
appears* -- one branch in `GetSpawnSettlement`, reached once per spawn and never again. An
`IInvasionOriginStrategy` over that is one interface, one call site, two implementations whose whole
content is which settlement to return. Design B stays a pure rule plus a branch.

The distinction worth keeping: a strategy earns its place when the same question is asked from
several places at several moments (supply line: clocks, landings, food, over the invasion's whole
life). It does not when the question is asked once, at birth (origin).

---

## Recommendation

**Three changes, in this order. The first is a prerequisite, not a refinement.**

1. **Design A step 1** -- three doors to one port member. No behaviour change, so the existing
   coastal campaign is the test: behaviour identical before and after.
2. **Design A step 2** -- the three-valued strategy. First configuration that is not coastal.
3. **Design A2 (castles)** and **Design B (revolt origin)**, independently, in either order.

Design A is the whole of "invasions that aren't coastal", and it is the prerequisite for a revolt,
because a revolt with a sea-gated supply line is a revolt that never reinforces. Design B is
independently riskier -- it touches the shared spawn path every bandit spawn runs through. A2 is the
cheapest of the four and the only one that helps the loyalty problem.

### Blast radius

Step 1: `InvasionReinforcements.cs`, `InvasionFoodSupportRules.cs`,
`InvasionReinforcementArrivals.cs`, `InvasionOccupations.cs` -- 4 files, none new, all renames and
redirected calls.

Step 2 (as built): `InvasionSupplyLines.cs` (new), `InvasionSupplyBases.cs`,
`InvasionReinforcements.cs`, `InvasionReinforcementArrivals.cs`, `InvasionOccupations.cs`,
`InvasionConfig.cs`, `InvasionData.cs`, `InvasionDtoAdapter.cs` -- 8 files, one new.

Design A2: `InvasionSieges.cs`, `InvasionCoastalTownResolver.cs`,
`InvasionReinforcementArrivals.cs`, and the target policy if `<Targets>` is wanted -- 4 files, none
new.

Design B: `RevoltOriginRules.cs` (new), `SpawnBehaviour.cs`, `Spawn.cs`, `SpawnDto.cs`,
`SpawnDtoAdapter.cs` -- 5 files, one new.

Tests: `CustomSpawns.Tests/Spawn/InvasionConfigTests.cs`, `CustomSpawns.Tests/Invasions/*` (the
reinforcement and food-rule suites assert on the coastal parameter names), a new suite for the three
supply lines -- which is where this pays off, since each is a pure rule testable without the engine
-- and a new suite for `RevoltOriginRules`.

---

## Open questions

1. **Does `Free` mean "any held fortification", or must it be contiguous** with something the
   invasion already holds? The proposal assumes the simplest thing. A contiguity rule would make an
   overland campaign feel like a front line rather than a set of islands, but it is a real pathing
   rule and more machinery than asked for.

2. **Gate 5 needs no separate switch.** *Answered: dropped.* Gathering and the target restriction
   are the same filter, and that is now correct: the mod steers an invasion's parties only while it
   does not hold its capital, which is when gathering is wanted. What a landlocked supply line still
   has to answer is what plays the capital's part for it -- the first fortification taken, presumably.

2b. **Is `None` actually wanted, or is it `Free` with no rules?** `None` and "`Free` with an
   interval nobody ever reaches" look the same in play. `None` is worth its own value only if it
   should also make `<Reinforcements>` optional -- which today is a throw that drops the whole spawn
   (`InvasionConfig.cs:77-80`). If `<Reinforcements>` stays mandatory, `None` buys only clarity.

2c. **Castles: targets only, or also kingdom seats?** *Answered: a castle may be the kingdom seat.*
   `SetInitialHomeSettlement` (`InvasionOccupations.cs:100-104`) is left to take whichever
   fortification was captured first, castle or town.

2d. **Which settlements can be supply bases?** *Answered: coastal supply requires a town.* A coastal
   castle is a target and may be a foothold and a seat, but it never winds the reinforcement clocks,
   never takes an arrival and is never fed. So `CoastalSupplyLine` keeps the town test that
   `InvasionCoastalTownResolver.cs:26` already applies, and the castle guards of Design A2 are
   deliberately *not* removed from the supply path -- only from the capture, foothold and
   strength-budget paths. `AnySupplyLine` is the value that admits castles as supply bases.

3. **Does a revolt need to be at war with its target, and who declares it?** An invasion clan
   reaches its target through CustomSpawns' own diplomacy
   (`ConstantWarFactionDiplomacyProvider`, `CustomSpawnsClanDiplomacyProvider`). A revolt that is
   at constant war with *everyone* is not a revolt against one kingdom. I have not designed this
   because I do not know whether `revolt_in_kingdom` should imply a war declaration against that
   kingdom only, or whether the existing diplomacy config is expected to express it.

4. **Loyalty, security, or culture mismatch?** The brief lists all three as revolt triggers. I
   proposed loyalty alone because it is the one number that already exists on `Town` and reads
   cleanly as discontent. Adding security and a culture test triples the config surface for what may
   be no gameplay gain. Which do you actually want to drive it?

5. **Should the revolt target the settlement it spawned at, specifically?** The proposal relies on
   "nearest valid town" happening to be the revolting town. That is true at spawn, but if the
   rebels are driven off they will then besiege whatever is nearest, which may be a different
   kingdom's town. Acceptable, or does the revolting town need to be pinned as the first target
   (which is the same shape as `RequiresFirstCoastalConquest`, and would reuse the
   initial-target-filter mechanism)?

6. **Content-side observation, not a code bug.** The live CalradiaAtWar blocks still use the old
   reinforcement schema -- `<Reinforcements><PartyTemplateId>…</PartyTemplateId><IntervalInDays>…
   </IntervalInDays><ActivePartyCap>…</ActivePartyCap></Reinforcements>` -- whereas this branch
   expects `<Reinforcements><Rule><Id>…` and `InvasionConfigValidator` throws
   "Invasion Reinforcements requires at least one Rule" when there are none
   (`InvasionConfig.cs:77-80`). On this branch those two invasions look like they would be rejected
   at load. Worth confirming before using them as a reference configuration or as a playtest
   baseline for any of the above.
