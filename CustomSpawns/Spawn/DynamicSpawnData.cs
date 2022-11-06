using System.Collections.Generic;
using CustomSpawns.Data;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.UtilityBehaviours;
using CustomSpawns.Utils;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Spawn
{
    public class DynamicSpawnData
    {
        private readonly Dictionary<MobileParty, CSPartyData> _dynamicSpawnData = new ();
        private readonly SpawnDao _spawnDao;

        public DynamicSpawnData(SpawnDao spawnDao, SaveInitialiser saveInitialiser)
        {
            _spawnDao = spawnDao;
            saveInitialiser.RunCallbackOnFirstCampaignTick(Init);
        }

        private void Init()
        {
            foreach (MobileParty mb in MobileParty.All)
            {
                if (mb == null)
                    return;
                foreach (SpawnDto dat in _spawnDao.FindAll())
                {
                    if (CampaignUtils.IsolateMobilePartyStringID(mb) == dat.PartyTemplate.StringId) //TODO could deal with sub parties in the future as well!
                    {
                        //this be a custom spawns party :O
                        AddDynamicSpawnData(mb, new CSPartyData(dat, null));
                        UpdateDynamicData(mb);
                        UpdateRedundantDynamicData(mb);
                    }
                }
            }
        }

        public void AddDynamicSpawnData(MobileParty mb, CSPartyData data)
        {
            if (_dynamicSpawnData.ContainsKey(mb))
            {
                return;
            }
            _dynamicSpawnData.Add(mb, data);
        }

        public bool RemoveDynamicSpawnData(MobileParty mb)
        {
            return _dynamicSpawnData.Remove(mb);
        }

        public CSPartyData GetDynamicSpawnData(MobileParty mb)
        {
            if (!_dynamicSpawnData.ContainsKey(mb))
                return null;
            return _dynamicSpawnData[mb];
        }

        public void UpdateDynamicData(MobileParty mb)
        {

        }

        public void UpdateRedundantDynamicData(MobileParty mb)
        {
            GetDynamicSpawnData(mb).latestClosestSettlement = CampaignUtils.GetClosestHabitedSettlement(mb);
        }

    }


    public class CSPartyData
    {
        public SpawnDto spawnBaseDto;
        public Settlement latestClosestSettlement;

        public CSPartyData(SpawnDto spawnDto, Settlement latestClosestSettlement)
        {
            spawnBaseDto = spawnDto;
            this.latestClosestSettlement = latestClosestSettlement;
        }
    }
}
