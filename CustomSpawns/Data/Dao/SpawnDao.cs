using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Dto;
using CustomSpawns.Data.Reader.Impl;
using CustomSpawns.Utils;
using TaleWorlds.Core;

namespace CustomSpawns.Data.Dao
{
    public class SpawnDao
    {
        private readonly SpawnDataReader _spawnDataReader;
        private readonly SpawnDtoAdapter _spawnDtoAdapter;
        private readonly MessageBoxService _messageBoxService;
        private List<SpawnDto>? _spawns;

        public SpawnDao(SpawnDataReader spawnDataReader, SpawnDtoAdapter spawnDtoAdapter, MessageBoxService messageBoxService)
        {
            _spawnDataReader = spawnDataReader;
            _spawnDtoAdapter = spawnDtoAdapter;
            _messageBoxService = messageBoxService;
        }

        private List<SpawnDto> Spawns()
        {
            if (_spawns == null)
            {
                _spawns = _spawnDataReader.Data
                    .Select(spawn => 
                    {
                        try
                        {
                            return _spawnDtoAdapter.Adapt(spawn);
                        }
                        catch (ArgumentException e)
                        {
                            _messageBoxService.ShowCustomSpawnsErrorMessage(e, "reading spawn data");
                            return null;
                        }
                    })
                    .Where(spawn => spawn != null)
                    .ToList()!;
            }
            return _spawns;
        }

        public IList<SpawnDto> FindAll()
        {
            return Spawns();
        }

        public SpawnDto? FindByPartyTemplateId(string partyTemplateId)
        {
            List<SpawnDto> candidates = Spawns()
                .Where(spawn => spawn.PartyTemplate.StringId.Equals(partyTemplateId))
                .ToList();
            if (candidates.IsEmpty())
            {
                return null;
            }
            return candidates.First();
        }

        public ISet<string> FindAllPartyTemplateId()
        {
            ISet<string> mainPartyTemplateIds = Spawns()
                .Select(spawn => spawn.PartyTemplate.StringId)
                .ToHashSet();
            ISet<string> secondaryPartyTemplateIds = Spawns()
                .Select(spawn => spawn.SpawnAlongWith.AsEnumerable())
                .Aggregate((allSupporters, supportingPartyTemplates) =>
                {
                    var newSupporters = allSupporters.ToList();
                    newSupporters.AddRange(supportingPartyTemplates);
                    return newSupporters;
                })
                .Select(partyTemplate => partyTemplate.templateObject.StringId)
                .ToHashSet();
            mainPartyTemplateIds.UnionWith(secondaryPartyTemplateIds);
            return mainPartyTemplateIds;
        }
    }
}