# Invasions

An `Invasion` block in a spawn definition turns that spawn into an invasion: a foreign host that lands,
takes a capital, founds a realm and keeps conquering kingdom after kingdom. Without the block a spawn
behaves as it always has. This page is the overview; [Invasion logistics](invasion-logistics.md) holds
every rule in full.

## How an invasion runs

1. **Landing.** The spawn fires like any other, with its escorts. With `FirstConquestMustBeSupplyBase`
   its parties besiege nothing but a coastal town until they hold one. Until it founds its capital it
   besieges nothing the player holds: no fief of the player's clan, nor of the kingdom the player rules
   or serves, as a vassal or a mercenary. It lands at the nearest settlement the player does not hold,
   and with none in reach it raids and patrols until one opens up.
2. **Capital.** The first town it takes becomes its capital and the clan's new kingdom. The escorts
   join the capital's garrison.
3. **The bank.** The capital's garrison is the invasion's troop bank. Each reinforcement rule pays a
   wave of men into it on its own clock, up to the bank limit. Waves come only while the capital is
   held and not besieged; nothing marches across the map.
4. **Lords.** Each wave can bring new lords, up to the lord cap. A lord fills his party from the bank,
   never below `MinimumCapitalGarrison`, and so does a beaten lord coming back. A lord who is captured
   or flees is removed for good.
5. **Holding fiefs.** Every other fief is topped up from the bank to `MinimumFiefGarrison`, and the
   lords lay no new siege while one is short. The invasion's garrisons do not desert over wages or
   party size, and the banked men eat none of the capital's food.
6. **Conquest.** The first kingdom it takes a fief from becomes its target, but never the player's
   kingdom: a fief taken from the player names no target. It stays at war with that kingdom until it
   falls, and its lords prefer that kingdom's sieges. Then it turns on the best prey on its borders,
   which can be the player's kingdom once its line has beaten a kingdom. Every fief it holds raises
   the other kingdoms' wish to declare war on it.
7. **Garrison army.** With `GarrisonArmy`, part of the bank marches out now and then as one besieging
   army under a lord made for it. When that lord is beaten, his men return to the capital.
8. **New realms.** Every kingdom the invasion conquers launches a new invasion realm on a distant
   coast: the next `FollowsInvasion` spawn, or a copy of the first. The realms of a line are allied
   and make war and peace as one. `MaxKingdoms` caps how many stand at once.
9. **Coalition.** Once a line holds a quarter of all fiefs, the other kingdoms make peace among
   themselves, ally and fight it together until it is beaten. The player's kingdom is asked first.
10. **Decline.** A lost capital is retaken, or moved to the nearest fit fief with
    `RelocateCapitalOnLoss`. Without a capital no waves arrive, and losing towns shrinks the bank
    limit and the lord cap. A realm with no fortification left that cannot retake one for two weeks
    collapses.

While an invasion fights, its spawn rolls no new force.

## Configuration

The example is from Calradia at War. Every element is optional except one reinforcement rule.

```xml
<Invasion>
  <FirstConquestMustBeSupplyBase>true</FirstConquestMustBeSupplyBase>
  <RelocateCapitalOnLoss>true</RelocateCapitalOnLoss>
  <MinimumCapitalGarrison>400</MinimumCapitalGarrison>
  <MinimumFiefGarrison>250</MinimumFiefGarrison>
  <LordPartySizeBonus>25</LordPartySizeBonus>
  <Conquest>
    <ThreatPerFiefHeld>0.01</ThreatPerFiefHeld>
    <TargetSiegePreference>4</TargetSiegePreference>
    <ReconsiderTargetIntervalInDays>30</ReconsiderTargetIntervalInDays>
    <MaxKingdoms>3</MaxKingdoms>
    <GarrisonArmy>
      <IntervalInDays>90</IntervalInDays>
      <GarrisonShare>0.5</GarrisonShare>
    </GarrisonArmy>
  </Conquest>
  <MaxCapitalGarrison>2000</MaxCapitalGarrison>
  <CapitalGarrisonPerTownHeld>100</CapitalGarrisonPerTownHeld>
  <MaxLords>6</MaxLords>
  <LordsPerTownHeld>3</LordsPerTownHeld>
  <Reinforcements>
    <Rule>
      <Id>main</Id>
      <PartyTemplateId>cs_makko_invasion_reinforcement</PartyTemplateId>
      <IntervalInDays>7</IntervalInDays>
      <LordsPerWave>3</LordsPerWave>
      <MenPerWave>600</MenPerWave>
      <MenPerTownHeld>100</MenPerTownHeld>
    </Rule>
  </Reinforcements>
</Invasion>
```

| Element | What it sets |
| --- | --- |
| `FirstConquestMustBeSupplyBase` | The first conquest must be a coastal town. |
| `SupplyLine` | `Coastal` (default), `Any` or `None`: which towns can serve as the supply base. |
| `RelocateCapitalOnLoss` | A fallen capital moves to the nearest fit fief instead of being retaken. |
| `MinimumCapitalGarrison` | Men the lords always leave in the capital. |
| `MinimumFiefGarrison` | Men every other fief is topped up to. |
| `LordPartySizeBonus` | Extra men per lord party over vanilla's limit. |
| `MaxCapitalGarrison`, `CapitalGarrisonPerTownHeld` | The bank limit: a base plus a share per town held. |
| `MaxLords`, `LordsPerTownHeld` | The lord cap: a base plus a share per town held. |
| `FollowsInvasion` | Makes this spawn a follower, launched when the named invasion's line conquers a kingdom. |
| `Conquest/ThreatPerFiefHeld` | How much each fief held pushes other kingdoms to war. |
| `Conquest/OpenFrontIntervalInDays` | Fewest days between wars the invasion declares itself; 0 never. |
| `Conquest/TargetSiegePreference` | How much more the lords want the target's sieges. |
| `Conquest/ReconsiderTargetIntervalInDays` | How often the target is weighed against the neighbours. |
| `Conquest/MaxKingdoms` | Most realms of the line standing at once. |
| `Conquest/GarrisonArmy` | `IntervalInDays` between armies and the `GarrisonShare` each takes. |
| `Reinforcements/Rule` | `Id`, `PartyTemplateId`, `IntervalInDays`, `MaxWaves`, `LordsPerWave`, `MenPerWave`, `MenPerTownHeld`. |

An invasion spawn needs a fixed clan of its own: it cannot use `OverridenClanSpawns` or
`InheritClanFromSettlement`, and two invasions cannot share a clan.
