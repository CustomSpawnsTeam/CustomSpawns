using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.UtilityBehaviours;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Invasions.Adapters
{
    internal sealed class InvasionClanRepository : CampaignBehaviorBase, IInvasionClanRepository
    {
        internal const string SaveKey = "_invasionClanSpawnTemplateIds";
        private readonly ISpawnDao _spawnDao;
        private Dictionary<string, string> _spawnTemplateIdsByClanId = new(StringComparer.Ordinal);

        internal InvasionClanRepository(ISpawnDao spawnDao, SaveInitialiser initialiser)
        {
            _spawnDao = spawnDao ?? throw new ArgumentNullException(nameof(spawnDao));
            if (initialiser == null) throw new ArgumentNullException(nameof(initialiser));
            initialiser.RunCallbackOnFirstCampaignTick(Initialize);
        }

        public override void RegisterEvents()
        {
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(SaveKey, ref _spawnTemplateIdsByClanId);
            _spawnTemplateIdsByClanId = _spawnTemplateIdsByClanId == null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(_spawnTemplateIdsByClanId, StringComparer.Ordinal);
        }

        internal void Initialize()
        {
            foreach (SpawnDto spawn in _spawnDao.FindAll().Where(candidate => candidate.Invasion != null))
            {
                Register(spawn.PartyTemplate.StringId, spawn.SpawnClan.StringId);
            }
        }

        public string? FindSpawnTemplate(string clanId) =>
            _spawnTemplateIdsByClanId.TryGetValue(clanId, out string? spawnTemplateId)
                ? spawnTemplateId
                : null;

        public IReadOnlyList<string> FindClanIds(string spawnTemplateId) => _spawnTemplateIdsByClanId
            .Where(entry => string.Equals(entry.Value, spawnTemplateId, StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .ToList();

        public void Register(string spawnTemplateId, string clanId)
        {
            if (string.IsNullOrWhiteSpace(spawnTemplateId))
                throw new ArgumentException("An invasion spawn template id is required.", nameof(spawnTemplateId));
            if (string.IsNullOrWhiteSpace(clanId))
                throw new ArgumentException("An invasion clan id is required.", nameof(clanId));

            // Configuration validation already guarantees one invasion per clan. Keeping the first mapping
            // also preserves the identity stored in an existing save if configuration later changes.
            if (!_spawnTemplateIdsByClanId.ContainsKey(clanId))
            {
                _spawnTemplateIdsByClanId.Add(clanId, spawnTemplateId);
            }
        }
    }
}
