# Invasion logistics

An optional, element-only `Invasion` block in a spawn definition enables invasion occupation and logistics. `RequiresFirstCoastalConquest` separately opts that spawn into coastal-first targeting. Omitting the `Invasion` block preserves existing spawn behaviour. The block belongs to the spawn, not its shared party-behaviour template.

```xml
<Invasion>
  <RequiresFirstCoastalConquest>true</RequiresFirstCoastalConquest>
  <SupplyLine>Coastal</SupplyLine>
  <MinimumCapitalGarrison>400</MinimumCapitalGarrison>
  <MaxFactionStrength>900</MaxFactionStrength>
  <StrengthPerTownHeld>300</StrengthPerTownHeld>
  <Supplies>
    <FoodPerConvoy>200</FoodPerConvoy>
    <IntervalInDays>7</IntervalInDays>
  </Supplies>
  <Reinforcements>
    <Rule>
      <Id>infantry_wave</Id>
      <PartyTemplateId>cs_makko_invasion_reinforcement</PartyTemplateId>
      <IntervalInDays>7</IntervalInDays>
      <LordsPerWave>3</LordsPerWave>
    </Rule>
    <Rule>
      <Id>cavalry_wave</Id>
      <PartyTemplateId>cs_makko_invasion_horse</PartyTemplateId>
      <IntervalInDays>21</IntervalInDays>
    </Rule>
  </Reinforcements>
</Invasion>
```

These are illustrative values, not a guarantee of rebellion prevention or siege success. No spawn definition in this repository currently carries an `Invasion` block, so the numbers above are a starting point to balance against, not shipped balance. Scouts, ordinary armies, original spawn chances/caps and shared behaviour templates are unchanged.

## Configuration

| Element | Meaning / omitted default |
| --- | --- |
| `RequiresFirstCoastalConquest` | When `true`, restricts the invasion to coastal towns while its current faction owns no coastal town; default `false`. |
| `SupplyLine` | Where the invasion's supplies may come from: `Coastal`, `Any` or `None`; omitted, supplies come by sea, as they did before there was a choice. `Coastal` makes a coastal town the only supply base, `Any` any town, and `None` removes the dependency altogether — nothing has to be retaken for supply's sake. Supply decides which capture winds the reinforcement clocks and whether the first-coastal-conquest restriction applies; it never decides whether reinforcements come, which turns on the capital alone. |
| `MinimumCapitalGarrison` | How many men the invasion keeps standing in its capital: the floor its lords draw that garrison down to but never below; default 0. It says nothing about any other town the invasion holds. |
| `MaxFactionStrength` | Ceiling on the strength the invasion's clans may field at once, shared by all of its rules; default 0, which leaves it uncapped. |
| `StrengthPerTownHeld` | What every town the invasion's kingdom holds right now adds to that ceiling; default 0. The bonus is the kingdom's, not the invading clans': a town held by an allied clan of the same kingdom raises the ceiling as much as one the invasion took itself, however that clan came by it. Read live, so a town lost stops counting the moment it changes hands. |
| `Supplies/FoodPerConvoy` | How much food each convoy unloads into the capital's own market; default 0, which runs no convoys at all and leaves the capital feeding itself. Size it against what the capital's garrison eats, since that garrison is the invasion's troop bank and is the reason the town needs more food than it produces. |
| `Supplies/IntervalInDays` | How often a convoy arrives; default 7, minimum 1. Its own cadence — changing how often the invasion is reinforced leaves its supplies alone, and the other way round. Ignored when no food is configured. |
| `Reinforcements/Rule` | One reinforcement stream. At least one is required; declare as many as the invasion needs, and they draw in the order written. |
| `Reinforcements/Rule/Id` | Required non-blank name, unique within the invasion. It names the rule's own clock, so editing or reordering the configuration does not move a running schedule onto another rule. |
| `Reinforcements/Rule/PartyTemplateId` | Required raw troop-template ID, without the `PartyTemplate.` prefix. Each rule brings its own. |
| `Reinforcements/Rule/IntervalInDays` | Interval for both the first and later waves of this rule; default 7, minimum 1. Waves are considered once a day, so a shorter interval is refused rather than silently rounded. |
| `Reinforcements/Rule/MaxWaves` | How many waves this rule ever sends; default 0, which leaves it sending for as long as the invasion lives. One wave plus an interval of one day is the one-off reinforcement: it arrives the first day after the capital falls and nothing follows it. Only waves actually paid into the garrison count, so one refused for a besieged capital or a full strength ceiling is sent an interval later instead of being lost. |
| `Reinforcements/Rule/LordsPerWave` | How many new lords join the invasion with each wave of this rule; default 0, a wave of troops only. Each fills his party out of the capital's garrison. |

All quantities must be finite and nonnegative, and `MaxFactionStrength` is measured in vanilla's own faction strength — the same estimate the game uses, counting the clans' parties on the map **and** the troops sitting in their garrisons. Occupation deliberately dumps the whole escort roster into the captured town's garrison, so a capture eats a large part of the budget immediately; `StrengthPerTownHeld` is what offsets that, and must be sized against garrison strength rather than against a field party. Runtime validation checks each rule's troop-template reference, non-bandit ownership and an effective behaviour template able to besiege towns. Invalid opt-ins are diagnosed without breaking unrelated valid spawn definitions.

A clan may be invaded for once: one faction, one invasion. The clan an invasion invades for is what identifies it — it holds the towns taken, fields the parties and carries the kingdom left behind — so the invaded clan must be settled by the configuration and unique across it. An invasion spawn therefore cannot use `OverridenClanSpawns`, which draws a clan at random on every spawn, nor `InheritClanFromSettlement`, which would make the invading faction whoever owns the landing site; and a second invasion spawn naming a clan an earlier one already invades for is reported and ignored rather than merged into it, because a invasion reads one spawn template's configuration and merging would silently discard the other's. None of this limits what one invasion can do over time: further waves are the same spawn firing again, which join the existing invasion alongside whatever parties are still stranded from an earlier attempt, and several troop streams at once are what the reinforcement rules are for. Lords brought by a wave inherit the original `PriorityPartyBehaviourTemplateId` along with the rest of the invasion; there is no second behaviour configuration.

## Invasions, conquests and supply

One logistics invasion belongs to an original spawn-template ID and the clan that founded it, plus any further clan it expands into when that one runs out of war party slots. Several original armies and their escorts can share it. Each town becomes a foothold of the verified capturing party's invasion alone; allied conquests gain no duplicate foothold or supply.

When `RequiresFirstCoastalConquest` is enabled and the invasion party's current faction owns no coastal town, the invasion may begin a siege only at an eligible coastal town with a reachable land gate. Existing hostility, defender-strength, reconnaissance and siege-compatibility checks still apply. No viable coast means the normal raid/patrol fallback, not an inland exception. Until the invasion has taken a town anywhere, owning any coastal town unlocks the configured town/castle targets. The first town it captures becomes its capital, and from then on that one town is what the restriction turns on: while the invasion holds its capital the configured targets are unlocked, and losing it narrows every party's targeting to the capital itself, so the invasion goes back for the town it started from rather than for whichever other port happens to be nearest. Which town the capital is, is saved with the invasion; whether it is still held is read from live ownership.

Ownership loss deletes the foothold and pending occupation effects. Peaceful reacquisition does not restore them. A new attributable siege capture creates a new transition. Assault victories and winning besieger-side sally-out results use native ownership handling; successful occupation promotes the capturing invasion clan into its own kingdom.

An invasion supplies its capital by convoy, and by nothing else. Every `IntervalInDays` a convoy unloads `FoodPerConvoy` worth of grain into the capital's own market, and the town then lives off it exactly as it lives off anything a trader brings in: hunger, loyalty and rebellion stay vanilla's to work out, and nothing writes a food balance or grants a town an allowance. That is the whole of it — no other town the invasion holds is supplied, and a town it takes and does not make its capital lives on what it produces, like any other town that changes hands.

This replaces the food support the invasion used to have, which stocked a captured town once and then fed it daily. A flat allowance either left a conquest short, which dropped its loyalty and invited the rebellion the support was meant to prevent, or was generous enough that nothing the invasion held ever ran short at all. A convoy has neither problem: it is a quantity of real food arriving at a known rate, so a capital whose garrison outgrows the convoy goes hungry on vanilla's terms, and the men in the bank are what the hunger is charged against.

The first convoy is due an interval after the capital falls, not on the day of it, because the clock is wound by the capture rather than spent by it. While the capital is lost or besieged the clock holds where it is instead of running on — a convoy cannot be unloaded into a town the invasion cannot reach, and a siege is already starving that town by vanilla's own rules — so whatever was due is unloaded once the way in is open again. That is deliberately the opposite of a reinforcement wave, which spends a slot it could not deliver: a wave refused is one the invasion can afford to lose, and a convoy refused is food it was going to need either way.

What a foothold's supply status still decides is which capture winds the reinforcement clocks, and which settlement the invasion is sent back to retake when it loses one.

## Occupation and reinforcement lifecycle

Occupation waits until ownership and the actual battle participant snapshot agree, the native garrison is active, and the town and donors have left their map events. One guarded hourly operation merges the escorts into the garrison, then records completion. Duplicate callbacks and save/load must not repeat the package.

The transfer is unconditional: every still-active escort party among the capture's recorded participants has its whole `MemberRoster` added to the town's garrison, and the emptied party is then destroyed. There is no configured or capacity-derived bound on the resulting garrison, and no filtering of wounded troops or heroes — the roster moves as it stands. Non-escort participants, parties that are no longer active and other groups are excluded. Native wages, recruitment and desertion remain in force; there is no wage subsidy or weekly garrison top-up.

Every rule's first wave is due at the capture that gave the invasion its capital plus that rule's own interval, and each rule runs its own clock from there, so a stream at its ceiling never holds the others off. A daily check considers each due rule once. A wave has one destination: the capital's own garrison, which is the invasion's troop bank. The rule's troop template is rolled once per kind of troop in it, between the numbers the template declares, and paid straight in — nothing marches across the map, no party is created, and the garrison takes the wave whole however large the bank has grown. The rule's `LordsPerWave` lords are then generated into the invasion's clans and field their parties at the capital, drawing their men back out of that same garrison down to `MinimumCapitalGarrison` and no further. A lord beaten and come back draws the same way, which is what makes the bank the invasion's real strength: an empty bank sends him out alone to recruit Calradians.

Nothing arrives unless the invasion holds its capital and that capital is not under siege. An invasion that has yet to take a capital, or that has lost the one it took, is sent nothing and fights on with what it has; a besieged capital is cut off both ways, so no wave is paid in and no lord takes any out either. The clock runs through all of it — a wave refused is not banked up to be paid later — but a refused wave does not count against `MaxWaves`, so a rule allowed only so many waves still owes the ones it could not deliver.

All of an invasion's rules draw against one shared strength ceiling, which is the only valve on the bank: the bank itself is bottomless. When several rules come due on the same day they are served in configuration order, each decided only after the one before it has been paid in, so the first takes the room and a later one can find none left and wait for its next interval. The ceiling itself is never saved: it is recomputed from the configuration and the towns the invasion's kingdom holds at that moment.

Two paths hand an invasion clan an ordinary lord party under a name no spawn template matches, so that it carries on outside its invasion. The common one runs all campaign: a spawned party is handed to its clan leader as owner, and unless a lord is generated to lead it that clan leader stays its owner without ever standing in a roster, so the game reads it as having no party and fields one for it whenever the clan sits under its tier party limit. A generated besieging lord is not that hero — it both owns and leads the party it was made for, and is enrolled in its roster. The other is a living invasion party changing component, as one does when the invasion becomes a kingdom and loses its only coastal town. Either party is adopted back into its clan's invasion: it is registered under that invasion's spawn template and carries the `"Adopted"` role across saves. That restores its membership, steering and spawn-sized party limit. Its roster is left as the game filled it: adoption restores the invasion identity the party lost, not the troops standing in it.

Adoption counts nothing and refuses nothing: any lord party of a clan the invasion holds is taken back, however many the invasion already fields. There is no need to count, because the invasion's own clan membership already says which parties are its, and how much it may field is the strength ceiling's question — the one valve on the whole system. Garrisons, caravans and militias of the same clan are not lord parties and are never adopted, and a party already registered keeps the identity it was registered with.

A due slot blocked by an unavailable capital, a full ceiling, or a configuration or troop template that has changed under it is skipped until that rule's next interval. There is no backlog or catch-up loop; advancing multiple weeks produces at most one current wave per rule. Capital ownership and siege are rechecked at the moment the wave would be paid in. Membership, the per-rule deadlines and the waves each rule has already sent all persist. A clock whose rule the configuration no longer declares spends its due slot once and banks nothing, rather than being retried daily. Losing the original army does not stop an eligible surviving foothold; a invasion disappears only when it has neither a foothold nor surviving members and the kingdom it leaves behind has been retired.

## Defeat removes the lord

A lord of a clan that is still fighting a live invasion is removed from the campaign the moment it is captured or becomes a fugitive, instead of waiting without a party until its clan's next daily tick hands it a fresh one. Capture, escape and ransom therefore all end the same way, and a ransom needs no handling of its own because the lord is already gone by the time one could be offered. The removal is `KillCharacterAction.ApplyByRemove`: the lord simply disappears, and no relation loss or honor penalty is charged to whoever beat it. `Hero.MainHero`, any hero of the player clan, and any hero whose clan is not fighting a live invasion are never touched.

The trigger is losing the invasion party, however that comes about, and not defeat as such: a party disbanding while stranded with no settlement in reach runs the same fugitive action a lost battle does, so that too removes its lord. A disband that reaches a settlement does not — it destroys the party without announcing a flight. Two shapes therefore escape in the other direction: that settled disband, and a battle ending in a retreat, which annihilates the losing party while marking its lord a fugitive silently. A lord lost either way reaches neither of the two events above and keeps the earlier behaviour of coming back with a fresh party.

Only a living adult lord is removed. An invasion may be configured against an existing clan rather than a spawned one, and retaking a town takes prisoner every hero of the owning clan residing in it, so the clan's spouses, children and quest heroes arrive at the same event a defeated lord does. The test is vanilla's own — the one it uses to pick a successor for a dead clan leader — because a lord's child carries the lord occupation from birth and an occupation alone would not tell the two apart.

Succession is the game's own. While another adult lord of the clan is alive, one is promoted and the clan keeps invading; the defeat that leaves no successor dissolves the clan, which is the invasion ending. How many defeats that takes is not the clan's configured lord count: a spawn that besieges settlements has a lord generated into its clan whenever it spawns without one, so a clan invading that way accumulates adult lords and outlasts correspondingly more defeats.

## Losing the foothold

Promotion hands an invasion to vanilla at its capital: while the invasion holds the first town it ever captured, the native AI steers its lords. An invasion that has yet to take that town, or that has lost it, is steered by CustomSpawns and resumes its configured behaviour template, so an invasion that took its capital and then lost it goes back on the offensive instead of sitting still. No other town stands in for it: an invasion holding one of two towns is left to vanilla only while the one it still holds is the capital, and otherwise takes all of its parties back to retake the capital, whatever else it holds. Like the coastal restriction, which town the capital is, is saved with the invasion while whether it is held is read from live footholds, and a suspended invasion — removed configuration, eliminated clan — never counts as live.

A kingdom carrying a live invasion survives the loss of its last settlement. `CanKingdomBeDiscontinuedEvent` is vetoed for any kingdom one of whose clans carries an unsuspended invasion invasion that still has something to fight with, so the kingdom is still there to retake a town with.

While the coastal restriction applies — the initial landing, and every retake that follows a lost capital — the invasion chooses and sizes up its target as one force rather than as whichever party happens to be deciding. Candidate towns are ranked by the summed gate distance of every member, so the invasion converges on the town that is nearest for all of them, and a garrison is weighed against the summed strength of those same members. Escorts are members themselves and are counted once. A invasion with a single party ranks and assesses exactly as it did before, and once the invasion holds its capital every one of its parties is left to native rules anyway, so the question of choosing as one force no longer arises.

An invasion with no surviving member and no foothold is spent, and the kingdom it was given is retired. The veto stops applying at that point, but vanilla only asks whether a kingdom can be discontinued when a settlement changes hands or the player switches kingdom, so the missing trigger is supplied on the hourly tick. The rule that decides is vanilla's own — not eliminated, not the player kingdom, holding no settlement, and the event dispatcher consulted so other mods keep their vote — and so are the actions that end it: each clan leaves as an independent with its map and siege events finalised, the ruling clan is cleared, and the kingdom is destroyed. Departing clans then get the usual native time to survive on their own.

The end is deliberately gradual. Reinforcements need the capital itself, held and not under siege, so an invasion that has lost it draws none and can only bleed down while it fights to take it back. A capped invasion also loses the `StrengthPerTownHeld` share of every town its kingdom loses, so its ceiling falls with its holdings and it cannot rebuild to the strength it had at its height. A spent invasion is kept in state only until its kingdom has been retired, since it is what names that kingdom; the next reconciliation then drops it and cancels its remaining captures. Only the capital's identity is persisted: both the offensive switch and the spent condition are otherwise derived from live membership and footholds.

## Save compatibility and verification

Existing save IDs and original-spawn meanings are retained. Invasion, role, foothold, capture evidence, occupation completion and scheduling data are appended. Legacy saves adopt identifiable parties prospectively, without inferring old conquests from clan ownership. Ambiguous capture history gets no retroactive troop transfer. Removed configuration or eliminated clans suspend new effects while allowing saved state to be cleaned up.

Compile references remain v1.4.7; installed v1.4.8 is the accepted native verification runtime. Focused tests establish rule, mapping and adapter-boundary behaviour, not live campaign balance. The strength ceiling is exercised against plain numbers only: what the game reports as a clan's strength, and what a template roll actually puts into a garrison, are reachable only through the adapter and are not pinned by a test. Before treating sample balance as verified, record a new campaign and pre-feature-save load, occupation/deadline reloads, and at least three weekly cycles with loyalty, security, militia, garrison strength, donor counts/XP, clan wages and party counts — including one cycle at the ceiling, to confirm the wave is refused and sent later rather than lost, and one with the capital besieged, to confirm neither a wave in nor a lord's draw out.

Runtime verification status and exact build/test evidence belong in the implementation report. Do not infer a successful live playtest from this configuration example.
