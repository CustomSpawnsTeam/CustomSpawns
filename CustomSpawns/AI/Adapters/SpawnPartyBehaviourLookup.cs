using System;
using System.Collections.Generic;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Utils;

namespace CustomSpawns.AI.Adapters
{
    public sealed class SpawnPartyBehaviourLookup
    {
        private readonly ISpawnDao _spawnDao;
        private readonly IPartyBehaviourDao _partyBehaviourDao;
        private readonly MessageBoxService _messageBoxService;
        private readonly ISet<string> _reportedUnknownTemplateIds = new HashSet<string>();

        public SpawnPartyBehaviourLookup(ISpawnDao spawnDao, IPartyBehaviourDao partyBehaviourDao,
            MessageBoxService messageBoxService)
        {
            _spawnDao = spawnDao ?? throw new ArgumentNullException(nameof(spawnDao));
            _partyBehaviourDao = partyBehaviourDao ?? throw new ArgumentNullException(nameof(partyBehaviourDao));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
        }

        public IReadOnlyList<PriorityPartyBehaviourData> FindBehaviours(string? owningSpawnTemplateId)
        {
            SpawnDto? spawn = owningSpawnTemplateId is null
                ? null
                : _spawnDao.FindByPartyTemplateId(owningSpawnTemplateId);
            if (spawn is null)
            {
                return Array.Empty<PriorityPartyBehaviourData>();
            }

            string? templateId = spawn.PriorityPartyBehaviourTemplateId;
            if (templateId is null)
            {
                return spawn.PatrolAroundClosestLestInterruptedAndSwitch is null
                    ? Array.Empty<PriorityPartyBehaviourData>()
                    : new PriorityPartyBehaviourData[] { spawn.PatrolAroundClosestLestInterruptedAndSwitch };
            }

            PartyBehaviourDto? partyBehaviour = _partyBehaviourDao.FindById(templateId);
            if (partyBehaviour is null)
            {
                if (_reportedUnknownTemplateIds.Add(templateId))
                {
                    _messageBoxService.ShowMessage("Custom Spawns cannot find the priority party behaviour template " +
                                                   "'" + templateId + "' referenced by a spawn.");
                }

                return Array.Empty<PriorityPartyBehaviourData>();
            }

            return partyBehaviour.PriorityPartyBehaviours ?? Array.Empty<PriorityPartyBehaviourData>();
        }

        public IEnumerable<Type> FindAllConfiguredBehaviourTypes()
        {
            foreach (PartyBehaviourDto partyBehaviour in _partyBehaviourDao.FindAll())
            {
                foreach (PriorityPartyBehaviourData configuration in partyBehaviour.PriorityPartyBehaviours ??
                         Array.Empty<PriorityPartyBehaviourData>())
                {
                    yield return configuration.GetType();
                }
            }
        }
    }
}
