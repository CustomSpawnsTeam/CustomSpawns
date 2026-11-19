# Invasion loyalty: why occupied towns revolt, and what to do about it

Research and design proposal. No production code is changed by this document.

Vanilla figures below are read from the decompiled 1.4.8 campaign assemblies
(`BannerlordSearch.Source` 1.4.8 content files). Every number is quoted from a named class and
member; nothing here is invented or tuned by feel.

## Part 1 — how vanilla loyalty actually works

### The model and its composition

`SettlementLoyaltyModel` is an abstract `MBGameModel<SettlementLoyaltyModel>`; the shipped
implementation is `DefaultSettlementLoyaltyModel`. `Town.DailyTick` applies the result once a day:

```csharp
// Town.cs
public float LoyaltyChange => Campaign.Current.Models.SettlementLoyaltyModel.CalculateLoyaltyChange(this).ResultNumber;
internal void DailyTick() { Loyalty += LoyaltyChange; Security += SecurityChange; ... }
public float Loyalty { set { _loyalty = MathF.Clamp(value, 0f, 100f); } }   // clamped 0..100
```

`DefaultSettlementLoyaltyModel.CalculateLoyaltyChangeInternal` sums, in order: food stocks,
governor culture, owner culture, policies, projects, issues, security, notable relations, governor
perks, and loyalty drift.

### The terms, with magnitudes

| Term | Value | Condition |
|---|---|---|
| Drift | `-0.1 * (Loyalty - 50)` | always (`LoyaltyDriftMedium = 50`) |
| Owner culture | **-3** | `OwnerClan.Culture != Settlement.Culture` |
| Governor culture | **+1 / -1** | only if `town.Governor != null`; **no governor contributes 0** |
| Starving | **-1**, then **-2** | `Settlement.IsStarving`; the extra -1 after `DaysStarving > 14` |
| Security | `+1 .. -2` | `MBMath.Map(Security, 50..100 -> 0..+1)` or `Map(Security, 0..50 -> -2..0)` |
| Notables | ±0.5 each | `+0.5` per notable supporting the owner clan, `-0.5` per notable supporting a clan the town's faction is at war with |
| Projects | see below | `AddEffectOfBuildings(BuildingEffectEnum.Loyalty, ...)` |
| Policies | see below | from `kingdom.ActivePolicies` |

Policies (`GetSettlementLoyaltyChangeDueToPolicies`): Citizenship ±0.5 (sign by owner/settlement
culture match), HuntingRights -0.2, GrazingRights +0.5, TrialByJury +0.5, ImperialTowns +1.0 if
the owner is the ruling clan else -0.3, ForgivenessOfDebts +2.0, TribunesOfThePeople +1.0 (towns),
DebasementOfTheCurrency -1.0.

Thresholds on the model: `RebellionStartLoyaltyThreshold = 15`,
`RebelliousStateStartLoyaltyThreshold = 25`, `MaximumLoyaltyInSettlement = 100`,
`LoyaltyBoostAfterRebellionStartValue = 5`, `MilitiaBoostPercentage = 200`.

**There is no garrison-size loyalty term anywhere in `DefaultSettlementLoyaltyModel.`** The brief's
"garrison size pressure" premise does not exist in this version — see Part 3's rejected options.
Likewise there is no "recently conquered" loyalty effect: `ChangeOwnerOfSettlementAction.ApplyInternal`
touches loyalty nowhere.

### The arithmetic

Let `S` be the sum of every non-drift term. One day is then

```
L(n+1) = L(n) + S - 0.1 * (L(n) - 50)  =  0.9 * L(n) + (S + 5)
```

a first-order linear recurrence, so loyalty does not slide forever — it converges:

```
equilibrium   L* = 50 + 10 * S
trajectory    L(n) = L* + (L0 - L*) * 0.9^n
```

Therefore, exactly:

- to stay out of the rebellious state (`L > 25`) permanently: **`S > -2.5`**
- to stay out of rebellion (`L > 15`) permanently: **`S > -3.5`**

### How loyalty becomes a rebellion

`RebellionsCampaignBehavior.DailyTickSettlement` runs for every town that is not in a map event or
siege, is not owned by a rebel clan, and is not the player's current settlement:

```csharp
CheckAndSetTownRebelliousState(settlement);                       // InRebelliousState = Loyalty <= 25
if (MBRandom.RandomFloat < 0.25f && CheckRebellionEvent(settlement))
    StartRebellionEvent(settlement);
```

```csharp
private static bool CheckRebellionEvent(Settlement settlement)
{
    if (settlement.Town.Loyalty <= 15)                            // RebellionStartLoyaltyThreshold
    {
        float militia = settlement.Militia;
        float num = settlement.Town.GarrisonParty?.Party.CalculateCurrentStrength() ?? 0f;
        foreach (MobileParty party in settlement.Parties)
            if (party.IsLordParty && DiplomacyHelper.IsSameFactionAndNotEliminated(party.MapFaction, settlement.MapFaction))
                num += party.Party.CalculateCurrentStrength();
        return militia >= num * 1.4f;                              // MilitiaGarrisonRatio
    }
    return false;
}
```

So rebellion is a **two-gate** event: loyalty at or under 15 **and** militia at least 1.4x the
combined strength of the garrison plus friendly lord parties present. Then a 25% roll per day.

The rebellious state itself feeds the militia that opens the second gate
(`DefaultSettlementMilitiaModel`):

```csharp
if (settlement.Town.InRebelliousState) {
    float num2 = MBMath.Map(settlement.Town.Loyalty, 0f, 25f, 200f, 0f);   // MilitiaBoostPercentage
    result.Add(MathF.Abs((prosperity / 1000f) * (num2 * 0.01f)), LowLoyaltyText);
}
```

At loyalty 0 that triples the prosperity-driven militia growth. Low loyalty is thus
self-reinforcing through militia even though loyalty itself converges.

When it fires (`StartRebellionEvent` / `ApplyRebellionConsequencesToSettlement`): 50% of the
garrison roster becomes prisoners and the remainder of the roster is cleared; the garrison is
refilled with `settlement.Culture.RangedMilitiaTroop` at ~60% of militia; the militia party is
destroyed; a rebel clan of four generated heroes is created (`Clan.CreateSettlementRebelClan`) with
three lord parties and 50,000 gold; `DeclareWarAction.ApplyByRebellion` and
`ChangeOwnerOfSettlementAction.ApplyByRebellion` transfer the town; loyalty is set to 100, food to
`FoodStocksUpperLimit()`, militia to 100. The rebel clan reverts to a normal clan after 30 days
(`UpdateClanAfterDays = 30`). For an invasion this is terminal: the foothold is gone, the garrison
is halved into captivity, and the invasion's kingdom gains a new war.

### Supporting models that matter here

`DefaultSettlementSecurityModel`: drift `-1 * (Security - 50) / 15`, so security equilibrium is
`50 + 15 * S_sec`. The garrison **raises** security: `+0.01 * garrisonStrength`
(`CalculateGarrisonEffectsOnSecurity`). Other terms: `max(-5, -0.0005 * Prosperity)`, under siege
-3, each looted bound village -2, nearby infested hideout -2, Bailiffs/Serfdom/Magistrates +1 each.
`CalculateGovernorEffectsOnSecurity` and `CalculateProjectEffectsOnSecurity` have empty bodies.

`DefaultSettlementFoodModel`: `FoodStocksUpperLimit = 300`; production `15` for a town plus
`(GetHearthLevel() + 1) * 6` per non-raided, non-besieged bound village, and under siege the
"lands around" term is skipped entirely; consumption `Prosperity / 40 + GarrisonParty.NumberOfAllMembers / 20`
(`NumberOfProsperityToEatOneFood = 40`, `NumberOfMenOnGarrisonToEatOneFood = 20`).

### What a mod can influence without patching

- **`SettlementLoyaltyModel` is replaceable.** `CampaignGameStarter.GetModel<T>()` returns the
  current model and `starter.AddModel(...)` installs a replacement; last added wins, and the result
  is readable back as `Campaign.Current.Models.SettlementLoyaltyModel`. CustomSpawns already does
  exactly this for `SettlementFoodModel`. No Harmony needed.
- **Owner clan culture**, set when the invasion's kingdom is founded.
- **Governor assignment** (`ChangeGovernorAction`).
- **Kingdom policies** on the invasion's own kingdom.
- **The daily project** (`BuildingHelper.ChangeDefaultBuilding`) — with a large caveat, below.
- **Food stocks** directly (`town.FoodStocks`) and via the food model, which CustomSpawns already
  decorates.

CustomSpawns currently overrides `SettlementFoodModel`, a diplomacy model and `PartySizeLimitModel`
(`Main.cs` ~204-211). **No `SettlementLoyaltyModel` override exists.**

### The buildings that give loyalty, and why they are unreliable

`DefaultBuildingTypes` contains exactly two sources of `BuildingEffectEnum.Loyalty`:

```csharp
// Courthouse — a construction project
(BuildingEffectEnum.Loyalty, BuildingEffectIncrementType.Add, 0.3f, 0.6f, 1f)   // levels 1/2/3
// Festival and Games — InitializeDailyProject
(BuildingEffectEnum.Loyalty, BuildingEffectIncrementType.Add, 3f, 3f, 3f)       // flat +3/day
```

Festival and Games is a flat **+3.0 loyalty/day**, costs nothing, and exactly cancels the -3 owner
culture penalty. But a daily project only counts while it is the selected default:

```csharp
// Building.AddEffectOfBuilding
if (_currentLevel != 0 && (!BuildingType.IsDailyProject || Town.CurrentDefaultBuilding == this) && BuildingType.HasEffect(buildingEffect))
```

and `CurrentDefaultBuilding` is **null whenever a construction project is queued**:

```csharp
// Town.cs
public Building CurrentDefaultBuilding {
    get { if (!BuildingsInProgress.IsEmpty()) return null;
          return Buildings.FirstOrDefaultQ(k => k.IsCurrentlyDefault); } }
```

`BuildingsCampaignBehavior` clears `BuildingsInProgress` on an ownership change to a non-player
clan, so the queue is empty right after capture — but then, per town per day, it runs
`DecideBuildingQueue` at 10% and `DecideDailyProject` at 1%:

```csharp
if (MBRandom.RandomFloat < 0.1f)  DecideBuildingQueue(town);     // enqueues a construction project
if (MBRandom.RandomFloat < 0.01f) DecideDailyProject(town);
```

So a one-shot "set Festival and Games" survives the 1% daily re-pick well, but the 10% daily
`DecideBuildingQueue` enqueues a building and **silently zeroes the +3** (expected within ~10 days,
and it stays zeroed for the whole multi-year construction). Keeping it alive would mean clearing
another faction's build queue every day. That demotes this lever from "cheapest fix" to "fallback";
see Part 3.

### The realistic worst case for an invasion-held town

Owner culture -3 (always, by construction — see Part 2). Governor 0 (vanilla strips the governor on
every ownership change). Notables: the dispossessed owner clan is at war with the invasion, so
hostile-supporter notables give roughly -0.5 to -2. Security: with a large dumped garrison,
`+0.01 * strength` pins security at the 100 clamp, giving the loyalty term **+1**; if the garrison
melts away, security collapses toward the prosperity penalty and the term goes to **-2**. Food: -1,
then -2 after 14 days of starvation.

| Scenario | `S` | `L*` | Outcome |
|---|---|---|---|
| Culture penalty only, garrison intact, fed | -3 + 1 = **-2** | 30 | holds, never even rebellious |
| Culture + hostile notables, fed, garrison intact | **-3.5** | 15 | pinned at the rebellion threshold |
| Culture + notables + starving, garrison intact | **-4.5** | 0 (clamped) | reaches 25 in ~7 d, 15 in ~11 d |
| Culture + notables + starving >14 d + garrison gone | **-8.5** | 0 (clamped) | reaches 15 in ~8 d |

From a typical post-capture `L0 = 50` with `S = -5`: `L(n) = 50 * 0.9^n`, so 25 at
`n = ln(0.5)/ln(0.9) ≈ 6.6` days and 15 at `n = ln(0.3)/ln(0.9) ≈ 11.4` days, then a 25%/day roll
once the militia gate also opens. That matches the reported "coastal towns always revolt".

Note the one honest tension: a large intact garrison **blocks** the second gate
(`militia >= 1.4 * garrisonStrength`) outright, even at loyalty 0. So an observed revolt means the
garrison was no longer large — see the open questions.

## Part 2 — what CustomSpawns does to a captured town today

Branch `implement-new-party-behaviour`. Occupation runs on the hourly tick, once the capture
callbacks have settled: `InvasionCampaignBehaviour.OnHourlyTick` -> `InvasionOccupations.ApplyReady()`.

`InvasionOccupations.ApplyReady` does three things per ready capture:

1. **Garrison dump.** Every still-active escort party of the capture has its whole `MemberRoster`
   added to `town.GarrisonParty.MemberRoster` and is then destroyed. There is no bound of any kind
   on the resulting garrison size (`docs/invasion-logistics.md` states this is deliberate).
2. **Food floor,** once, if eligible: `InvasionOccupationFood.Apply(town, configuration.FoodStockFloor)`
   -> `town.FoodStocks = max(existing, min(capacity, floor))`.
3. **`PromoteCapturer`,** which converts the capturer to a lord party and, if the clan has no
   kingdom, founds one: `Campaign.Current.KingdomManager.CreateKingdom(clan.Name, clan.InformalName, clan.Culture, clan)`.

Ongoing, `InvasionOccupations.GetOngoingFoodSupport(town)` returns `configuration.DailyFoodBonus`
while the foothold is held, surfaced through `InvasionCampaignBehaviour.GetOngoingFoodSupport` and
consumed by the `InvasionSettlementFoodModel` decorator registered in `Main.cs`.

Admission rules live in the pure domain class `InvasionFoodSupportRules.IsEligibleForOngoingSupport`
(foothold held, live coastal town, owned by the holding group, configuration enabled, not under
siege).

**The mod touches loyalty nowhere.** Grepping `Invasions/` for `Loyalty` finds nothing.

### Does the mod make the loyalty problem worse? Yes, in two specific ways

1. **It guarantees the -3 owner-culture penalty, permanently.** `PromoteCapturer` founds the
   kingdom with `clan.Culture` — the invader's culture. The captured English town keeps
   `Settlement.Culture`, so `OwnerClan.Culture != Settlement.Culture` holds forever. On its own this
   is survivable (`S = -2` with security help, `L* = 30`), but it is the fixed -3 that every other
   penalty stacks on top of.
2. **The unbounded garrison dump drives the food deficit that supplies the -1/-2 starvation term.**
   Consumption is `Prosperity/40 + garrison/20`. At prosperity 3000 that is 75/day before the
   garrison; a 400-man dumped garrison adds another 20/day; production is only
   `15 + sum((hearth+1) * 6)` over bound villages, and a freshly invaded region's villages are
   typically raided (contributing 0) or still enemy-held. The shipped illustrative `DailyFoodBonus`
   is far too small to close a deficit of that size, so the town starves, and after day 14 the
   penalty doubles.

Everything else the mod does is loyalty-neutral. Nothing in the occupation path sets a governor,
adopts a policy, or touches the daily project.

## Part 3 — options, ranked

Required margins, from Part 1: `S > -2.5` keeps a town out of the rebellious state permanently;
`S > -3.5` keeps it out of rebellion. Target `S >= -2` (`L* >= 30`) for headroom.

### Recommended primary: a bounded `SettlementLoyaltyModel` decorator, mirroring the food model

A `SettlementLoyaltyModel` decorator that adds a fixed contribution to
`CalculateLoyaltyChange` for a town the invasion currently holds, and nothing anywhere else.

- **Magnitude: +3.0/day.** Not an invented number — it is vanilla's own Festival-and-Games
  loyalty figure, and it exactly cancels the owner-culture -3. With the food leg below it takes the
  worst realistic case from `S ≈ -4.5` to `S ≈ -1.5` (`L* = 35`) and the garrison-gone case from
  `-8.5` to `-5.5`, which still falls — correctly, because an invasion whose garrison has
  evaporated should lose the town.
- **Implementation.** New adapter `Invasions/Adapters/InvasionSettlementLoyaltyModel.cs`, a direct
  structural copy of the existing `InvasionSettlementFoodModel`: `sealed class ... : SettlementLoyaltyModel`,
  constructor `(SettlementLoyaltyModel inner, Func<Town, float> contribution)`, every abstract
  member forwarded, and in `CalculateLoyaltyChange` add the contribution with a named `TextObject`
  ("Occupation garrison" or similar) so the player sees why. Feed it from a new
  `InvasionOccupations.GetOngoingLoyaltySupport(Town)` with the same shape as
  `GetOngoingFoodSupport`, exposed through `InvasionCampaignBehaviour` for the same reason the food
  accessor is (the occupation pass is replaced when a save rebinds state). Register in `Main.cs`
  beside the food model.
- **Admission rules.** Reuse `InvasionFoodSupportRules.IsEligibleForOngoingSupport` as-is rather
  than writing a parallel rules class: the conditions wanted for loyalty are the same five
  (foothold held, coastal town, owned by the holding group, configuration enabled, not under siege),
  and a second single-use rules class would be an abstraction for nothing. If the two ever diverge,
  split then. **Open question for the owner:** should loyalty support be restricted to coastal towns
  the way food is, or should it apply to any town the invasion holds? The food rule's coastal
  condition models supply by sea; loyalty support models garrison rule, which is not coastal. I have
  not decided this silently — it changes which rules class is reused and is the one real design fork
  here.
- **Config:** none. A fixed +3 derived from a vanilla constant needs no knob, and
  `AGENTS.md` forbids speculative configurability. If the owner wants it configurable, it belongs
  beside `DailyFoodBonus` in `InvasionSuppliesConfig` -> `InvasionDtoAdapter` -> `InvasionData`,
  with the same `ValidateNonNegativeFinite` treatment.
- **Risk:** low. The decorator forwards everything, is scoped by town, and vanishes when the
  foothold is lost. The real risk is model-stacking with another mod that replaces
  `SettlementLoyaltyModel`; the food model already carries that risk and logs the selected model
  type on the first campaign tick — do the same.
- **Blast radius:** one new adapter file, one accessor on `InvasionOccupations`, one on
  `InvasionCampaignBehaviour`, three lines in `Main.cs`. No save-data change. No domain change if
  the food rules are reused.

### Necessary second leg (not optional): size `DailyFoodBonus` against the real deficit

The starvation term is worth -1, then -2 after 14 days, and starvation is what makes the culture
penalty lethal rather than merely annoying. Set `DailyFoodBonus` from the actual arithmetic:
`Prosperity/40 + garrison/20 - (15 + sum((hearth+1) * 6))`. For a prosperity-3000 town with a
400-man garrison and raided villages that is roughly 95/day of consumption against 15/day of
production. No existing sample value is close. Pure config; zero code; zero risk. Without this leg
the loyalty decorator is fighting a penalty it does not need to fight.

### Fallback A: set Festival and Games as the captured town's daily project

One call to `BuildingHelper.ChangeDefaultBuilding` at occupation, re-asserted while the foothold is
held. Pure vanilla, +3/day, zero config, and perfectly believable in fiction (an occupier buying
goodwill with festivals). **But** it is silently defeated by `CurrentDefaultBuilding` returning null
whenever `BuildingsInProgress` is non-empty, and `DecideBuildingQueue` enqueues a project at 10%
per town per day. Keeping it would mean clearing the town's construction queue daily, which
suppresses all building progress in the occupied town — a much larger and much less honest side
effect than a scoped loyalty term. Recommended only if the owner prefers "no model override" over
"no queue interference". Courthouse (+0.3/0.6/1.0) is too small and too slow to matter.

### Fallback B: adopt loyalty policies in the invasion's kingdom

The invasion founds its own kingdom, so it can hold its own policies: ForgivenessOfDebts +2.0,
TribunesOfThePeople +1.0 (towns only), GrazingRights +0.5, TrialByJury +0.5 (but -0.2 security).
Adopting ForgivenessOfDebts plus TribunesOfThePeople is +3.0 from pure vanilla mechanics with no
model override at all. Costs: it is kingdom-wide rather than town-scoped, it has unrelated economic
side effects, and it needs code to force a policy onto an AI kingdom (which the AI may later
revoke). Good as a flavour addition on top of the primary, weak as a replacement.

### Fallback C: give the invading clan the settlement's culture, or pick invasion clans by culture

Removes the -3 at the root and makes `Citizenship` positive instead of negative. But it breaks the
fiction the feature exists for — a Scottish or French invasion whose clan is culturally English is
no longer an invasion — and `clan.Culture` feeds troop trees, names and recruitment far beyond
loyalty. Reject.

### Rejected: assign a governor

`town.Governor == null` contributes **0**; a different-culture governor contributes **-1**. Every
hero in an invading clan is foreign-culture, so assigning one makes the town strictly worse. A
same-culture governor (+1) requires a local-culture hero inside the invading clan, which the
occupation path cannot produce (retaking a town imprisons the previous owner's heroes). The brief's
"same-culture governor" option is not reachable from this code path.

### Rejected: keep the occupation garrison small

The premise is inverted for 1.4.8. There is no garrison-size loyalty term at all. A large garrison
is doubly protective: `+0.01 * garrisonStrength` of security (which pushes the loyalty security term
toward +1 instead of -2), and `CheckRebellionEvent` requires `militia >= garrisonStrength * 1.4f`,
which a large garrison blocks outright even at loyalty 0. The only harm a big garrison does is
`garrison/20` food per day — which is a food problem, addressed by the second leg. Shrinking the
garrison would make revolts *more* likely, not less.

### Last resort, and it is a cheat: suppress rebellion for invasion-held towns

Technically easy — `_rebellionEnabled` is private, but `CheckAndSetTownRebelliousState` keys off the
model's `RebelliousStateStartLoyaltyThreshold`, so a decorator returning a negative threshold for
the occupied town would stop the rebellious state and therefore the militia boost. To be honest
about it: **this is a cheat, not a mechanic.** It makes occupied towns unloseable to their own
population, which is precisely the drama an invasion should generate, and it would silently also
suppress the militia growth that the rebellious state is supposed to cause. Use only if the owner
decides invasion footholds must never be lost to revolt by design, and say so in the feature's docs
if so.

### Not recommended now: a decaying grace period

A support term that decays over the first N days after capture is the most believable shape (an
occupier's grip loosening or tightening with time). Its cost is state: `InvasionFoothold` carries
only `SettlementId`, `InvasionClanId`, `CaptureId` and has **no capture timestamp**, so this needs either a
new saved field (and `InvasionSaveData` migration) or deriving the clock from
`InvasionCapture.BattleStartTimeBits` via the foothold's `CaptureId`. The derived route adds no save
data but ties the decay clock to the siege's start rather than the occupation's. Since a flat,
foothold-scoped +3 already makes the arithmetic work, the decay is extra machinery for a problem
that is already solved — propose it only if playtesting shows a permanent +3 feels too safe.

## Verification

As in `docs/invasion-logistics.md`: hold a captured coastal town for at least three weekly cycles
and record loyalty, the loyalty tooltip breakdown (the decorator's `TextObject` must appear),
security, militia, food stocks, and garrison strength. The pass condition is loyalty settling at or
above 30 and `InRebelliousState` never becoming true, with food stocks non-decreasing.

## Open questions for the owner

1. Should loyalty support be coastal-only (reusing `InvasionFoodSupportRules` unchanged) or apply to
   any town the invasion holds (needing its own rule)? This is the one genuine design fork.
2. Given that `CheckRebellionEvent` requires `militia >= 1.4 * garrisonStrength`, an observed revolt
   means the dumped garrison was no longer large by the time loyalty hit 15. Which is it: the
   garrison starving down from the food deficit, desertion from unpaid wages in a bankrupt invasion
   clan, or the town not actually qualifying as a food-eligible coastal foothold (so it never got
   even the food floor)? Confirming this on a live save decides whether the loyalty decorator or the
   food number is the primary fix. I recommend checking it before implementing either.
3. Should the invasion's kingdom also adopt ForgivenessOfDebts / TribunesOfThePeople for flavour, or
   stay policy-free?
