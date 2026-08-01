using System.Collections.Generic;
using CustomSpawns.Data.Dao;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.AI.Barterables
{
    public class SafePassageBehaviour : CampaignBehaviorBase
    {
        private SpawnDao _spawnDao;

        public SafePassageBehaviour(SpawnDao spawnDao)
        {
            _spawnDao = spawnDao;
        }
        
        public override void RegisterEvents()
        {
            CampaignEvents.BarterablesRequested.AddNonSerializedListener(this, SwapSafePassageImplementation);
        }

        private void SwapSafePassageImplementation(BarterData barterData)
        {
            List<Barterable> barterables = barterData.GetBarterables();
            for (int index = 0; index < barterables.Count; index++)
            {
                if (!(barterables[index] is SafePassageBarterable))
                {
                    continue;
                }
                var safePassage = barterables[index];
                bool isCustomSpawnsParty = IsCustomSpawnsParty(safePassage.OriginalParty.MobileParty);
                // If a vanilla minor faction is at war against all kingdoms then bartering for safe passage will also crash 
                if (!isCustomSpawnsParty && !safePassage.OriginalParty.MapFaction.IsMinorFaction)
                {
                    return;
                }
                CustomSpawnSafePassageBarterable customSpawnCustomSpawnSafePassage = new(safePassage.OriginalOwner,
                    Hero.MainHero, safePassage.OriginalParty, PartyBase.MainParty, isCustomSpawnsParty);
                customSpawnCustomSpawnSafePassage.Initialize(safePassage.Group, safePassage.IsContextDependent);
                customSpawnCustomSpawnSafePassage.SetIsOffered(safePassage.IsOffered);
                barterables[index] = customSpawnCustomSpawnSafePassage;
                return;
            }
        }

        private bool IsCustomSpawnsParty(MobileParty mobileParty)
        {
            return CustomSpawnSafePassageBarterable.IsCustomSpawnsParty(mobileParty.StringId,
                _spawnDao.FindAllPartyTemplateId(), _spawnDao.FindAllSubPartyTemplateId());
        }

        public override void SyncData(IDataStore dataStore) {}
    }
}
