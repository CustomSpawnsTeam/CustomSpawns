using CustomSpawns.Spawn.PartySize;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Adds the invasion's lord party size bonus to vanilla's limit; see InvasionLordPartySize. A garrison army's lord
     * is sized to the men he led out instead, so none of them deserts for crowding him.
     */
    public sealed class InvasionLordPartySizeObserver : IPartySizeLimitObserver
    {
        private static readonly TextObject InvasionHostText = new("Invasion host");
        private static readonly TextObject GarrisonArmyText = new("Invasion garrison army");
        private readonly InvasionLordPartySize _invasionLordPartySize;
        private readonly IInvasionGarrisonArmyLeaders _invasionGarrisonArmyLeaders;

        public InvasionLordPartySizeObserver(InvasionLordPartySize invasionLordPartySize,
            IInvasionGarrisonArmyLeaders invasionGarrisonArmyLeaders)
        {
            _invasionLordPartySize = invasionLordPartySize;
            _invasionGarrisonArmyLeaders = invasionGarrisonArmyLeaders;
        }

        public ExplainedNumber OnPartySizeLimitCalculated(MobileParty mobileParty, ExplainedNumber partySizeLimit)
        {
            int? garrisonArmyLimit = mobileParty.IsLordParty
                ? _invasionGarrisonArmyLeaders.FindPartySizeLimit(mobileParty.LeaderHero?.StringId)
                : null;

            if (garrisonArmyLimit != null)
            {
                partySizeLimit.Add(garrisonArmyLimit.Value - partySizeLimit.ResultNumber, GarrisonArmyText);
                return partySizeLimit;
            }

            int bonus = _invasionLordPartySize.FindBonus(mobileParty.ActualClan?.StringId,
                mobileParty.IsLordParty);

            if (bonus > 0)
            {
                partySizeLimit.Add(bonus, InvasionHostText);
            }

            return partySizeLimit;
        }
    }
}
