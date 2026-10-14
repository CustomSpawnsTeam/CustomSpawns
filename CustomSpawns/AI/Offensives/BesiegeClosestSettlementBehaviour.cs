using System;
using CustomSpawns.AI.Adapters;
using CustomSpawns.AI.Model;
using CustomSpawns.Data.Dto;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.AI.Offensives
{
    public sealed class BesiegeClosestSettlementBehaviour
        : SettlementOffensivePartyBehaviour<BesiegeClosestSettlementData>
    {
        public const string SaveKey = "_customSpawnsBesiegeClosestSettlementStates";

        // How far below the launch requirement a force that has already set off must fall before it gives
        // the attempt up, as a share of that requirement.
        private const float ReHoldRatioMargin = 0.8f;

        private readonly ISiegeOperations _siege;

        public BesiegeClosestSettlementBehaviour(ISiegeOperations siege,
            Func<float, float, float> roll)
            : this(siege, roll, new PartyStateStore<SettlementOffensiveBehaviourState>(SaveKey))
        {
        }

        public BesiegeClosestSettlementBehaviour(ISiegeOperations siege,
            Func<float, float, float> roll,
            PartyStateStore<SettlementOffensiveBehaviourState> states) : base(roll, states)
        {
            _siege = siege ?? throw new ArgumentNullException(nameof(siege));
        }

        private protected override IPartyStrengthObserver PartyStrength => _siege;

        private protected override IPatrolOrders PatrolOrders => _siege;

        private protected override void TrackTarget(SettlementOffensiveBehaviourState state)
        {
            state.TargetMapFaction ??= _siege.GetTargetMapFaction(state.Target!);
        }

        private protected override bool IsMatchingOperationActive(MobileParty party, Settlement target)
        {
            return _siege.IsMatchingSiegeActive(party, target) ||
                   _siege.IsMatchingAssaultActive(party, target);
        }

        private protected override bool IsCurrentTargetValid(MobileParty party,
            SettlementOffensiveBehaviourState state, bool matchingOperationActive)
        {
            return _siege.IsCurrentTargetValid(party, state.Target!, matchingOperationActive,
                state.TargetMapFaction);
        }

        private protected override bool IsTargetComplete(MobileParty party, Settlement target)
        {
            return _siege.IsTargetComplete(party, target);
        }

        /**
         * The strength requirement that qualified the target when it was selected is re-checked every tick.
         * While a siege camp is active, the assessment instead weighs the force standing at the town
         * against the town plus the relief force near it, so reinforcements can make the besieger withdraw
         * before an assault starts.
         */
        private protected override bool ShouldAbandonTarget(MobileParty party, Settlement target,
            float attackerStrength, BesiegeClosestSettlementData configuration)
        {
            // A camp already stands at the town, so what can hold it against a relief force is what is
            // standing there: a sibling still marching cannot lift the siege it is walking towards.
            if (IsActiveSiegeCamp(party, target))
            {
                // A battle anybody in the force is already in decides this for all of it: walking away is
                // not on offer while pinned, so asking here would dissolve the camp over a fight it may
                // well be winning. The parties fighting it are not on the defending side of this sum --
                // the roaming scan skips anybody in a battle -- which is why the arrived figure skips the
                // force's own fighters too, and why the ones left over must not read themselves as alone.
                if (_siege.IsForcePinnedInBattle(party, target))
                {
                    return false;
                }

                // A camp that already stands is the furthest a party gets from having to be talked into
                // setting off, so it is given at least the slack a marching force is given. Asking it for
                // the full requirement while the march that produced it was let through on the margin
                // would tear down every camp founded on that margin the tick after it was built. Only a
                // force that was let through on that margin earns it: a party that besieges on its own was
                // never held short of anything, so it keeps the threshold it has always withdrawn on.
                float holdRatio = _siege.GathersBeforeAssault(party)
                    ? configuration.MinimumAttackerToTargetRatio * ReHoldRatioMargin
                    : configuration.MinimumAttackerToTargetRatio;
                return !OffensiveStrengthRules.MeetsStrengthRatio(
                    _siege.GetArrivedAttackerStrength(party, target),
                    _siege.GetActiveSiegeDefenderStrength(party, target), holdRatio);
            }

            // A party that masses on its objective is weak alone by design, so the ratio must not take the
            // target away from it. What ends a gather nobody joins is the operation window, not the ratio.
            if (_siege.GathersBeforeAssault(party))
            {
                return false;
            }

            return !OffensiveStrengthRules.MeetsStrengthRatio(attackerStrength,
                GetTargetStrength(party, target), configuration.MinimumAttackerToTargetRatio);
        }

        /**
         * A gathering party holds near its objective until the force that has arrived there could take it.
         * A siege one of its own is already running is a force in itself, so it marches to support that.
         * A force that has already set off turns back only once it is clearly short, not the moment it
         * dips under the line: the march itself moves the parties the arrived figure is counted from, so a
         * force sitting on the line would otherwise spend the whole operation window stepping forward and
         * back without ever reaching the gate. What it must be strong enough for is the town plus the
         * relief force near it -- the very sum the camp it is about to found will be judged against -- or
         * the slack would be measured against a smaller requirement than the one that then tears the camp
         * down, and a force would be let in only to be driven straight back out.
         */
        private protected override bool ShouldHoldShortOfTarget(MobileParty party, Settlement target,
            bool alreadyApproaching, BesiegeClosestSettlementData configuration)
        {
            float requiredRatio = alreadyApproaching
                ? configuration.MinimumAttackerToTargetRatio * ReHoldRatioMargin
                : configuration.MinimumAttackerToTargetRatio;
            return _siege.GathersBeforeAssault(party) &&
                   // A fight at the gate takes its own side out of the arrived figure and leaves the town
                   // whole, so asking here would turn a force that is fighting at the walls into one that
                   // mills about short of them instead of joining in.
                   !_siege.IsForcePinnedInBattle(party, target) &&
                   !_siege.IsSupportableSiegeActive(party, target) &&
                   !OffensiveStrengthRules.MeetsStrengthRatio(
                       _siege.GetArrivedAttackerStrength(party, target),
                       _siege.GetActiveSiegeDefenderStrength(party, target), requiredRatio);
        }

        private protected override AIBehaviorData? CreateTargetAbandonmentBehavior(
            MobileParty party, Settlement target, BesiegeClosestSettlementData configuration)
        {
            return IsActiveSiegeCamp(party, target)
                ? _siege.AbandonSiegeAndCreateRetreatBehavior(party, target)
                : null;
        }

        private bool IsActiveSiegeCamp(MobileParty party, Settlement target)
        {
            return _siege.IsMatchingSiegeActive(party, target) &&
                   !_siege.IsMatchingAssaultActive(party, target);
        }

        /**
         * A siege asks only about the lords within reach of the target, weighed against the force that is
         * actually with the party: what it carries itself, or what the force has standing at the gate once
         * that is the greater. The pooled figure is deliberately not what is asked, however much selection
         * asks it: an objective is recorded with no regard to distance, so siblings a province away would
         * talk a lone party into marching past a stack it cannot survive. Arrival alone will not do either
         * -- recon runs while the party is still marching, where it reads zero and would take the objective
         * away before anybody could reach it.
         */
        private protected override bool PassesReconSafety(MobileParty party, Settlement target,
            float attackerStrength, BesiegeClosestSettlementData configuration)
        {
            return OffensiveStrengthRules.MeetsStrengthRatio(
                Math.Max(_siege.GetOwnAttackerStrength(party),
                    _siege.GetArrivedAttackerStrength(party, target)),
                _siege.GetHostileLordStrengthNearTarget(party, target),
                configuration.MinimumAttackerToRoamingHostilesRatio);
        }

        /**
         * A party that masses on its objective marches on a siege its own side is already running there, or
         * on a fight its force is already in at the gate: those are the force, and lords near the town are
         * what the camp's own withdrawal test weighs them against. A party attacking alone joins nobody.
         */
        private protected override bool JoinsOperationUnderway(MobileParty party, Settlement target) =>
            _siege.GathersBeforeAssault(party) &&
            (_siege.IsSupportableSiegeActive(party, target) || _siege.IsForcePinnedInBattle(party, target));

        /**
         * A party that masses on its objective keeps it while the ground around it is dangerous: the
         * recorded objective is the only thing that makes its siblings one force, so giving it up would
         * disband the gathering the danger is the very reason for. It holds in reconnaissance instead,
         * and the operation window ends a hold that never becomes safe. A party attacking alone has
         * nobody to wait for and still looks for somewhere quieter.
         */
        private protected override bool ReselectTargetWhenUnsafe(MobileParty party) =>
            !_siege.GathersBeforeAssault(party);

        private protected override Settlement? FindAlternativeTarget(MobileParty party,
            Settlement target, float attackerStrength, BesiegeClosestSettlementData configuration)
        {
            return _siege.FindAlternativeSiegeTarget(party, target, attackerStrength,
                configuration.MinimumAttackerToTargetRatio,
                configuration.MinimumAttackerToRoamingHostilesRatio, configuration.TargetTypes);
        }

        private protected override Settlement? FindTarget(MobileParty party, float attackerStrength,
            BesiegeClosestSettlementData configuration)
        {
            return _siege.FindClosestSiegeTarget(party, attackerStrength,
                configuration.MinimumAttackerToTargetRatio,
                configuration.MinimumAttackerToRoamingHostilesRatio, configuration.TargetTypes);
        }

        private protected override float GetTargetStrength(MobileParty party, Settlement target)
        {
            return _siege.GetTargetStrength(party, target);
        }

        private protected override AIBehaviorData CreateOffensiveBehavior(MobileParty party,
            Settlement target)
        {
            return _siege.CreateSiegeBehavior(party, target);
        }
    }
}
