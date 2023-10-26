using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Dto;
using CustomSpawns.Data.Reader.Impl;
using CustomSpawns.Exception;
using CustomSpawns.Utils;
using TaleWorlds.Core;
using TaleWorlds.Localization;

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
                        catch (TechnicalException e)
                        {
                            _messageBoxService.ShowMessage(new TextObject("{=SpawnAPIWarn001}Warning: The Custom Spawns API detected that you are trying to load" +
                                                           " a Custom Spawns sub-mod into an existing saved game." +
                                                           " This mod requires a fresh start to work correctly.\n" +
                                                           "If you decide to continue, expect to have very unstable" +
                                                           " behaviours during your play-through.").ToString());
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

        public ISet<string> FindAllSubPartyTemplateId()
        {
            IList<SpawnDto> spawns = Spawns();
            if (spawns.All(spawn => spawn.SpawnAlongWith.IsEmpty()))
            {
                return new HashSet<string>();
            }

            return new HashSet<string>(spawns
                .Select(spawn => spawn.SpawnAlongWith.AsEnumerable())
                .Aggregate((allSupporters, supportingPartyTemplates) =>
                {
                    var newSupporters = allSupporters.ToList();
                    newSupporters.AddRange(supportingPartyTemplates);
                    return newSupporters;
                })
                .Select(partyTemplate => partyTemplate.templateObject.StringId));
        }

        public ISet<string> FindAllPartyTemplateId()
        {
            return new HashSet<string>(Spawns()
                .Select(spawn => spawn.PartyTemplate.StringId));
        }
    }
}