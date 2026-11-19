using System.Collections.Generic;
using CustomSpawns.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.AI.Barterables
{
    public class CustomSpawnSafePassageBarterable : SafePassageBarterable
    {
        private readonly bool _isCustomSpawnsParty;

        public CustomSpawnSafePassageBarterable(Hero originalOwner, Hero otherHero, PartyBase ownerParty,
            PartyBase otherParty, bool isCustomSpawnsParty = true)
            : base(originalOwner, otherHero, ownerParty, otherParty)
        {
            _isCustomSpawnsParty = isCustomSpawnsParty;
        }

        public override int GetUnitValueForFaction(IFaction faction)
        {
            return ApplyCustomSpawnsPriceAdjustment(base.GetUnitValueForFaction(faction), _isCustomSpawnsParty,
                OriginalParty.MapFaction.IsBanditFaction);
        }

        public static int ApplyCustomSpawnsPriceAdjustment(int unitValue, bool isCustomSpawnsParty,
            bool isBanditFaction)
        {
            return isCustomSpawnsParty && !isBanditFaction ? unitValue / 8 : unitValue;
        }

        public static bool IsCustomSpawnsParty(string mobilePartyStringId, ISet<string> partyTemplateIds,
            ISet<string> subPartyTemplateIds)
        {
            string partyTemplateId = CampaignUtils.IsolateMobilePartyStringID(mobilePartyStringId);
            return partyTemplateIds.Contains(partyTemplateId) || subPartyTemplateIds.Contains(partyTemplateId);
        }

        public override void Apply()
        {
            List<MobileParty> partiesToJoinPlayerSide = new();
            List<MobileParty> partiesToJoinEnemySide = new()
            {
                OriginalParty.MobileParty
            };
            PlayerEncounter.Current.FindAllNpcPartiesWhoWillJoinEvent(partiesToJoinPlayerSide, partiesToJoinEnemySide);
            foreach (MobileParty mobileParty in partiesToJoinEnemySide)
            {
                mobileParty.Ai.SetDoNotAttackMainParty(16);
                mobileParty.SetMoveModeHold();
                mobileParty.IgnoreForHours(16f);
                mobileParty.Ai.SetInitiative(0.0f, 0.8f, 8f);
            }
            PlayerEncounter.LeaveEncounter = true;
            OriginalParty.MobileParty.SetMoveModeHold();
        }
    }
}
