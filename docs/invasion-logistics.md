# Invasion logistics

An optional, element-only `Invasion` block in a spawn definition enables invasion occupation and logistics. `RequiresFirstCoastalConquest` separately opts that spawn into coastal-first targeting. Omitting the `Invasion` block preserves existing spawn behaviour. The block belongs to the spawn, not its shared party-behaviour template.

```xml
<Invasion>
  <RequiresFirstCoastalConquest>true</RequiresFirstCoastalConquest>
  <Supplies>
    <FoodStockFloor>100</FoodStockFloor>
    <DailyFoodBonus>5</DailyFoodBonus>
  </Supplies>
  <MaxFactionStrength>900</MaxFactionStrength>
  <StrengthPerTownHeld>300</StrengthPerTownHeld>
  <Reinforcements>
    <Rule>
      <Id>infantry_wave</Id>
      <PartyTemplateId>cs_makko_invasion_reinforcement</PartyTemplateId>
      <IntervalInDays>7</IntervalInDays>
      <MaxConcurrentParties>3</MaxConcurrentParties>
    </Rule>
    <Rule>
      <Id>cavalry_wave</Id>
      <PartyTemplateId>cs_makko_invasion_horse</PartyTemplateId>
      <IntervalInDays>21</IntervalInDays>
      <MaxConcurrentParties>1</MaxConcurrentParties>
    </Rule>
  </Reinforcements>
</Invasion>
```

These are illustrative values, not a guarantee of positive food, rebellion prevention or siege success. No spawn definition in this repository currently carries an `Invasion` block, so the numbers above are a starting point to balance against, not shipped balance. Scouts, ordinary armies, original spawn chances/caps and shared behaviour templates are unchanged.

## Configuration

| Element | Meaning / omitted default |
| --- | --- |
| `RequiresFirstCoastalConquest` | When `true`, restricts the invasion to coastal towns while its current faction owns no coastal town; default `false`. |
| `Supplies/FoodStockFloor` | One-time minimum food stock after occupation, limited by actual capacity; default 0. Existing stock is never reduced. |
| `Supplies/DailyFoodBonus` | Additive daily `Overseas supplies` food-model contribution; default 0. |
| `MaxFactionStrength` | Ceiling on the strength the invasion's clans may field at once, shared by all of its rules; default 0, which leaves it uncapped. |
| `StrengthPerTownHeld` | What every town the invasion's kingdom holds right now adds to that ceiling; default 0. The bonus is the kingdom's, not the invading clans': a town held by an allied clan of the same kingdom raises the ceiling as much as one the invasion took itself, however that clan came by it. Read live, so a town lost stops counting the moment it changes hands. |
| `Reinforcements/Rule` | One reinforcement stream. At least one is required; declare as many as the invasion needs, and they draw in the order written. |
| `Reinforcements/Rule/Id` | Required non-blank name, unique within the invasion. It names the rule's own clock and its own concurrent count, so editing or reordering the configuration does not move a running schedule onto another rule. |
| `Reinforcements/Rule/PartyTemplateId` | Required raw troop-template ID, without the `PartyTemplate.` prefix. Each rule brings its own. |
| `Reinforcements/Rule/IntervalInDays` | Positive interval for both the first and later arrivals of this rule; default 7. |
| `Reinforcements/Rule/MaxConcurrentParties` | Positive cap on this rule's own surviving registered reinforcements in this invasion; default 1. |

All quantities must be finite and nonnegative, and `MaxFactionStrength` is measured in vanilla's own faction strength — the same estimate the game uses, counting the clans' parties on the map **and** the troops sitting in their garrisons. Occupation deliberately dumps the whole escort roster into the captured town's garrison, so a capture eats a large part of the budget immediately; `StrengthPerTownHeld` is what offsets that, and must be sized against garrison strength rather than against a field party. Runtime validation checks each rule's troop-template reference, non-bandit ownership and an effective behaviour template able to besiege towns. Invalid opt-ins are diagnosed without breaking unrelated valid spawn definitions.

A clan may be invaded for once: one faction, one invasion. The clan an invasion invades for is what identifies it — it holds the towns taken, fields the parties and carries the kingdom left behind — so the invaded clan must be settled by the configuration and unique across it. An invasion spawn therefore cannot use `OverridenClanSpawns`, which draws a clan at random on every spawn, nor `InheritClanFromSettlement`, which would make the invading faction whoever owns the landing site; and a second invasion spawn naming a clan an earlier one already invades for is reported and ignored rather than merged into it, because a invasion reads one spawn template's configuration and merging would silently discard the other's. None of this limits what one invasion can do over time: further waves are the same spawn firing again, which join the existing invasion alongside whatever parties are still stranded from an earlier attempt, and several troop streams at once are what the reinforcement rules are for. Reinforcements inherit the original `PriorityPartyBehaviourTemplateId`; there is no second behaviour configuration.

## Invasions, conquests and supply

One logistics invasion belongs to an original spawn-template ID and the clan that founded it, plus any further clan it expands into when that one runs out of war party slots. Several original armies and their escorts can share it. Each town becomes a foothold of the verified capturing party's invasion alone; allied conquests gain no duplicate foothold or supply.

When `RequiresFirstCoastalConquest` is enabled and the invasion party's current faction owns no coastal town, the invasion may begin a siege only at an eligible coastal town with a reachable land gate. Existing hostility, defender-strength, reconnaissance and siege-compatibility checks still apply. No viable coast means the normal raid/patrol fallback, not an inland exception. Until the invasion has taken a town anywhere, owning any coastal town unlocks the configured town/castle targets. The first town it captures becomes its capital, and from then on that one town is what the restriction turns on: while the invasion holds its capital the configured targets are unlocked, and losing it narrows every party's targeting to the capital itself, so the invasion goes back for the town it started from rather than for whichever other port happens to be nearest. Which town the capital is, is saved with the invasion; whether it is still held is read from live ownership.

Ownership loss deletes the foothold and pending occupation effects. Peaceful reacquisition does not restore them. A new attributable siege capture creates a new transition. Assault victories and winning besieger-side sally-out results use native ownership handling; successful occupation promotes the capturing invasion clan into its own kingdom.

Only footholds that are still eligible coastal towns receive overseas food. Siege pauses deliveries without deleting the foothold; they resume when it ends. Later inland town conquests may receive garrison troops but no overseas food. The food decorator preserves the selected underlying model and adds its bonus once through that model. UI/model queries do not replenish stocks or advance timers. A later mod replacing the decorator is diagnosed and requires checking load/model order.

## Occupation and reinforcement lifecycle

Occupation waits until ownership and the actual battle participant snapshot agree, the native garrison is active, and the town and donors have left their map events. One guarded hourly operation merges the escorts into the garrison, applies eligible initial food relief, then records completion. Duplicate callbacks and save/load must not repeat the package.

The transfer is unconditional: every still-active escort party among the capture's recorded participants has its whole `MemberRoster` added to the town's garrison, and the emptied party is then destroyed. There is no configured or capacity-derived bound on the resulting garrison, and no filtering of wounded troops or heroes — the roster moves as it stands. Non-escort participants, parties that are no longer active and other groups are excluded. Native wages, recruitment and desertion remain in force; there is no wage subsidy or weekly garrison top-up.

Every rule's first arrival is due at the invasion's first coastal foothold plus that rule's own interval, and each rule runs its own clock from there, so a stream at its ceiling never holds the others off. A daily check considers each due rule once and chooses one held, eligible, non-besieged port. An arrival uses the land gate, its rule's troop template and a generated leader, fresh AI state, and the original behaviour configuration. It has no `SupportedParty` and creates no escorts. Its creation/death does not change original `MaximumOnMap` accounting. Each landed party remembers which rule sent it, which is what that rule's concurrent count is read from.

All of an invasion's rules draw against one shared strength ceiling. When several come due on the same day they are served in configuration order: the first takes the room it needs, the next ones get what is left, and a rule that finds nothing left spends its slot and waits for its next interval. An arrival worth more than the remaining headroom is not cancelled — it lands cut back to its share, down to a single man, and the party-size limit then fits whatever it actually arrived with, so a trimmed party takes no speed penalty. The ceiling itself is never saved: it is recomputed from the configuration and the towns the invasion's kingdom holds at that moment.

Two paths hand an invasion clan an ordinary lord party under a name no spawn template matches, so that it carries on outside its invasion. The common one runs all campaign: a spawned party is handed to its clan leader as owner, and unless a lord is generated to lead it that clan leader stays its owner without ever standing in a roster, so the game reads it as having no party and fields one for it whenever the clan sits under its tier party limit. A generated besieging lord is not that hero — it both owns and leads the party it was made for, and is enrolled in its roster. The other is a living invasion party changing component, as one does when the invasion becomes a kingdom and loses its only coastal town. Either party is adopted back into its clan's invasion: it is registered under that invasion's spawn template and carries the `"Adopted"` role across saves. That restores its membership, food eligibility, steering and spawn-sized party limit. Its roster is left as the game filled it: adoption restores the invasion identity the party lost, not the troops standing in it.

Adoption is bounded by the invasion's budget -- `MaximumConcurrentPrimaries` plus the `MaxConcurrentParties` of every reinforcement rule added together -- weighed against the invasion's live non-escort members, so a clan that keeps losing lords does not accumulate invasion parties without end. Escorts are outside that count, and an adopted lord spends no landing slot against the reinforcement cap. The bound is on adopting, not on how many parties are alive: the spawner keeps its own separate count.

A due slot blocked by either cap, unavailable port/configuration/template, or spawn failure is skipped until that rule's next interval. There is no backlog or catch-up loop; advancing multiple weeks produces at most one current arrival per rule. Port ownership is rechecked at the spawn call. Membership and the per-rule deadlines persist, while active counts are reconciled from surviving registered parties. A clock whose rule the configuration no longer declares spends its due slot once and lands nothing, rather than being retried daily. Losing the original army does not stop an eligible surviving foothold; a invasion disappears only when it has neither a foothold nor surviving members and the kingdom it leaves behind has been retired.

## Defeat removes the lord

A lord of a clan that is still fighting a live invasion is removed from the campaign the moment it is captured or becomes a fugitive, instead of waiting without a party until its clan's next daily tick hands it a fresh one. Capture, escape and ransom therefore all end the same way, and a ransom needs no handling of its own because the lord is already gone by the time one could be offered. The removal is `KillCharacterAction.ApplyByRemove`: the lord simply disappears, and no relation loss or honor penalty is charged to whoever beat it. `Hero.MainHero`, any hero of the player clan, and any hero whose clan is not fighting a live invasion are never touched.

The trigger is losing the invasion party, however that comes about, and not defeat as such: a party disbanding while stranded with no settlement in reach runs the same fugitive action a lost battle does, so that too removes its lord. A disband that reaches a settlement does not — it destroys the party without announcing a flight. Two shapes therefore escape in the other direction: that settled disband, and a battle ending in a retreat, which annihilates the losing party while marking its lord a fugitive silently. A lord lost either way reaches neither of the two events above and keeps the earlier behaviour of coming back with a fresh party.

Only a living adult lord is removed. An invasion may be configured against an existing clan rather than a spawned one, and retaking a town takes prisoner every hero of the owning clan residing in it, so the clan's spouses, children and quest heroes arrive at the same event a defeated lord does. The test is vanilla's own — the one it uses to pick a successor for a dead clan leader — because a lord's child carries the lord occupation from birth and an occupation alone would not tell the two apart.

Succession is the game's own. While another adult lord of the clan is alive, one is promoted and the clan keeps invading; the defeat that leaves no successor dissolves the clan, which is the invasion ending. How many defeats that takes is not the clan's configured lord count: a spawn that besieges settlements has a lord generated into its clan whenever it spawns without one, so a clan invading that way accumulates adult lords and outlasts correspondingly more defeats. Removing a lord also frees the slot its party held in the adoption budget.

## Losing the foothold

Promotion hands an invasion to vanilla at its capital: while the invasion holds the first town it ever captured, the native AI steers its lords. An invasion that has yet to take that town, or that has lost it, is steered by CustomSpawns and resumes its configured behaviour template, so an invasion that took its capital and then lost it goes back on the offensive instead of sitting still. No other town stands in for it: an invasion holding one of two towns is left to vanilla only while the one it still holds is the capital, and otherwise takes all of its parties back to retake the capital, whatever else it holds. Like the coastal restriction, which town the capital is, is saved with the invasion while whether it is held is read from live footholds, and a suspended invasion — removed configuration, eliminated clan — never counts as live.

A kingdom carrying a live invasion survives the loss of its last settlement. `CanKingdomBeDiscontinuedEvent` is vetoed for any kingdom one of whose clans carries an unsuspended invasion invasion that still has something to fight with, so the kingdom is still there to retake a town with.

While the coastal restriction applies — the initial landing, and every retake that follows a lost capital — the invasion chooses and sizes up its target as one force rather than as whichever party happens to be deciding. Candidate towns are ranked by the summed gate distance of every member, so the invasion converges on the town that is nearest for all of them, and a garrison is weighed against the summed strength of those same members. Escorts are members themselves and are counted once. A invasion with a single party ranks and assesses exactly as it did before, and once the invasion holds its capital every one of its parties is left to native rules anyway, so the question of choosing as one force no longer arises.

An invasion with no surviving member and no foothold is spent, and the kingdom it was given is retired. The veto stops applying at that point, but vanilla only asks whether a kingdom can be discontinued when a settlement changes hands or the player switches kingdom, so the missing trigger is supplied on the hourly tick. The rule that decides is vanilla's own — not eliminated, not the player kingdom, holding no settlement, and the event dispatcher consulted so other mods keep their vote — and so are the actions that end it: each clan leaves as an independent with its map and siege events finalised, the ruling clan is cleared, and the kingdom is destroyed. Departing clans then get the usual native time to survive on their own.

The end is deliberately gradual. Reinforcements need a foothold port, still eligible, that the invasion owns and that is not under siege, so a townless invasion draws none and can only bleed down while it keeps attacking. A capped invasion also loses the `StrengthPerTownHeld` share of every town its kingdom loses, so its ceiling falls with its holdings and it cannot rebuild to the strength it had at its height. A spent invasion is kept in state only until its kingdom has been retired, since it is what names that kingdom; the next reconciliation then drops it and cancels its remaining captures. Only the capital's identity is persisted: both the offensive switch and the spent condition are otherwise derived from live membership and footholds.

## Save compatibility and verification

Existing save IDs and original-spawn meanings are retained. Invasion, role, foothold, capture evidence, occupation completion and scheduling data are appended. Legacy saves adopt identifiable parties prospectively, without inferring old conquests from clan ownership. Ambiguous capture history gets no retroactive food or troop transfer. Removed configuration or eliminated clans suspend new effects while allowing saved state to be cleaned up.

Compile references remain v1.4.7; installed v1.4.8 is the accepted native verification runtime. Focused tests establish rule, mapping and adapter-boundary behaviour, not live campaign balance. The strength ceiling is exercised against plain numbers only: what the game reports as a clan's strength, and how much of a roster a trim actually removes, are reachable only through the adapter and are not pinned by a test. Before treating sample balance as verified, record a new campaign and pre-feature-save load, occupation/deadline reloads, and at least three weekly cycles with food, loyalty, security, militia, garrison strength, donor counts/XP, clan wages and party counts — including one cycle at the ceiling, to confirm a trimmed arrival rather than a cancelled one. Each reinforcement must demonstrate the existing 1.5 target-strength and 1.2 roaming-hostile ratios against a real candidate; troop counts alone are not proof.

Runtime verification status and exact build/test evidence belong in the implementation report. Do not infer a successful live playtest from this configuration example.
