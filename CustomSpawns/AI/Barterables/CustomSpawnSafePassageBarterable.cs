using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.AI.Barterables
{
    public class CustomSpawnSafePassageBarterable : SafePassageBarterable
    {

        public CustomSpawnSafePassageBarterable(Hero originalOwner, Hero otherHero, PartyBase ownerParty,
            PartyBase otherParty)
            : base(originalOwner, otherHero, ownerParty, otherParty) {}

        public override void Apply()
        {
            List<MobileParty> partiesToJoinPlayerSide = new();
            List<MobileParty> partiesToJoinEnemySide = new()
            {
                OriginalParty.MobileParty
            };
            PlayerEncounter.Current.FindAllNpcPartiesWhoWillJoinEvent(ref partiesToJoinPlayerSide, ref partiesToJoinEnemySide);
            foreach (MobileParty mobileParty in partiesToJoinEnemySide)
            {
                mobileParty.Ai.SetDoNotAttackMainParty(16);
                mobileParty.Ai.SetMoveModeHold();
                mobileParty.IgnoreForHours(16f);
                mobileParty.Ai.SetInitiative(0.0f, 0.8f, 8f);
            }
            PlayerEncounter.LeaveEncounter = true;
            OriginalParty.MobileParty.Ai.SetMoveModeHold();
        }
    }
}