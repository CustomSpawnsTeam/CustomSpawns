using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace CustomSpawns.Spawn.PartyComponents
{
    /**
     * This class is the implementation of a WarPartyComponent for all Custom Spawns parties.
     * This will make spawned parties part of their respective clan strength calculation.
     */
    public class CustomSpawnsPartyComponent : WarPartyComponent
    {
        public CustomSpawnsPartyComponent(Hero partyOwner, TextObject name, Settlement homeSettlement)
        {
            PartyOwner = partyOwner;
            Name = name;
            HomeSettlement = homeSettlement;
        }

        /**
         * This is the clan that owns the party.
         * Generally this would be the clan leader.
         */
        public override Hero PartyOwner { get; }

        /**
         * This is the name of the party on the campaign map.
         */
        public override TextObject Name { get; }

        /**
         * This is the settlement the party has spawned at.
         */
        public override Settlement HomeSettlement { get; }
        
        protected override void OnMobilePartySetOnCreation()
        {
            MobileParty.ActualClan = PartyOwner.Clan;
            MobileParty.Party.SetCustomOwner(PartyOwner);
            MobileParty.Party.SetCustomName(Name);
            MobileParty.SetCustomHomeSettlement(HomeSettlement);
            MobileParty.ShouldJoinPlayerBattles = true;
            MobileParty.Party.SetVisualAsDirty();
        }
    }
}