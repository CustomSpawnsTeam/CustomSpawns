using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.UtilityBehaviours;
using CustomSpawns.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace CustomSpawns.Spawn
{
    public class DynamicSpawnData : CampaignBehaviorBase
    {
        private readonly SpawnDao _spawnDao;
        private Dictionary<MobileParty, CsPartyData> _dynamicSpawnData;
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
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, HourlyMobilePartyUpdate);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_dynamicSpawnData", ref _dynamicSpawnData);
            _dynamicSpawnData ??= new Dictionary<MobileParty, CsPartyData>();
        }

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
                bool isUnambiguousPrimary = _spawnPartyTemplateIds.Contains(isolatedPartyStringId)
                                            && !_spawnSubPartyTemplateIds.Contains(isolatedPartyStringId);
                string? owningSpawnTemplateId = isUnambiguousPrimary
                    ? isolatedPartyStringId
                    : _spawnDao.FindParentSpawnByPartyTemplateId(isolatedPartyStringId)?.PartyTemplate.StringId;
                CsPartyData partyData = CreateDiscoveredPartyData(isolatedPartyStringId, owningSpawnTemplateId,
                    _spawnPartyTemplateIds.Contains(isolatedPartyStringId),
                    _spawnSubPartyTemplateIds.Contains(isolatedPartyStringId), settlement);
                _dynamicSpawnData.Add(mobileParty, partyData);
            }
        }

        internal static CsPartyData CreateDiscoveredPartyData(string partyTemplateId,
            string? owningSpawnTemplateId, bool isPrimaryTemplate, bool isSupportingTemplate,
            Settlement? latestClosestSettlement)
        {
            bool isPrimarySpawn = isPrimaryTemplate && !isSupportingTemplate;
            return new CsPartyData(partyTemplateId, owningSpawnTemplateId, isPrimarySpawn,
                latestClosestSettlement);
        }

        public void RegisterPrimaryParty(MobileParty mobileParty, string owningSpawnTemplateId)
        {
            RegisterParty(mobileParty, owningSpawnTemplateId, true);
        }

        public void RegisterSupportingParty(MobileParty mobileParty, string owningSpawnTemplateId)
        {
            RegisterParty(mobileParty, owningSpawnTemplateId, false);
        }

        private void RegisterParty(MobileParty mobileParty, string owningSpawnTemplateId, bool isPrimarySpawn)
        {
            string partyTemplateId = CampaignUtils.IsolateMobilePartyStringID(mobileParty);
            _dynamicSpawnData[mobileParty] = new CsPartyData(partyTemplateId, owningSpawnTemplateId,
                isPrimarySpawn, GetNearestSettlement(mobileParty));
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

        public CsPartyData? TakeDynamicSpawnData(MobileParty? mobileParty)
        {
            return TakeFromRegistry(_dynamicSpawnData, mobileParty);
        }

        internal static CsPartyData? TakeFromRegistry<TKey>(IDictionary<TKey, CsPartyData> registry, TKey? key)
            where TKey : class
        {
            if (key is null || !registry.TryGetValue(key, out CsPartyData? partyData))
            {
                return null;
            }

            registry.Remove(key);
            return partyData;
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
        [SaveableField(1)]
        private string _partyTemplateId;

        [SaveableField(2)]
        private string? _owningSpawnTemplateId;

        [SaveableField(3)]
        private bool _isPrimarySpawn;

        [SaveableField(4)]
        private Settlement? _latestClosestSettlement;

        public string PartyTemplateId => _partyTemplateId;
        public string? OwningSpawnTemplateId => _owningSpawnTemplateId;
        public bool IsPrimarySpawn => _isPrimarySpawn;
        public Settlement? LatestClosestSettlement
        {
            get => _latestClosestSettlement;
            set => _latestClosestSettlement = value;
        }

        public CsPartyData(string partyTemplateId, string? owningSpawnTemplateId, bool isPrimarySpawn,
            Settlement? latestClosestSettlement)
        {
            _partyTemplateId = partyTemplateId;
            _owningSpawnTemplateId = owningSpawnTemplateId;
            _isPrimarySpawn = isPrimarySpawn;
            _latestClosestSettlement = latestClosestSettlement;
        }
    }
}
