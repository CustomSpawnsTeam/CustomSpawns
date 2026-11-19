# Invasion gathering as a domain concept

Status: implemented, unit tested, not yet playtested. Two deviations from the design as written are
recorded under "What was built".

## The problem

An invasion's parties converge on one town today, but nothing in the code says so. Convergence is
emergent, out of two implicit mechanisms in the Bannerlord-facing targeting policy
(`InvasionCoastalSiegeTargetPolicy`):

1. **A shared ranking.** A candidate's cost is the deciding party's distance *plus every other
   invasion party's distance to the same gate*. All parties compute the same sum, so they
   independently pick the same winner. Nothing coordinates them; they coincide.
2. **A proximity window.** A garrison is weighed against this party plus any invasion party within
   `GatheringRadius` (30 map units) or sharing its siege camp, whichever total is greater.

Three consequences:

- **The group is guessed, not stated.** Each party already saves the settlement it is working on
  (`SettlementOffensiveBehaviourState.Target`), so the set of parties on one objective is available
  exactly. The distance sum and the 30-unit window exist only because nobody ever asked.
- **Both switch off together with an unrelated flag.** `GetLandingForce` returns only the deciding
  party when the first-conquest filter is null, so an invasion configured without
  `RequiresFirstCoastalConquest` -- and now every `None` invasion, permanently -- besieges one party
  at a time and weighs garrisons against single parties. This matches the Calradia-at-war playtest
  where a town was taken and then lost to the first vanilla army.
- **The strength test runs at the wrong moment.** A party only selects a target it can beat *alone*
  (`FindClosestSiegeTarget` filters candidates by `MeetsStrengthRatio` before any pooling is
  consulted). A weak party therefore never chooses the hard target, so the group that would have
  taken it never forms.

## The model

**Choosing a target is joining an objective.** The target stays each party's own choice; what
changes is that the choice is recorded. The invasion holds which parties are on which objective, and
gathering runs over that recorded set rather than over whoever happens to be standing nearby.
Several objectives may run at once, and a party far from the rest can still pursue its own.

Rejected on the way: one objective per invasion. It makes the group trivially explicit but forbids
an invasion from besieging two places at once, which is a real loss of aggression and was never
asked for.

**Joining needs a bias, because the distance sum is what used to create agreement.** Without it each
party takes its own nearest candidate and they scatter, so a gather would almost never form. Rule:
a party prefers an objective its siblings are already on, unless its own nearest candidate is
clearly closer -- taken as the sibling objective being within roughly 1.5x the distance of its own
best candidate, and never further than a kingdom away. Near parties converge; a party on the far
side of the map does not trek across; and nothing is decreed.

The kingdom bound is what stops the relative rule from dragging a party across the map when both
its options happen to be distant. Measured on vanilla Calradia, grouping fortifications by owning
kingdom: a kingdom spans 145 (Battania) to 414 (Aserai) end to end, median 240, and the median
distance between two fortifications of the same kingdom is 70 to 175. The widest fort-to-fort
distance on the map is 677. So a bound around 250 covers every vanilla kingdom's own territory
except Sturgia and the Aserai and is about a third of the map's reach -- generous enough to never
block a plausible gather, tight enough to be a real limit.

**Three questions, not one radius.** Today `GatheringRadius` (30) is asked all three at once; they
want separate answers:

| question | answer |
| --- | --- |
| who belongs to the force | the recorded objective set -- no distance at all |
| who may be tempted onto a sibling's objective | kingdom scale, ~250 (above) |
| whose troops count at the assault | the parties that have arrived at the gate |

The last one is the one that must stay tight, and it is better asked about the target than about
each other: a party counts once it is within its own sight radius of the land gate, the radius
`GetHostileLordStrengthNearTarget` already uses to judge an approach. That needs no tuning constant
and does not depend on how densely a given map is settled. A kingdom-scale number in that slot would
launch the assault on paper strength while the troops are still a province away, which is the
Calradia-at-war failure moved one step earlier.

**One strength test, at the assault.** Committing to an objective does not consider strength at all:
the existing ratio moves to the decision to leave the hold and attack, measured over the parties
that have arrived. A map-wide commitment test was considered and dropped -- with several objectives
alive it would count parties committed elsewhere toward this town, and it buys nothing the give-up
window does not already cover.

**Gathering happens short of the objective, not at a rally settlement.** Parties march on the target
and hold at standoff distance until the arrived force passes the ratio. No village is chosen:
villages belong to their town, so the nearest one to an enemy town is normally the enemy's, and an
invasion at war with the whole map (Humar, Makko) has no non-hostile village anywhere. A named rally
can also sit *behind* the parties, making them walk away from the objective and back. Holding short
of the target is always forward travel, needs no ownership test, and reuses the scouting radius
already used to judge an approach.

**Latecomers join for free.** Once the assault starts, arriving parties join the siege in progress.
`IsJoinableSiege` already does this; it has to key off the recorded objective instead of the landing
group.

**Independent of supply doctrine, and not configurable.** Gathering is what an invasion *is*, so it
is on for every party of a live invasion and has no XML surface: the two numbers are constants in the
targeting policy. The first-conquest flag goes back to doing only the job it is named for.

## Assumptions

- **Raiding stays suppressed while gathering.** Behaviours are tried in configuration order and the
  first applicable one wins (`PartyBehaviourResolver.Resolve`), so the siege behaviour must keep
  reporting itself applicable through the hold, as it already does while scouting. Otherwise waiting
  parties peel off to villages one at a time and the gather never completes. The cost is that a
  waiting force stops pillaging the countryside.
- **A gather gives up.** Since commitment ignores strength, a party can commit to something no
  sibling ever joins. The hold therefore runs on the existing operation window
  (`OperationWindowDays` / `EnterCooldown`): if the force has not arrived in time, the objective is
  dropped and reselected.

## Shape of the change

The per-party phase machine already has the right structure -- `Idle -> Search -> Recon -> Approach
-> Active -> Cooldown`, saved per party, with scouting already a "hold and accumulate days before
committing" phase. Gathering is a phase of the same shape, and the enum has a free slot at 5.

1. **Objectives become recorded state.** The invasion holds which parties are on which objective,
   written when a party selects a target and cleared when it drops one.
   *Verify:* two parties of one invasion on one town report one objective with both of them on it;
   a third on another town is a separate objective, not a member of the first.
2. **Bias the choice toward a sibling objective.** The 1.5x rule plus the kingdom bound above
   replaces the whole-invasion distance sum.
   *Verify:* a party whose own nearest candidate is marginally closer joins the siblings; one whose
   nearest is far closer does not; one whose sibling objective is beyond the kingdom bound does not,
   however much closer nothing else is.
3. **Move the strength test to the assault.** Objective selection stops filtering on the ratio; the
   ratio gates leaving the hold, over the parties that have arrived at the gate.
   *Verify:* a party too weak alone still commits; it does not attack until the arrived force passes;
   a sibling committed to the same objective but still marching does not count toward the launch.
4. **Add the gather phase.** Hold at standoff distance while the arrived force is short, launch when
   it passes, abandon when the operation window runs out.
   *Verify:* arrival of a second party flips a held gather into an assault; expiry drops the objective.
5. **Delete the implicit mechanisms.** Remove the distance sum and the 30-unit window; point
   `IsJoinableSiege` at the recorded objective.
   *Verify:* existing invasion siege tests pass against the explicit group.
6. **No configuration.** Gathering is tied to the domain: every party of a live, unsuspended invasion
   gathers, independent of `SupplyLine` and of `RequiresFirstCoastalConquest`. The only question the
   public API gained is that on/off one (`IInvasionLandingQuery.ServesLiveInvasion`).
   *Verify:* a coastal invasion with the first-conquest flag off now gathers; a party that serves no
   live invasion keeps the old ranking and the old strength filter.

## What was built

All six steps are in, the solution builds at its warning baseline, and the suite is green apart from
the one pre-existing `MissingConfiguredClanIds_AreCollectedWithoutFirstFailure` failure.

Where it differs from the design above:

- **No new save data was needed for step 1.** `SettlementOffensiveBehaviourState.Target` is already a
  saved field, so the objective set is read out of the siege behaviour's own store instead of being
  held a second time by the invasion. `Main` now owns that store and hands the policy a lookup into
  it, which is what breaks the construction cycle between the two.
- **`IsJoinableSiege` still keys off an actual running siege**, by any live member of the invasion,
  rather than off the recorded objective as step 5's wording asks. It never used the 30-unit window,
  and "joinable" has to mean a siege event exists: keying it to a sibling's mere intention would
  corrupt `HasCompatibleSiege` and `IsCurrentTargetValid`, which ask whether there is something to
  join right now.
- **The hold needed no standoff constant and no new give-up code.** It reuses the patrol-around-point
  order on the target, which is forward travel, and the existing operation window already ends a
  gather nobody joins.
- **Three decisions, three different strengths.** The ratio that skips reconnaissance asks the pooled
  figure: everything committed to the objective, whatever its distance. Selection cannot ask it, for
  the plain reason that there is no objective to pool around at the moment of choosing -- a party
  picking a town weighs what it has before it commits to anything, and only once the objective is
  recorded is the figure re-read as the pooled one for the decisions that follow in the same tick.
  The one exception is a party that is already standing in a siege camp when it chooses: its camp
  mates pool whether an objective is recorded or not, because they are with it in fact rather than by
  intention, and that is the state a party is left in when the town it was besieging stops being a
  target without the camp going away.
  The assault
  gate and the withdrawal of a camp already standing at the town ask the arrived figure: only the
  parties within their own sight of the land gate, with nothing standing in for those still marching.
  Reconnaissance safety asks the greater of what the party has with it and what the force has standing
  at the gate. It cannot use the pooled figure -- an objective is recorded with no regard to distance,
  so siblings a province away would talk a lone party into marching past a stack it cannot survive --
  and it cannot use arrival alone, which reads zero while the party is still marching and takes the
  objective away before anyone can reach it, as the first implementation did.
- **The arrived figure counts nobody who cannot fight at the walls.** A party resting inside a
  settlement and a party pinned in a battle of its own are both left out. Leaving out the pinned ones
  is what keeps the sum symmetric: the scan that sizes up a relief force skips anybody already in a
  battle too, so counting one side of that fight and not the other is what made a camp sit still while
  its siblings were eaten. The cost is that an army member which is neither a force member nor an
  escort -- a vanilla lord party travelling inside the invasion's army -- no longer counts at the gate.
  That under-counts, so it can only delay an assault, never launch one on troops who are not there.
- **The kingdom bound removes the join bias rather than vetoing the objective.** Beyond 250 a sibling's
  objective is ranked at its true distance, so a stranded party with nothing else in reach still picks
  it. Step 2's verify wording asks for a veto; the implementation only declines to discount. The
  outcome is the same as plain nearest-target selection, which is the behaviour the bound exists to
  fall back to.
- **Both gates now weigh the same town, from the same place.** A camp deciding whether to pack up was
  reading a different figure than the one that let it set off: the walls alone, counted from wherever
  the deciding party happened to stand, with every cart and caravan on the road counted as a relief
  force. It now reads what the launch gate read -- walls, militia and the lords sheltering inside --
  and counts a relief force only near the town it would relieve, with trade and peasant traffic left
  out, exactly as they are left out of the defenders inside. The two gates can no longer disagree
  about the same place, and a merchant train passing by cannot lift a siege.
- **A force that has set off turns back only once it is clearly short.** Marching is what brings the
  rest of the force up, so a force sitting right on the requirement would otherwise step forward, drop
  under it, step back, and spend the whole operation window doing that without ever going in. Once
  marching, it holds again only below four fifths of the requirement. A party that has just finished
  looking around has not set off yet, so it is asked for the whole requirement on its first decision.
  A camp that already stands at the town is given the same slack, for the same reason and then some: a
  camp let through on the margin would otherwise be torn down the tick after it was built, and the
  retreat that follows walks the force back from the very gate it was massing at. A relief force now
  has to put a camp clearly short, not a hair short, before it packs up.
- **A party in flight brings nothing to the walls.** A sibling that has just broken off a fight is no
  longer in a battle and may well be running past the town, so it used to be counted at full strength
  towards launching the assault while the same party on the defending side was discounted for exactly
  that reason. It is now left out of both.
- **Being attacked at the camp no longer splits the force.** The ones taken into a fight at the town
  stop counting towards what holds the camp, while the town they are all besieging counts whole, so
  the ones left over read themselves as alone against it and packed up -- walking away from the fight
  their own siblings were in, which is the opposite of what massing is for. Now a fight that engages
  anybody standing at the town is a fight the whole force is in: nobody is asked to justify the camp
  while it is going on. Only the party's own fight used to count, which covered the party being
  attacked and nobody beside it.
- **A force is no longer let in only to be thrown straight back out.** The decision to march on a town
  weighed the walls alone, while the decision to keep the camp that march founds weighs the walls plus
  whatever relief force stands beside them. So a force strong enough for the walls set off, built its
  camp, and was told the next hour that the place was too strong for it -- for as long as the relief
  force was worth more than a quarter of the garrison, which is not much. Both decisions now weigh the
  same thing, and a force that is let in is one that can stay.
- **Fighting at the walls no longer counts as milling about short of them.** The same fight that must
  not be allowed to tear a camp down must not be allowed to turn a march into a wait either, for the
  same reason: the ones in the fight stop counting, and the town counts whole, so a force winning at
  the gate would read itself as too weak to go in.
- **Being driven out of a camp now means sitting the attempt out.** A force that masses weighs no
  strength at all when it picks a town, so a force thrown out of its camp would pick the very same town
  again the next hour, march back, and be thrown out again for the rest of the operation window. It now
  takes the cooldown instead. A party merely marching on a town that gets reinforced is a different
  case -- nothing threw it out of anything, and the choice that picked the town did weigh the strength
  that has turned against it -- so that one still switches to somewhere it can take, immediately.
- **A camp a lone party built keeps the threshold it has always kept.** The slack that stops a massing
  force from flapping in and out of its own camp is slack it earned by being talked into setting off in
  the first place. A party besieging on its own was never held short of anything, so handing it the same
  slack would quietly have made it stubborner than it was before any of this.
- **A beaten enemy no longer drives an invasion off its objective.** A lord's army that the invasion
  has just routed runs past the town it is massing on, and was counted at full strength as a danger in
  the area -- so the force that had just won the field walked away from its objective. Parties in
  flight no longer count on that side, which is already how they are treated everywhere else.
- **The hourly sweep of the map stopped pricing parties nobody would look at.** Working out what a
  party is worth is the expensive part of describing one, and every reader of those sweeps wants live
  enemies and nobody else, so the rest are dropped before being priced rather than after.
- **The decision log stopped filling up with a force standing still.** One of the facts a log line was
  judged new by was whether the party had come up to the gate, which flips back and forth as it walks
  the last stretch -- so a force whose whole job is to wait wrote a line an hour. That fact is still
  reported, it just no longer decides that something happened. The same log also counted parties in
  flight as having arrived, which overstated the force in precisely the case that explains a withdrawal.
- **Being jumped in transit no longer counts as fighting at the walls.** The rule that lets a force
  already in a fight at the town get on with it, rather than milling about short of the walls, asked
  only whether the deciding party was in a battle at all -- so a party intercepted a province away on
  the march read as pinned at the gate and was waved through to an assault it had not gathered for.
  Where its fight is is now measured from the town, like every other member of the force.
- **Choosing a town is weighed on the party alone even after an objective is dropped.** The strength
  figure is read once at the top of the tick, which on the tick that gives up an objective is the
  pooled figure of the force that was committed to the dead one. Selection reads it fresh now, so
  what it weighs matches what it weighed on the tick the party first chose anything.
- **Three rules that worked were held by nothing.** Each of them could have been deleted outright
  without a single test noticing: that a force caught in a fight at the walls attacks into it instead
  of waiting short of them; that the slack on withdrawal belongs only to a force that was let through
  on that slack, so a lone besieger keeps its old threshold; and that what is weighed against the
  lords nearby is the greater of what the party carries and what its force has standing at the gate,
  not the party alone. Each now has a test, and each test was confirmed by breaking the rule it
  covers and watching it fail.
- **A grouping that did nothing is gone.** The two sweeps that price the hostiles near a target used
  to gather them by the army they march with before summing, which comes to exactly the same number as
  summing them one by one, and a test named for counting army members once pinned a de-duplication
  that never happened. Both sweeps now sum flatly, the army a party belongs to is no longer carried
  into the domain at all, and the test says what it actually checks: a stack straddling the edge of
  sight counts its near half and leaves the far half out, party by party. What this removes is the
  invitation for a future reader to see half a grouping and "restore" the other half into a real
  de-duplication the rules were never written for.

## Risks

- **Holding in hostile land.** A force waiting short of an enemy town is visible and can be engaged
  by vanilla armies before it is ready. The operation window bounds the exposure, but the standoff
  distance matters: inside the garrison's reach it will be sallied on.
- **Escorts are only counted because the adapter puts them back.** Pooling by recorded objective
  cannot see escorts: an escort never runs the siege behaviour, so it never records an objective. Each
  party's escorts are added back explicitly when the force is assembled, the deciding party's own
  included. Anything else that fights for an invasion without recording an objective would be
  invisible the same way.
- **The hold does not actually stand short of the objective.** It patrols around the target, which is
  inside the garrison's reach, so a waiting force can be sallied on. Standing off needs a distance the
  patrol order has no way to express.
- **A dangerous hold is not a hold at all.** The danger that follows from waiting outside an enemy
  town is also what sends a gathering party back into reconnaissance, and reconnaissance patrols the
  nearest friendly settlement, not the objective -- so the force walks back from the gate and loses
  the ground it had gained. What it keeps through that is the recorded objective, which is the whole
  point: giving that up would disband the very force the danger is the reason for, so only the
  operation window ends a hold that never becomes safe. A party attacking alone moves on instead.
- **The 1.5x bias is a tuning number, not a principle.** Too tight and invasions never mass; too
  loose and they behave like the single-objective model that was rejected. It wants a playtest.
- **The kingdom bound is measured on Calradia, not on the DADG map.** 250 is derived from vanilla
  kingdom spans; a denser or sparser map would want its own figure. It is deliberately a constant
  rather than a config knob, so retuning it is a code change.
- **Objectives are now saved state, and nothing clears a stale one.** A recorded objective is written
  and cleared only inside the siege behaviour, so a party the behaviour stops running for -- one now
  steered by vanilla because its invasion holds its capital, or one whose spawn template lets another
  behaviour win -- keeps whatever it last recorded. That stale objective still joins the force and
  still biases siblings toward a town nobody is marching to. The live-member filter limits it to
  parties that still exist. Fixing it properly means clearing the objective when steering stops, which
  reaches past gathering into the steering lifecycle.
