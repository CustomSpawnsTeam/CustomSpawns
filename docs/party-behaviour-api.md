# Priority party behaviour XML API

`PriorityPartyBehaviours` is an ordered behaviour list. The resolver evaluates entries first to last and returns the first applicable result; if none applies, normal CustomSpawns fallback behaviour runs. An entry can be applicable without an AI behaviour, reserving its priority while Bannerlord's active, flee, or engage state proceeds.

| Element | Purpose |
| --- | --- |
| `PatrolAroundClosestLestInterruptedAndSwitch` | Patrol a non-hostile settlement, switching after a rolled stable interval unless interrupted. |
| `RaidClosestVillage` | Find, reconnoitre, approach, and raid an eligible hostile village. |
| `BesiegeClosestSettlement` | Find, reconnoitre, approach, and besiege an eligible hostile town or castle. |

## Behaviour templates

Definitions live in `<submod>/CustomSpawns/PartyBehaviours.xml`, whose root is `PriorityPartyBehaviourTemplates`. A template needs an `Id` and a `PriorityPartyBehaviours` wrapper. Order matters inside the wrapper: this one tries siege, then raid, then patrol. Behaviour configuration is element-only.

```xml
<?xml version="1.0" encoding="utf-8"?>
<PriorityPartyBehaviourTemplates>
  <PriorityPartyBehaviourTemplate>
    <Id>siege-raid-and-patrol</Id>
    <PriorityPartyBehaviours>
      <BesiegeClosestSettlement>
        <TargetTypes><TargetType>Town</TargetType><TargetType>Castle</TargetType></TargetTypes>
        <StrengthAssessment><MinimumAttackerToTargetRatio>1.5</MinimumAttackerToTargetRatio><MinimumAttackerToRoamingHostilesRatio>1.2</MinimumAttackerToRoamingHostilesRatio></StrengthAssessment>
        <Reconnaissance><RequiredClearTimeInDays><Minimum>0.5</Minimum><Maximum>1</Maximum></RequiredClearTimeInDays><SkipWhenTargetStrengthRatio>3</SkipWhenTargetStrengthRatio></Reconnaissance>
        <OperationWindowInDays><Minimum>5</Minimum><Maximum>10</Maximum></OperationWindowInDays>
        <Recovery><GracePeriodInDays>2</GracePeriodInDays><MaximumFailedAttempts>2</MaximumFailedAttempts></Recovery>
        <CooldownInDays>2</CooldownInDays>
      </BesiegeClosestSettlement>
      <RaidClosestVillage>
        <StrengthAssessment><MinimumAttackerToTargetRatio>1.25</MinimumAttackerToTargetRatio><MinimumAttackerToRoamingHostilesRatio>1.1</MinimumAttackerToRoamingHostilesRatio></StrengthAssessment>
        <Reconnaissance><RequiredClearTimeInDays><Minimum>0.5</Minimum><Maximum>1</Maximum></RequiredClearTimeInDays><SkipWhenTargetStrengthRatio>3</SkipWhenTargetStrengthRatio></Reconnaissance>
        <OperationWindowInDays><Minimum>3</Minimum><Maximum>6</Maximum></OperationWindowInDays>
        <Recovery><GracePeriodInDays>1</GracePeriodInDays><MaximumFailedAttempts>2</MaximumFailedAttempts></Recovery>
        <CooldownInDays>2</CooldownInDays>
      </RaidClosestVillage>
      <PatrolAroundClosestLestInterruptedAndSwitch>
        <TargetTypes><TargetType>Village</TargetType><TargetType>Town</TargetType><TargetType>Castle</TargetType></TargetTypes>
        <StableTimeInDays><Minimum>2</Minimum><Maximum>2</Maximum></StableTimeInDays>
      </PatrolAroundClosestLestInterruptedAndSwitch>
    </PriorityPartyBehaviours>
  </PriorityPartyBehaviourTemplate>
</PriorityPartyBehaviourTemplates>
```

IDs are global across every loaded submod and case-sensitive, so prefix them with your submod's short name (CalradiaAtWar uses `caw-`) to avoid collisions. A duplicate ID is reported and the duplicate template dropped. Spawns referencing the same ID share one immutable configuration, so they cannot affect each other. There are no merges or overrides.

## Referencing a template

A spawn opts in with `PriorityPartyBehaviourTemplateId` (element-only) in its `CustomDailySpawn.xml` or `RegularBanditDailySpawn.xml`. Behaviours are never declared inline on a spawn.

```xml
<root>
  <RegularBanditDailySpawnData party_template="example_template_party">
    <PriorityPartyBehaviourTemplateId>siege-raid-and-patrol</PriorityPartyBehaviourTemplateId>
  </RegularBanditDailySpawnData>
</root>
```

A template applies to the main spawned party only. The parties spawned by that spawn's `SpawnAlongWith` escort their main party for as long as it lives, so they never run the template's behaviours; this escort is internal to CustomSpawns and cannot be selected from XML. Once their main party is gone, they fall back to normal fallback behaviour.

The two files are read independently, so a blank or unknown ID cannot be caught at parse time: it is reported once at runtime and the party falls through to normal fallback behaviour. A referenced template whose `PriorityPartyBehaviours` is explicitly empty disables priority behaviour and legacy patrol. The older top-level `PatrolAroundClosestLestInterruptedAndSwitch` on a spawn is retained only when that spawn has no `PriorityPartyBehaviourTemplateId`. The reviewed `CalradiaAtWar/CustomSpawns/` pair of files is a real example.

## Fields and defaults

| Behaviour | Exact element path | Default |
| --- | --- | --- |
| Patrol | `TargetTypes/TargetType` | `Village`, `Town`, `Castle` |
| Patrol | `StableTimeInDays/Minimum`, `Maximum` | `0`, `10` days |
| Raid | `StrengthAssessment/MinimumAttackerToTargetRatio` | `1.25` |
| Raid | `StrengthAssessment/MinimumAttackerToRoamingHostilesRatio` | `1.1` |
| Raid | `Reconnaissance/RequiredClearTimeInDays/Minimum`, `Maximum` | `0.5`, `1` days |
| Raid | `Reconnaissance/SkipWhenTargetStrengthRatio` | `3` |
| Raid | `OperationWindowInDays/Minimum`, `Maximum` | `3`, `6` days |
| Raid | `Recovery/GracePeriodInDays`, `Recovery/MaximumFailedAttempts` | `1` day, `2` |
| Raid | `CooldownInDays` | `2` days |
| Siege | `TargetTypes/TargetType` | `Town`, `Castle` |
| Siege | `StrengthAssessment/MinimumAttackerToTargetRatio` | `1.5` |
| Siege | `StrengthAssessment/MinimumAttackerToRoamingHostilesRatio` | `1.2` |
| Siege | `Reconnaissance/RequiredClearTimeInDays/Minimum`, `Maximum` | `0.5`, `1` days |
| Siege | `Reconnaissance/SkipWhenTargetStrengthRatio` | `3` |
| Siege | `OperationWindowInDays/Minimum`, `Maximum` | `5`, `10` days |
| Siege | `Recovery/GracePeriodInDays`, `Recovery/MaximumFailedAttempts` | `2` days, `2` |
| Siege | `CooldownInDays` | `2` days |

`TargetTypes` syntax is repeated `<TargetType>Village|Town|Castle</TargetType>`. Omit patrol targets for all three. Siege supports only `Town`/`Castle`; omission selects both, while an explicitly empty list is invalid. Raid always targets villages and has no target-types field. An omitted individual nested `Minimum`/`Maximum` retains that behaviour's default.

Every min/max range conceptually rolls a random value in its inclusive range at the relevant episode/target start, and that state is persisted. Patrol's stable time is retained for its target; offensive recon clear time and operation window are retained for the episode. Equal endpoints are fixed.

## Strength and offensive lifecycle

Strength ratios use attacker strength (the party, or its army when it is army leader). `MinimumAttackerToTargetRatio` compares it with target defenders: raid uses village militia; siege uses militia plus garrison plus eligible hostile defending occupants (not villagers, caravans, or duplicated garrison/militia). `MinimumAttackerToRoamingHostilesRatio` compares it with combined hostile roaming strength inside the observer party's Bannerlord `SeeingRange`; there is no XML radius. The observer itself is excluded, as are inactive, settled, and map-event parties.

`SkipWhenTargetStrengthRatio` is attacker/target defenders. When it and the roaming-hostile safety ratio pass, recon is skipped; otherwise clear reconnaissance must accumulate. Safety loss resets recon, and safety loss during approach returns to recon.

Raid/siege runs search, recon, approach, then actual native raid or siege/assault. Search, recon, and approach consume the rolled operation window; Active and Recovery freeze that clock. Recovery has its own bounded `GracePeriodInDays` clock for retrying an interrupted active operation; failed attempts reaching `MaximumFailedAttempts` clear the target. Invalid targets clear immediately. Completion or an expired operation window enters Cooldown, which is outside and freezes the completed episode's operation clock. During the configured `CooldownInDays`, this behaviour is not applicable, so resolution falls through to a later behaviour or fallback; a new episode can start after cooldown ends.

Bannerlord only lets a party besiege a settlement when a hero leads it, so a party spawned against a template containing `BesiegeClosestSettlement` is given a lord generated on the fly from its clan culture's lord template, enrolled at the front of its member roster and persisted with the party. Only the main spawn is given a lord; its `SpawnAlongWith` companions stay leaderless, since they escort it rather than besiege on their own. A clan culture with no lord template is reported once per clan and the party is left leaderless. A bandit-faction spawn never gets a lord, because its party component cannot carry a leader, so do not give bandit spawns a siege template.

A successful siege uses native Bannerlord ownership handling: the settlement goes to the capturing clan/map faction. CustomSpawns does not create or promote a kingdom; kingdom progression is not part of this API.

## Validation

All numeric values must be finite. Ratios are positive. Ranges, grace periods, and cooldowns are non-negative; every range requires `Maximum >= Minimum`; `MaximumFailedAttempts >= 1`; and duplicate behaviour types in one priority list are prohibited.
