using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Garrisons the fiefs an invasion holds out of its capital's bank, and keeps its lords from besieging
     * anything new until each of them can hold. The capital itself is the exception both ways: it is the
     * bank, and taking it back is never held up.
     */
    public sealed class BannerlordInvasionFiefGarrisons
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Func<InvasionState> _state;
        private readonly BannerlordInvasionConfigurations _bannerlordInvasionConfigurations;
        private readonly InvasionConsolidation _invasionConsolidation;
        private readonly InvasionTroopBank _invasionTroopBank;

        public BannerlordInvasionFiefGarrisons(Func<InvasionState> state,
            BannerlordInvasionConfigurations bannerlordInvasionConfigurations,
            InvasionConsolidation invasionConsolidation, InvasionTroopBank invasionTroopBank)
        {
            _state = state;
            _bannerlordInvasionConfigurations = bannerlordInvasionConfigurations;
            _invasionConsolidation = invasionConsolidation;
            _invasionTroopBank = invasionTroopBank;
        }

        public void TopUpDaily()
        {
            foreach (Invasion invasion in _state().Invasions.Where(invasion => !invasion.IsSuspended).ToList())
            {
                int minimumFiefGarrison =
                    _bannerlordInvasionConfigurations.FindMinimumFiefGarrison(invasion.SpawnTemplateId);
                Settlement? capital = FindHeldCapital(invasion);

                if (minimumFiefGarrison <= 0 || capital?.Town?.GarrisonParty == null)
                {
                    continue;
                }

                IReadOnlyList<InvasionFiefGarrison> fiefs = Snapshot(invasion);
                IReadOnlyList<InvasionGarrisonTransfer> transfers = _invasionConsolidation.PlanTopUps(
                    capital.Town.GarrisonParty.MemberRoster.TotalRegulars,
                    _bannerlordInvasionConfigurations.FindMinimumCapitalGarrison(invasion.SpawnTemplateId) ?? 0,
                    fiefs, minimumFiefGarrison);

                foreach (InvasionGarrisonTransfer transfer in transfers)
                {
                    int moved = Transfer(capital, transfer.SettlementId, transfer.Men);
                    Logger.Info("Invasion fief garrison: spawn=" + invasion.SpawnTemplateId +
                                " capital=" + capital.StringId + " fief=" + transfer.SettlementId +
                                " wanted=" + transfer.Men + " moved=" + moved +
                                " capital_left=" + capital.Town.GarrisonParty.MemberRoster.TotalManCount);
                }
            }
        }

        // Every fief can hold, so the invasion may reach for another; without its capital it has nothing to send.
        public bool CanExpand(Invasion invasion) =>
            FindHeldCapital(invasion) != null && _invasionConsolidation.AllowsExpansion(Snapshot(invasion),
                _bannerlordInvasionConfigurations.FindMinimumFiefGarrison(invasion.SpawnTemplateId));

        // Vanilla has scored its sieges by now; an over-extended invasion's are zeroed, but for its capital's and the
        // conquered kingdom's.
        public void HoldSieges(MobileParty party, PartyThinkParams thinkParams,
            Func<Invasion, string?> findConquestTargetKingdomId)
        {
            Invasion? invasion = _state().FindFightingInvasion(party.ActualClan?.StringId);
            Settlement? capital = invasion == null ? null : FindHeldCapital(invasion);

            if (invasion == null || capital == null || _invasionConsolidation.AllowsExpansion(Snapshot(invasion),
                    _bannerlordInvasionConfigurations.FindMinimumFiefGarrison(invasion.SpawnTemplateId)))
            {
                return;
            }

            string? conquestTargetKingdomId = findConquestTargetKingdomId(invasion);

            foreach ((AIBehaviorData behavior, float _) in thinkParams.AIBehaviorScores.ToList())
            {
                if (behavior.AiBehavior == AiBehavior.BesiegeSettlement && behavior.Party is Settlement target &&
                    _invasionConsolidation.HoldsSiege(false, target.StringId == invasion.CapitalSettlementId,
                        conquestTargetKingdomId != null && target.MapFaction?.StringId == conquestTargetKingdomId))
                {
                    thinkParams.SetBehaviorScore(behavior, 0f);
                }
            }
        }

        // The fiefs the invasion's own clans hold, wherever its kingdom has others.
        private static IReadOnlyList<InvasionFiefGarrison> Snapshot(Invasion invasion) => Town.AllFiefs
            .Where(town => invasion.HoldsClan(town.OwnerClan?.StringId))
            .Select(town => new InvasionFiefGarrison(town.Settlement.StringId,
                town.GarrisonParty?.MemberRoster.TotalRegulars ?? 0,
                town.Settlement.StringId == invasion.CapitalSettlementId,
                town.Settlement.SiegeEvent != null))
            .ToList();

        private static Settlement? FindHeldCapital(Invasion invasion)
        {
            Settlement? capital = invasion.CapitalSettlementId == null
                ? null
                : Settlement.Find(invasion.CapitalSettlementId);

            return capital != null && invasion.HoldsClan(capital.OwnerClan?.StringId) ? capital : null;
        }

        private int Transfer(Settlement capital, string settlementId, int men)
        {
            Settlement? fief = Settlement.Find(settlementId);
            TroopRoster bank = capital.Town.GarrisonParty.MemberRoster;

            if (fief?.Town == null)
            {
                return 0;
            }

            if (fief.Town.GarrisonParty == null)
            {
                fief.AddGarrisonParty();
            }

            TroopRoster? garrison = fief.Town.GarrisonParty?.MemberRoster;

            if (garrison == null)
            {
                return 0;
            }

            TroopRosterElement[] troops = bank.GetTroopRoster()
                .Where(element => !element.Character.IsHero).ToArray();
            int[] allocated = _invasionTroopBank.Distribute(troops.Select(element => element.Number).ToArray(), men);

            for (int index = 0; index < troops.Length; index++)
            {
                int taken = allocated[index];

                if (taken > 0)
                {
                    bank.RemoveTroop(troops[index].Character, taken);
                    garrison.AddToCounts(troops[index].Character, taken);
                }
            }

            return allocated.Sum();
        }
    }
}
