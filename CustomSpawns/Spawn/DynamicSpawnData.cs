using System;
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

        /**
         * Derived state, never saved: the registry grouped by the main party each entry supports.
         * Parties are registered a spawn at a time but their escorts are looked up every tick, so the
         * grouping is rebuilt on the next lookup after a registration rather than on every lookup.
         */
        private ILookup<MobileParty, MobileParty>? _accompanyingParties;
        private ISet<string> _spawnPartyTemplateIds;
        private ISet<string> _spawnSubPartyTemplateIds;
        public event Action<MobileParty, CsPartyData>? PartyRegistered;
        public event Action<MobileParty>? PartyRemoved;

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
            InvalidateAccompanyingParties();
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
                InvalidateAccompanyingParties();
                PartyRegistered?.Invoke(mobileParty, partyData);
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
            RegisterParty(mobileParty, owningSpawnTemplateId, true, null, "Original");
        }

        public void RegisterSupportingParty(MobileParty mobileParty, string owningSpawnTemplateId,
            MobileParty supportedParty)
        {
            RegisterParty(mobileParty, owningSpawnTemplateId, false, supportedParty, "Escort");
        }

        public void RegisterInvasionReinforcementParty(MobileParty mobileParty, string owningSpawnTemplateId)
        {
            RegisterParty(mobileParty, owningSpawnTemplateId, false, null, "Reinforcement");
        }

        /**
         * A party the game spawned itself and an invasion took back. It is not a reinforcement: it
         * costs the invasion none of its landing budget, which counts reinforcements alone.
         */
        public void RegisterAdoptedParty(MobileParty mobileParty, string owningSpawnTemplateId)
        {
            RegisterParty(mobileParty, owningSpawnTemplateId, false, null, "Adopted");
        }

        private void RegisterParty(MobileParty mobileParty, string owningSpawnTemplateId, bool isPrimarySpawn,
            MobileParty? supportedParty, string? partyRole = null)
        {
            string partyTemplateId = CampaignUtils.IsolateMobilePartyStringID(mobileParty);
            _dynamicSpawnData[mobileParty] = new CsPartyData(partyTemplateId, owningSpawnTemplateId,
                isPrimarySpawn, GetNearestSettlement(mobileParty), supportedParty, partyRole);
            InvalidateAccompanyingParties();
            PartyRegistered?.Invoke(mobileParty, _dynamicSpawnData[mobileParty]);
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

        /**
         * The still active parties that spawned along with the given main party and escort it.
         * It is empty for a supporting party, and for a main party whose escorts were discovered
         * outside of a spawn, since those do not know which party they support.
         */
        public IEnumerable<MobileParty> FindAccompanyingParties(MobileParty mainParty)
        {
            _accompanyingParties ??= BuildAccompanyingIndex(_dynamicSpawnData);
            return _accompanyingParties[mainParty].Where(accompanyingParty => accompanyingParty.IsActive);
        }

        /**
         * Drops the grouping so the next lookup rebuilds it. Every change to the registry must call
         * this, since the grouping is only as current as the registry it was built from.
         */
        private void InvalidateAccompanyingParties()
        {
            _accompanyingParties = null;
        }

        /**
         * Groups the registered supporting parties under the main party each one supports.
         * Entries without a supported party contribute nothing, since they escort no one.
         */
        internal static ILookup<MobileParty, MobileParty> BuildAccompanyingIndex(
            IDictionary<MobileParty, CsPartyData> registry)
        {
            return registry.Where(entry => entry.Value.SupportedParty is not null)
                .ToLookup(entry => entry.Value.SupportedParty!, entry => entry.Key);
        }

        public CsPartyData? TakeDynamicSpawnData(MobileParty? mobileParty)
        {
            InvalidateAccompanyingParties();
            CsPartyData? data = TakeFromRegistry(_dynamicSpawnData, mobileParty);
            if (data != null && mobileParty != null) PartyRemoved?.Invoke(mobileParty);
            return data;
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

        [SaveableField(5)]
        private MobileParty? _supportedParty;

        [SaveableField(6)]
        private string? _invasionGroupId;

        [SaveableField(7)]
        private string? _invasionRole;

        [SaveableField(8)]
        private string? _partyRole;

        public string? InvasionGroupId => _invasionGroupId;
        public string? InvasionRole => _invasionRole;

        public void SetInvasionIdentity(string? groupId, string? role)
        {
            _invasionGroupId = groupId;
            _invasionRole = role;
        }

        public string PartyTemplateId => _partyTemplateId;
        public string? OwningSpawnTemplateId => _owningSpawnTemplateId;
        public bool IsPrimarySpawn => _isPrimarySpawn;
        public bool IsIndependentReinforcement => string.Equals(_partyRole, "Reinforcement",
            StringComparison.Ordinal);
        public bool IsAdopted => string.Equals(_partyRole, "Adopted", StringComparison.Ordinal);
        public bool IsEscort => string.Equals(_partyRole, "Escort", StringComparison.Ordinal) ||
                                (_partyRole == null && !_isPrimarySpawn);
        public Settlement? LatestClosestSettlement
        {
            get => _latestClosestSettlement;
            set => _latestClosestSettlement = value;
        }

        /**
         * The main party this supporting party spawned with and escorts.
         * It is null for a main party, and for a supporting party discovered outside of a spawn.
         */
        public MobileParty? SupportedParty => _supportedParty;

        public CsPartyData(string partyTemplateId, string? owningSpawnTemplateId, bool isPrimarySpawn,
            Settlement? latestClosestSettlement, MobileParty? supportedParty = null, string? partyRole = null)
        {
            _partyTemplateId = partyTemplateId;
            _owningSpawnTemplateId = owningSpawnTemplateId;
            _isPrimarySpawn = isPrimarySpawn;
            _latestClosestSettlement = latestClosestSettlement;
            _supportedParty = supportedParty;
            _partyRole = partyRole;
        }
    }
}
