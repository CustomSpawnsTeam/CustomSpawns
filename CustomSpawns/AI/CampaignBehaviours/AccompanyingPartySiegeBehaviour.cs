using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.AI.CampaignBehaviours
{
    /**
     * Makes a party spawned alongside a main party join the siege the main party is besieging,
     * so that the whole spawn besieges a settlement as one force instead of milling about outside it.
     */
    public class AccompanyingPartySiegeBehaviour : CampaignBehaviorBase
    {
        private readonly SteeredPartyLookup _steeredParties;

        internal AccompanyingPartySiegeBehaviour(SteeredPartyLookup steeredParties)
        {
            _steeredParties = steeredParties;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, MatchSupportedPartySiege);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void MatchSupportedPartySiege(MobileParty party)
        {
            MobileParty? supportedParty = _steeredParties.GetSteeredPartyData(party)?.SupportedParty;
            if (supportedParty is null)
            {
                return;
            }

            if (ShouldMatchSupportedPartySiege(party.BesiegerCamp == supportedParty.BesiegerCamp,
                    supportedParty.IsActive, party.MapEvent is not null, party.CurrentSettlement is not null))
            {
                party.BesiegerCamp = supportedParty.BesiegerCamp;
            }
        }

        /**
         * Re-assigning an identical camp is pointless churn, a party fighting a map event must not be
         * repositioned out of its battle, and a party inside a settlement must not be teleported into the
         * siege lines. A destroyed main party drags nobody anywhere.
         * A null camp is intentionally matched too: when the main party lifts its siege, its escorts leave with it.
         */
        internal static bool ShouldMatchSupportedPartySiege(bool campsAlreadyMatch, bool supportedPartyIsActive,
            bool partyIsInMapEvent, bool partyIsInSettlement)
        {
            return !campsAlreadyMatch && supportedPartyIsActive && !partyIsInMapEvent && !partyIsInSettlement;
        }
    }
}
