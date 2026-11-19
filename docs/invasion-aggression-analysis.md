# Why the Humar invasion stalls, and how to make it aggressive with vanilla AI

Taken from the live CalradiaAtWar campaign on 2026-10-13 (game date Spring 12, 1093), the CustomSpawns logs, and
decompiled 1.4.x vanilla code. It covers only levers that work through vanilla kingdom and party AI: nothing here
forces CustomSpawns party behaviour (such as `BesiegeClosestSettlementBehaviour`).

## What the campaign shows

| | Humar | Everyone else |
|---|---|---|
| Fiefs | Charas only. Usanc Castle has since been retaken by the Western Empire (garrison 78) | 13–17 each |
| Strength | 4,941 | 9.3k–18.1k **per kingdom**, about 106k in total |
| Wars | at war with all 8 kingdoms | each at war with the Humar only |
| Armies | none, ever | — |
| Clans with a fief | 1 of 14 (Humar) | — |
| Lord party sizes | 7–104 men, mostly 20–30 | — |

Taking a coastal town and a castle gave the Humar one base. It did not give them an army that grows. Every other
kingdom is at peace with everyone else, so all of them fight only the Humar.

## The vanilla gates that keep the Humar passive

1. **No armies (the main cause).** `DefaultArmyManagementCalculationModel.CanLordCreateArmy` requires all of these:
   - clan influence > 100
   - the leader's `PartySizeRatio` > 0.6 (`AIMobilePartySizeRatioToCallToArmy`)
   - food for more than 12 days
   - at least 1,000 combined siege strength

   Members are called only when their own ratio is > 0.6 and they have food for more than 15 days. A Humar lord with
   25 men against a limit of about 100–150 sits at a ratio of 0.15–0.25, so no Humar lord can lead an army or be
   called into one.

2. **The party-size penalty also suppresses solo sieges.** In `AiMilitaryBehavior`, every target score is
   multiplied by `partySizeScore = min(1, (PartySizeRatio / normalLimit)^k)`, where k = clamp(towns/5, 1, 2.5).
   With ratios around 0.2, besiege and raid scores are crushed, so the lords patrol, defend Charas, or wander.

3. **13 of 14 clans can never refill.** `GarrisonTroopsCampaignBehavior` lets a lord take garrison troops only when
   `LeaderHero.Clan == Settlement.OwnerClan`. Charas belongs to the Humar clan, so the bank of about 1,100 men
   feeds only that clan and lords at the moment they are created. Every other clan's lords shrink with each fight
   and stay small.

4. **Wages pay for current size, not target size.** The waves pay `dailyWages × interval`. A party's ideal size is
   capped by `PaymentLimit / AverageWage` and by food, so clans never get the gold to grow past their current
   size.

5. **Diplomacy.** Eight kingdoms with roughly 20 times the Humar's strength all point their armies at the Humar's
   only fief.

## Levers, in order of impact for the effort

| # | Lever | Mechanism | Effort |
|---|---|---|---|
| 1 | **Fill lords to at least 70% of their party size limit from the bank**, whichever clan they belong to, when each wave lands. | Unlocks rules 1 and 2: armies become possible and siege scores stop being crushed. This is the one change that alone moves the AI. | Small: the bank already exists; drop the owner-clan restriction for invasion lords. |
| 2 | **Give each clan influence with each wave** (for example 150 for each clan with a lord in the field, capped). | Clears influence > 100 and pays the call-to-army costs (about 20 each; same-clan parties are free). | Trivial: `ChangeClanInfluenceAction.Apply`. |
| 3 | **Pay wages and food for the target size** (lords' party size limit × average wage), and top up lord food above 15 days. | Keeps lever 1 from decaying, and keeps lords eligible for armies. | Small change to `PayWages`. |
| 4 | **Give each landless clan a castle when one is taken** (vanilla fief distribution, or a direct grant). | Makes the vanilla garrison refill work natively, and raises `k` and the "neighbouring fief" distance bonus. | Medium: decide who gets what. |
| 5 | **`MobileParty.Objective = Aggressive`** on Humar lord parties. | Vanilla field: ×1.2 on besiege and raid scores. Cheap but modest; worthless without lever 1. | Trivial. |
| 6 | **Split the world's attention**: start a war or two between the bordering kingdoms when the invasion lands, through vanilla `DeclareWarAction`, or by lowering their peace barriers. | Fewer armies converge on Charas; neighbours fight each other the way they would in a vanilla campaign. | Small, but it changes the world for everyone, so it should be optional in the config. |
| 7 | (Optional) a Humar-scoped `ArmyManagementCalculationModel` or `TargetScoreCalculatingModel` subclass that lowers the 0.6 ratio or the size exponent for invasion kingdoms. | Same effect as lever 1 without the troops. It is a model override, not a CustomSpawns behaviour override, but it touches every kingdom's model path. | Medium, and vulnerable to model ordering against other mods. |

## Recommendation

Do levers 1 to 3 together: fill to 70%, give influence, and pay for the target size. That is the minimum set that
lets vanilla form a Humar army. Each lever alone fails another gate in `CanLordCreateArmy`. Add lever 5 because it
is cheap. Treat lever 6 as a configuration option for the "deadly" preset.

To check that it works, in a live run after one or two waves:

- `kingdom.get_kingdom new_kingdom` shows `armies` that aren't empty.
- The behaviour histogram (`hb.py`) shows `BesiegeSettlement` from lord parties, not only from CS spawns.
- A second fief is taken within about 20 days of the first.

## Data still unverified

The game was paused (blocker `paused` on the campaign map after the player's capture) while I was collecting this,
so these values were not read live:

- each clan's influence
- each lord's exact `PartySizeLimit` and `PaymentLimit`
- food days

The influence gate is therefore inferred, not measured. The party size gate is certain from the observed counts.
