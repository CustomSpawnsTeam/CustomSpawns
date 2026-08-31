# Invasion logistics

An optional, element-only `Invasion` block in a spawn definition enables coastal footholds, overseas food, a bounded occupation garrison and independent reinforcements. Omitting it preserves existing spawn behaviour. The block belongs to the spawn, not its shared party-behaviour template.

```xml
<Invasion>
  <Foothold />
  <Supplies>
    <FoodStockFloor>100</FoodStockFloor>
    <DailyFoodBonus>5</DailyFoodBonus>
  </Supplies>
  <OccupationGarrison>
    <MinimumTroopCount>90</MinimumTroopCount>
    <MaximumTroopCount>140</MaximumTroopCount>
    <MilitiaStrengthRatio>1</MilitiaStrengthRatio>
    <MaximumDonorLossFraction>0.2</MaximumDonorLossFraction>
  </OccupationGarrison>
  <Reinforcements>
    <PartyTemplateId>cs_makko_invasion_reinforcement</PartyTemplateId>
    <IntervalInDays>7</IntervalInDays>
    <ActivePartyCap>3</ActivePartyCap>
  </Reinforcements>
</Invasion>
```

These are initial sample balance values, not a guarantee of positive food, rebellion prevention or siege success. The Makkonian and Humarian invasion definitions are the only sample spawns opted in. Their dedicated reinforcement templates contain 222–333 and 252–405 troops respectively, before the generated leader. Scouts, ordinary armies, original spawn chances/caps and shared behaviour templates are unchanged.

## Configuration

| Element | Meaning / omitted default |
| --- | --- |
| `Foothold/CoastalTownIds/TownId` | Repeated raw settlement IDs. An empty list uses native port metadata. A nonempty list replaces that set; entries must still be towns. |
| `Supplies/FoodStockFloor` | One-time minimum food stock after occupation, limited by actual capacity; default 0. Existing stock is never reduced. |
| `Supplies/DailyFoodBonus` | Additive daily `Overseas supplies` food-model contribution; default 0. |
| `OccupationGarrison/MinimumTroopCount` | Minimum desired total native garrison count, not the size of an additional donation; default 0. |
| `OccupationGarrison/MaximumTroopCount` | Upper bound on desired total count, also limited by actual garrison capacity; default 0. |
| `OccupationGarrison/MilitiaStrengthRatio` | Desired garrison defensive strength relative to observed militia strength; default 0. Count bounds still apply. |
| `OccupationGarrison/MaximumDonorLossFraction` | Maximum fraction of each participating donor's healthy troop budget; 0–1, default 0. |
| `Reinforcements/PartyTemplateId` | Required raw troop-template ID, without the `PartyTemplate.` prefix. |
| `Reinforcements/IntervalInDays` | Positive interval for both the first and later arrivals; default 7. |
| `Reinforcements/ActivePartyCap` | Positive cap on surviving registered reinforcements in this group; default 1. The sample explicitly uses 3. |

All quantities must be finite and nonnegative, with maximum garrison count at least its minimum. Runtime validation checks troop/town references, non-bandit ownership and an effective behaviour template able to besiege towns. Invalid opt-ins are diagnosed without breaking unrelated valid spawn definitions. Reinforcements inherit the original `PriorityPartyBehaviourTemplateId`; there is no second behaviour configuration.

For a map with unsuitable port metadata, use an explicit list:

```xml
<Foothold>
  <CoastalTownIds><TownId>your_coastal_town_id</TownId></CoastalTownIds>
</Foothold>
```

## Groups, conquests and supply

One logistics group belongs to an original spawn-template ID and actual clan. Several original armies and their escorts can share it. Each town's credit belongs exclusively to the verified capturing party's group; allied conquests and other same-clan groups do not receive duplicate credit or supply.

Before the first credited coastal conquest, the group may begin a siege only at an eligible coastal town with a reachable land gate. Existing hostility, defender-strength, reconnaissance and siege-compatibility checks still apply. No viable coast means the normal raid/patrol fallback, not an inland exception. A native assault already running when a legacy save adopts the rule is allowed to finish. Coastal success permanently unlocks the configured town/castle targets across the group; losing ports does not re-lock expansion or create defence/retaking orders.

Ownership loss deletes credit and pending occupation effects. Peaceful reacquisition does not restore them. A new attributable siege capture creates a new transition. Assault victories and winning besieger-side sally-out results use native ownership handling; the feature never force-transfers a settlement or creates a kingdom.

Only credited, currently eligible coastal towns receive overseas food. Siege pauses deliveries without deleting credit; they resume when it ends. Later inland town conquests may receive garrison troops but no overseas food. The food decorator preserves the selected underlying model and adds its bonus once through that model. UI/model queries do not replenish stocks or advance timers. A later mod replacing the decorator is diagnosed and requires checking load/model order.

## Occupation and reinforcement lifecycle

Occupation waits until ownership and the actual battle participant snapshot agree, the native garrison is active, and the town and donors have left their map events. One guarded hourly operation observes the native donation first, transfers only the remaining bounded deficit, applies eligible initial food relief, then records completion. Duplicate callbacks and save/load must not repeat the package.

Garrison troops come only from healthy nonhero survivors in the credited group's actual participants, including participating escorts. Wounded troops, prisoners, generated leaders, distant parties and other groups are excluded. Selection is deterministic and mixed where ranged and melee troops are available; counts and stack XP are conserved using both rosters' native XP limits. Scarcity gives a smaller contribution and a diagnostic. Native wages, recruitment and desertion remain in force; there is no wage subsidy or weekly garrison top-up. An unexpected failure after roster mutation begins is terminal for that occupation and diagnosed rather than replaying the donor allowance.

The group's first arrival is due at its first credited coastal conquest plus the configured interval. A daily check considers the group once and chooses one held, eligible, non-besieged port. An arrival uses the land gate, its own troop template and generated leader, fresh AI state, and the original behaviour configuration. It has no `SupportedParty` and creates no escorts. Its creation/death does not change original `MaximumOnMap` accounting.

A due slot blocked by the cap, unavailable port/configuration/template, or spawn failure is skipped until the next interval. There is no backlog or catch-up loop; advancing multiple weeks produces at most one current arrival. Port ownership is rechecked at the spawn call. Membership and deadlines persist, while active counts are reconciled from surviving registered parties. Losing the original army does not stop an eligible surviving foothold; a group disappears only when it has neither credited territory nor surviving members.

## Save compatibility and verification

Existing save IDs and original-spawn meanings are retained. Group, role, credit, capture evidence, occupation completion and scheduling data are appended. Legacy saves adopt identifiable parties prospectively, without inferring old conquests from clan ownership. Ambiguous capture history gets no retroactive food or troop transfer. Removed configuration or eliminated clans suspend new effects while allowing saved state to be cleaned up.

Compile references remain v1.4.7; installed v1.4.8 is the accepted native verification runtime. Focused tests establish rule, mapping and adapter-boundary behaviour, not live campaign balance. Before treating sample balance as verified, record a new campaign and pre-feature-save load, occupation/deadline reloads, and at least three weekly cycles with food, loyalty, security, militia, garrison strength, donor counts/XP, clan wages and party counts. Each reinforcement must demonstrate the existing 1.5 target-strength and 1.2 roaming-hostile ratios against a real candidate; troop counts alone are not proof.

Runtime verification status and exact build/test evidence belong in the implementation report. Do not infer a successful live playtest from this configuration example.
