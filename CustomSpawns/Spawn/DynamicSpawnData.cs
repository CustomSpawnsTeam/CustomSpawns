using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.UtilityBehaviours;
using CustomSpawns.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Spawn
{
    public class DynamicSpawnData : CampaignBehaviorBase
    {
        private readonly SpawnDao _spawnDao;
        private readonly Dictionary<MobileParty, CsPartyData> _dynamicSpawnData;
        private ISet<string> _spawnPartyTemplateIds;
        private ISet<string> _spawnSubPartyTemplateIds;

        public DynamicSpawnData(SpawnDao spawnDao, SaveInitialiser saveInitialiser)
        {
            _spawnDao = spawnDao;
            _dynamicSpawnData = new ();
            _spawnPartyTemplateIds = new HashSet<string>();
            _spawnSubPartyTemplateIds = new HashSet<string>();
            saveInitialiser.RunCallbackOnFirstCampaignTick(Init);
        }
        
        public override void RegisterEvents()
        {
            CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, OnMobilePartyCreated);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, HourlyMobilePartyUpdate);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void Init()
        {
            _spawnPartyTemplateIds = _spawnDao.FindAllPartyTemplateId();
            _spawnSubPartyTemplateIds = _spawnDao.FindAllSubPartyTemplateId();
            foreach (MobileParty mb in MobileParty.All)
            {
                if (mb == null)
                    return;
                AddCustomSpawn(mb);
            }
        }

        private bool IsCustomSpawnParty(MobileParty? mobileParty)
        {
            if (mobileParty == null)
                return false;
            string isolatedPartyStringId = CampaignUtils.IsolateMobilePartyStringID(mobileParty);
            return _spawnPartyTemplateIds.Any(spawn => spawn.StartsWith(isolatedPartyStringId))
                || _spawnSubPartyTemplateIds.Any(spawn => spawn.StartsWith(isolatedPartyStringId));
        }

        private void OnMobilePartyCreated(MobileParty mobileParty)
        {
            AddCustomSpawn(mobileParty);
        }

        private void AddCustomSpawn(MobileParty mobileParty)
        {
            if (IsCustomSpawnParty(mobileParty) && !_dynamicSpawnData.ContainsKey(mobileParty))
            {
                string isolatedPartyStringId = CampaignUtils.IsolateMobilePartyStringID(mobileParty);
                Settlement? settlement = GetNearestSettlement(mobileParty);
                CsPartyData partyData = new (isolatedPartyStringId, settlement); 
                _dynamicSpawnData.Add(mobileParty, partyData);
            }
        }

        // partyBase is the party which destroyed the mobileParty
        private void OnMobilePartyDestroyed(MobileParty? mobileParty, PartyBase destroyer)
        {
            if (mobileParty is not null && IsCustomSpawnParty(mobileParty) && _dynamicSpawnData.ContainsKey(mobileParty))
            {
                _dynamicSpawnData.Remove(mobileParty);
            }
        }

        private void HourlyMobilePartyUpdate(MobileParty? mobileParty)
        {
            if (mobileParty is not null && _dynamicSpawnData.ContainsKey(mobileParty))
            {
                _dynamicSpawnData[mobileParty].LatestClosestSettlement = GetNearestSettlement(mobileParty);
            }
        }

        public CsPartyData? GetDynamicSpawnData(MobileParty? mb)
        {
            if (mb is not null && _dynamicSpawnData.ContainsKey(mb))
            {
                return _dynamicSpawnData[mb];   
            }
            return null;
        }

        private Settlement? GetNearestSettlement(MobileParty? mobileParty)
        {
            if (mobileParty is null)
            {
                return null;
            }
            return CampaignUtils.GetNearestSettlement(Settlement.All.ToList(), new List<IMapPoint>()
            {
                mobileParty
            });
        }
    }

    public class CsPartyData
    {
        public readonly string PartyTemplateId;
        public Settlement? LatestClosestSettlement;

        public CsPartyData(string partyTemplateId, Settlement? latestClosestSettlement)
        {
            PartyTemplateId = partyTemplateId;
            LatestClosestSettlement = latestClosestSettlement;
        }
    }
}
