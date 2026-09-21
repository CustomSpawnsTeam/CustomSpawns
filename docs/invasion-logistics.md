# Invasion logistics

An optional, element-only `Invasion` block in a spawn definition enables invasion occupation and logistics. `RequiresFirstCoastalConquest` separately opts that spawn into coastal-first targeting. Omitting the `Invasion` block preserves existing spawn behaviour. The block belongs to the spawn, not its shared party-behaviour template.

```xml
<Invasion>
  <RequiresFirstCoastalConquest>true</RequiresFirstCoastalConquest>
  <Supplies>
    <FoodStockFloor>100</FoodStockFloor>
    <DailyFoodBonus>5</DailyFoodBonus>
  </Supplies>
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
| `RequiresFirstCoastalConquest` | When `true`, restricts the invasion to coastal towns while its current faction owns no coastal town; default `false`. |
| `Supplies/FoodStockFloor` | One-time minimum food stock after occupation, limited by actual capacity; default 0. Existing stock is never reduced. |
| `Supplies/DailyFoodBonus` | Additive daily `Overseas supplies` food-model contribution; default 0. |
| `Reinforcements/PartyTemplateId` | Required raw troop-template ID, without the `PartyTemplate.` prefix. |
| `Reinforcements/IntervalInDays` | Positive interval for both the first and later arrivals; default 7. |
| `Reinforcements/ActivePartyCap` | Positive cap on surviving registered reinforcements in this group; default 1. The sample explicitly uses 3. |

All quantities must be finite and nonnegative. Runtime validation checks troop-template references, non-bandit ownership and an effective behaviour template able to besiege towns. Invalid opt-ins are diagnosed without breaking unrelated valid spawn definitions. Reinforcements inherit the original `PriorityPartyBehaviourTemplateId`; there is no second behaviour configuration.

## Groups, conquests and supply

One logistics group belongs to an original spawn-template ID and the clan that founded it, plus any further clan it expands into when that one runs out of war party slots. Several original armies and their escorts can share it. Each town becomes a foothold of the verified capturing party's group alone; allied conquests and other same-clan groups gain no duplicate foothold or supply.

When `RequiresFirstCoastalConquest` is enabled and the invasion party's current faction owns no coastal town, the group may begin a siege only at an eligible coastal town with a reachable land gate. Existing hostility, defender-strength, reconnaissance and siege-compatibility checks still apply. No viable coast means the normal raid/patrol fallback, not an inland exception. Owning any coastal town unlocks the configured town/castle targets; losing the faction's last coastal town enables the restriction again. This is calculated from live ownership and is not saved as invasion state.

Ownership loss deletes the foothold and pending occupation effects. Peaceful reacquisition does not restore them. A new attributable siege capture creates a new transition. Assault victories and winning besieger-side sally-out results use native ownership handling; successful occupation promotes the capturing invasion clan into its own kingdom.

Only footholds that are still eligible coastal towns receive overseas food. Siege pauses deliveries without deleting the foothold; they resume when it ends. Later inland town conquests may receive garrison troops but no overseas food. The food decorator preserves the selected underlying model and adds its bonus once through that model. UI/model queries do not replenish stocks or advance timers. A later mod replacing the decorator is diagnosed and requires checking load/model order.

## Occupation and reinforcement lifecycle

Occupation waits until ownership and the actual battle participant snapshot agree, the native garrison is active, and the town and donors have left their map events. One guarded hourly operation merges the escorts into the garrison, applies eligible initial food relief, then records completion. Duplicate callbacks and save/load must not repeat the package.

The transfer is unconditional: every still-active escort party among the capture's recorded participants has its whole `MemberRoster` added to the town's garrison, and the emptied party is then destroyed. There is no configured or capacity-derived bound on the resulting garrison, and no filtering of wounded troops or heroes — the roster moves as it stands. Non-escort participants, parties that are no longer active and other groups are excluded. Native wages, recruitment and desertion remain in force; there is no wage subsidy or weekly garrison top-up.

The group's first arrival is due at its first coastal foothold plus the configured interval. A daily check considers the group once and chooses one held, eligible, non-besieged port. An arrival uses the land gate, its own troop template and generated leader, fresh AI state, and the original behaviour configuration. It has no `SupportedParty` and creates no escorts. Its creation/death does not change original `MaximumOnMap` accounting.

A defeated invasion lord is respawned by the game as an ordinary lord of its clan under a party name no spawn template matches, so it comes back outside its invasion. Such a lord is adopted back into its clan's group: it is registered under that group's spawn template, carries the `"Adopted"` role across saves, and has its regulars replaced by the troops a fresh spawn of the same invasion would draw, its heroes left alone. That restores its membership, food eligibility, steering and spawn-sized party limit.

Adoption is bounded by the group's budget -- `MaximumConcurrentPrimaries` plus `Reinforcements/ActivePartyCap` -- weighed against the group's live non-escort members, so a clan that keeps losing lords does not accumulate invasion parties without end. Escorts are outside that count, and an adopted lord spends no landing slot against the reinforcement cap. The bound is on adopting, not on how many parties are alive: the spawner keeps its own separate count.

A due slot blocked by the cap, unavailable port/configuration/template, or spawn failure is skipped until the next interval. There is no backlog or catch-up loop; advancing multiple weeks produces at most one current arrival. Port ownership is rechecked at the spawn call. Membership and deadlines persist, while active counts are reconciled from surviving registered parties. Losing the original army does not stop an eligible surviving foothold; a group disappears only when it has neither a foothold nor surviving members and the kingdom it leaves behind has been retired.

## Losing the foothold

Promotion hands an invasion to vanilla: while the group holds a town, the native AI steers its lords. A group that holds no foothold at all is steered by CustomSpawns again and resumes its configured behaviour template, so an invasion that took a town and then lost it goes back on the offensive instead of sitting still. Holding one of two towns changes nothing; only the last one matters. Like the coastal restriction, this is read from live footholds rather than saved, and a suspended group — removed configuration, eliminated clan — never counts as live.

A kingdom carrying a live invasion survives the loss of its last settlement. `CanKingdomBeDiscontinuedEvent` is vetoed for any kingdom one of whose clans carries an unsuspended invasion group that still has something to fight with, so the kingdom is still there to retake a town with.

While the coastal restriction applies — the initial landing, and every retake that follows a lost foothold — the group chooses and sizes up its target as one force rather than as whichever party happens to be deciding. Candidate towns are ranked by the summed gate distance of every member, so the group converges on the town that is nearest for all of them, and a garrison is weighed against the summed strength of those same members. Escorts are members themselves and are counted once. A group with a single party ranks and assesses exactly as it did before, and once the group holds a town each party decides for itself again under native rules.

An invasion with no surviving member and no foothold is spent, and the kingdom it was given is retired. The veto stops applying at that point, but vanilla only asks whether a kingdom can be discontinued when a settlement changes hands or the player switches kingdom, so the missing trigger is supplied on the hourly tick. The rule that decides is vanilla's own — not eliminated, not the player kingdom, holding no settlement, and the event dispatcher consulted so other mods keep their vote — and so are the actions that end it: each clan leaves as an independent with its map and siege events finalised, the ruling clan is cleared, and the kingdom is destroyed. Departing clans then get the usual native time to survive on their own.

The end is deliberately gradual. Reinforcements need a foothold port, still eligible, that the group owns and that is not under siege, so a townless group draws none and can only bleed down while it keeps attacking. A spent group is kept in state only until its kingdom has been retired, since it is what names that kingdom; the next reconciliation then drops it and cancels its remaining captures. Nothing here is persisted: both the offensive switch and the spent condition are derived from live membership and footholds.

## Save compatibility and verification

Existing save IDs and original-spawn meanings are retained. Group, role, foothold, capture evidence, occupation completion and scheduling data are appended. Legacy saves adopt identifiable parties prospectively, without inferring old conquests from clan ownership. Ambiguous capture history gets no retroactive food or troop transfer. Removed configuration or eliminated clans suspend new effects while allowing saved state to be cleaned up.

Compile references remain v1.4.7; installed v1.4.8 is the accepted native verification runtime. Focused tests establish rule, mapping and adapter-boundary behaviour, not live campaign balance. Before treating sample balance as verified, record a new campaign and pre-feature-save load, occupation/deadline reloads, and at least three weekly cycles with food, loyalty, security, militia, garrison strength, donor counts/XP, clan wages and party counts. Each reinforcement must demonstrate the existing 1.5 target-strength and 1.2 roaming-hostile ratios against a real candidate; troop counts alone are not proof.

Runtime verification status and exact build/test evidence belong in the implementation report. Do not infer a successful live playtest from this configuration example.
