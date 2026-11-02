using CustomSpawns.Spawn.PartySize;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace CustomSpawns.Invasions.Adapters
{
    // Adds the invasion's lord party size bonus to vanilla's limit; see InvasionLordPartySize.
    public sealed class InvasionLordPartySizeObserver : IPartySizeLimitObserver
    {
        private static readonly TextObject InvasionHostText = new("Invasion host");
        private readonly InvasionLordPartySize _invasionLordPartySize;

        public InvasionLordPartySizeObserver(InvasionLordPartySize invasionLordPartySize)
        {
            _invasionLordPartySize = invasionLordPartySize;
        }

        public ExplainedNumber OnPartySizeLimitCalculated(MobileParty mobileParty, ExplainedNumber partySizeLimit)
        {
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
