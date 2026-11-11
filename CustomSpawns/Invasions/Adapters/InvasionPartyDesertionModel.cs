using NLog;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * An invasion's garrisons are spared vanilla's wage and party size desertion; see InvasionGarrisonDesertion. So is
     * the party of a garrison army's lord, which carries the same men.
     */
    public sealed class InvasionPartyDesertionModel : PartyDesertionModel
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly PartyDesertionModel _partyDesertionModel;
        private readonly IInvaderClanQuery _invaderClanQuery;
        private readonly InvasionGarrisonDesertion _invasionGarrisonDesertion;
        private readonly IInvasionGarrisonArmyLeaders _invasionGarrisonArmyLeaders;

        public InvasionPartyDesertionModel(PartyDesertionModel decorated, IInvaderClanQuery invaderClanQuery,
            InvasionGarrisonDesertion invasionGarrisonDesertion, IInvasionGarrisonArmyLeaders invasionGarrisonArmyLeaders)
        {
            _partyDesertionModel = decorated;
            _invaderClanQuery = invaderClanQuery;
            _invasionGarrisonDesertion = invasionGarrisonDesertion;
            _invasionGarrisonArmyLeaders = invasionGarrisonArmyLeaders;
        }

        public override TroopRoster GetTroopsToDesert(MobileParty mobileParty)
        {
            /*
             * A garrison army's lord leads the garrison's men, paid as a garrison is: an invader's veterans cost far
             * more than his clan's wage budget allows a lord, and the army would melt away before its first siege.
             */
            if (_invasionGarrisonArmyLeaders.LeadsGarrisonArmy(mobileParty.LeaderHero?.StringId))
            {
                return TroopRoster.CreateDummyTroopRoster();
            }

            TroopRoster vanillaDeserters = _partyDesertionModel.GetTroopsToDesert(mobileParty);
            if (!_invasionGarrisonDesertion.KeepsTroops(mobileParty.IsGarrison,
                    _invaderClanQuery.IsInvaderClan(mobileParty.CurrentSettlement?.OwnerClan?.StringId),
                    mobileParty.Morale, mobileParty.Party.NumberOfAllMembers - mobileParty.Party.PartySizeLimit,
                    _partyDesertionModel.GetMoraleThresholdForTroopDesertion()))
            {
                return vanillaDeserters;
            }

            int unpaidDesertions = _invasionGarrisonDesertion.UnpaidDesertions(mobileParty.HasUnpaidWages,
                mobileParty.Party.NumberOfHealthyMembers, mobileParty.MemberRoster.TotalRegulars);
            TroopRoster deserters = SelectDeserters(mobileParty.MemberRoster, unpaidDesertions);

            if (vanillaDeserters.TotalManCount > 0 || unpaidDesertions > 0)
            {
                Logger.Info("Invasion garrison desertion: settlement=" + mobileParty.CurrentSettlement?.StringId +
                            " owner=" + mobileParty.CurrentSettlement?.OwnerClan?.StringId +
                            " troops=" + mobileParty.MemberRoster.TotalManCount +
                            " size_limit=" + mobileParty.Party.PartySizeLimit +
                            " wage=" + mobileParty.TotalWage + " payment_limit=" + mobileParty.PaymentLimit +
                            " unpaid_wages=" + mobileParty.HasUnpaidWages.ToString("0.##") +
                            " owner_gold=" + mobileParty.CurrentSettlement?.OwnerClan?.Gold +
                            " morale=" + mobileParty.Morale.ToString("0.##") +
                            " vanilla=" + vanillaDeserters.TotalManCount + " deserted=" + deserters.TotalManCount);
            }

            return deserters;
        }

        public override float GetDesertionChanceForTroop(MobileParty mobileParty,
            in TroopRosterElement troopRosterElement) =>
            _partyDesertionModel.GetDesertionChanceForTroop(mobileParty, in troopRosterElement);

        public override int GetMoraleThresholdForTroopDesertion() =>
            _partyDesertionModel.GetMoraleThresholdForTroopDesertion();

        // Takes deserters from the back of the roster, wounded first, as vanilla does.
        private static TroopRoster SelectDeserters(TroopRoster memberRoster, int count)
        {
            TroopRoster deserters = TroopRoster.CreateDummyTroopRoster();
            for (int index = memberRoster.Count - 1; index >= 0 && count > 0; index--)
            {
                TroopRosterElement element = memberRoster.GetElementCopyAtIndex(index);
                if (element.Character.HeroObject != null)
                {
                    continue;
                }

                int wounded = System.Math.Min(element.WoundedNumber, count);
                int healthy = System.Math.Min(element.Number - element.WoundedNumber, count - wounded);
                if (wounded + healthy > 0)
                {
                    deserters.AddToCounts(element.Character, wounded + healthy, false, wounded);
                    count -= wounded + healthy;
                }
            }

            return deserters;
        }
    }
}
