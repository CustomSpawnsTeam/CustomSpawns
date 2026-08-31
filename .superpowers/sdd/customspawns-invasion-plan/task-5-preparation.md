# Task 5 preparation — bounded occupation garrison

Status: preparation only. No product source, tests, build, runtime, or commit was changed.

## Allocation boundary

Use one concrete, pure `OccupationGarrisonAllocation` rule under `CustomSpawns.Invasions`. It accepts only primitive-ID/value snapshots; it has no TaleWorlds references, save attributes, static campaign access, or roster mutation.

Its input snapshot contains:

- settlement ID; credited participant party IDs copied from the ready capture context; configuration values;
- native observations expressed as values: current garrison member count, actual `PartySizeLimit`, current native garrison defensive strength, and town militia strength;
- one immutable donor snapshot per *credited participant only*, including that party's healthy total and its nonhero member-stack candidates. Each candidate carries donor ID, character ID, ranged flag, healthy count, whole-stack count/XP, defender power, and the source/destination XP-cap values needed for preflight. Prison rosters are absent from this shape.

The adapter creates the snapshot from `context.ParticipantPartyIds`, resolving only active parties that still belong to the credited group. It does not enumerate the group's current members to widen the donor set. This includes an escort only when that escort was an actual recorded participant.

## Pure rule

The native garrison observation is authoritative. Let `countCeiling` be
`min(config.MaximumGarrisonTroopCount, max(0, actualPartySizeLimit))` and let
`room` be `max(0, countCeiling - currentGarrisonCount)`. The strength objective is
`militiaStrength * config.MilitiaStrengthRatio` and the count objective is
`config.MinimumGarrisonTroopCount`.

The rule takes no troop when the existing garrison already meets both objectives.
Otherwise it plans a contribution until both objectives are met, or `room` is
exhausted. Thus the final count never exceeds the configured maximum or native
capacity; an already-over-cap native garrison receives nothing. This is a stop
rule, not a conversion of defensive strength into an invented troop count.

For each donor, its initial loss allowance is
`floor(initialHealthyTotal * MaximumDonorLossFraction)`. A candidate is eligible
only while it has healthy members, its donor retains allowance, it is nonhero,
and its XP preflight succeeds. The rule may return a partial plan when the
available healthy budget, capacity, or maximum is insufficient; it also returns
one concise insufficiency diagnostic containing the limiting observations. It
never tops up an adequate garrison and never creates a troop.

Build ranged and non-ranged candidate queues, each ordered by descending native
defender power, then ordinal character ID, then ordinal donor ID. Select one
preflighted troop at a time, alternating ranged then non-ranged while both queues
remain eligible; continue with the remaining queue only after the other is
exhausted. Update the virtual donor, destination count/XP, strength, capacity,
and donor allowance after each selection. This produces deterministic mixed
contributions whenever both classes can fund more than one selection, without
allowing ranged-first selection to consume the whole budget.

### XP preflight

For every proposed single-member (or later coalesced only after re-preflight)
transfer, the pure rule chooses the integer nearest to
`floor(sourceXp * take / sourceStackCount)` within:

```text
max(0, sourceXp - (sourceStackCount - take) * sourceUpgradeCap)
    <= xpToMove <=
min(sourceXp, (destinationCount + take) * destinationUpgradeCap - destinationXp)
```

Use checked 64-bit intermediates and reject a malformed/overflowing candidate.
An empty interval makes that candidate ineligible before any mutation. The
virtual destination element is keyed by character ID, so sequential donors of
the same character see the previous planned destination count and XP. The plan
therefore predicts aggregate destination count and XP, while each retained
source and the destination remain within their own native upgrade-XP caps.

The returned plan consists of immutable transfer entries `(donorId, characterId,
take, xpToMove)` and summary deltas: source loss, destination gain, XP moved,
defensive strength added, and diagnostics. A zero-entry plan is valid and does
not make the rule infer a missing participant snapshot.

## Adapter integration after the Task 4 gate

Task 4 owns the single hourly consumer of `GetReadyOccupations()`. Task 5 extends
that exact executor, before food relief and its sole `CompleteOccupation` call:

1. Re-read the ready context, expected owner, active native garrison, absent
   settlement/donor map events, current garrison strength, capacity, militia,
   and only the persisted participant party IDs. If a readiness condition is no
   longer true, make no roster call and leave the context pending/cancelled by
   the existing state rules.
2. Snapshot the native member rosters with `GetElementCopyAtIndex`; exclude
   heroes and use only `Number - WoundedNumber` healthy members. Obtain defender
   power and both owner-context XP caps in the adapter, then pass values to the
   pure rule. Do not approximate garrison strength from a count.
3. Apply each already-preflighted entry with `AddToCounts` on the donor and
   destination member rosters, `woundedCount: 0`, and the paired negative/
   positive XP delta. Never use a prisoner roster, direct roster element writes,
   or `SetElementNumber`/`SetElementXp`.
4. Pre-mutation readiness failures remain pending and may be retried by the
   hourly executor. A malformed candidate is excluded before this stage. Once
   the first roster call begins, apply the fully preflighted plan synchronously;
   do not re-snapshot, continue, or replay it on a later tick. An unexpected
   roster-call or re-observation failure after mutation begins is terminal:
   record a high-signal diagnostic, mark the existing capture context cancelled,
   and make no more roster calls, food-relief calls, or `CompleteOccupation`
   calls for that context. `Cancelled` already persists and blocks later
   readiness after load. Native state may then contain an incomplete paired
   mutation, which this feature must not claim is conserved or try to repair
   automatically; it is explicit Task 8/live-diagnosis evidence.
5. Before completion, require total source member loss to equal destination
   gain, all source wounded counts to be unchanged, no hero/prison roster delta,
   and the planned/observed XP deltas to reconcile. Only then run Task 4's food
   relief and the one `CompleteOccupation` call. A completed context makes no
   further roster calls after save/load.

This transfers troops into the real native garrison, so existing owner-clan
wages and native desertion remain native behavior. Task 5 adds no wage subsidy,
desertion workaround, sustainability claim, or periodic garrison top-up; those
require Task 8 live evidence.

## Focused tests to add with implementation

- **Adequate/native bounds:** an existing garrison meeting count and strength
  returns no entries; a native donation below either objective yields only the
  remaining deficit; capacity below the configuration maximum, an over-cap
  existing garrison, and the configured maximum each bound the result.
- **Strength and conservative scarcity:** militia-relative strength can require
  additional entries after the minimum count; no survivors and
  `floor(healthy * fraction) == 0` donate none; insufficient donor budget
  returns the smaller plan and diagnostic, never fabricated members.
- **Eligibility and mixture:** only captured participant IDs (including a
  participating escort) contribute; same-clan other-group/nonparticipant,
  hero, wounded-only, and prisoner-shaped input cannot. Equal inputs produce
  the same ordered plan, and eligible ranged/non-ranged stacks alternate in
  the asserted order.
- **Count and wounded conservation:** summed donor healthy loss equals summed
  destination gain, every donor loss is within its initial fraction allowance,
  and source wounded count remains unchanged. A zero plan and a saved completed
  occupation produce no transfer/replay.
- **Failure boundary:** a failure before the first roster call remains pending;
  an injected failure after mutation begins cancels the existing context and
  suppresses food, completion, and every later roster call. The test must not
  assert conservation or automatic recovery for this deliberately fail-closed
  native-error path.
- **XP caps and conservation:** exercise a proportional share, a source lower
  bound that raises the share, a destination upper bound that lowers it, and an
  empty interval that excludes the candidate. In every accepted entry total XP
  across source plus destination is preserved and each post-transfer stack is
  within its own cap.
- **Sequential same-character XP:** two donors of the same character feed one
  destination element. Assert the second entry uses the first entry's virtual
  destination count/XP, and that the plan's aggregate destination count/XP and
  both donors' retained count/XP match the predicted interval results. Repeat
  with a destination cap that makes the second donor ineligible.

Pure tests establish allocation, conservation, XP-cap feasibility, and the
single-completion contract. They cannot establish native wage affordability,
desertion/recruitment, food ordering, or sustained force balance; Task 8 must
measure those in the disposable runtime fixture.
