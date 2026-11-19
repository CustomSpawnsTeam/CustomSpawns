using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Dto;
using CustomSpawns.Data.Reader.Impl;
using CustomSpawns.Utils;

namespace CustomSpawns.Data.Dao
{
    public class PartyBehaviourDao : IPartyBehaviourDao
    {
        private readonly PartyBehaviourDataReader _partyBehaviourDataReader;
        private readonly PartyBehaviourDtoAdapter _partyBehaviourDtoAdapter;
        private readonly MessageBoxService _messageBoxService;
        private List<PartyBehaviourDto>? _partyBehaviours;

        public PartyBehaviourDao(PartyBehaviourDataReader partyBehaviourDataReader,
            PartyBehaviourDtoAdapter partyBehaviourDtoAdapter, MessageBoxService messageBoxService)
        {
            _partyBehaviourDataReader = partyBehaviourDataReader;
            _partyBehaviourDtoAdapter = partyBehaviourDtoAdapter;
            _messageBoxService = messageBoxService;
        }

        private List<PartyBehaviourDto> PartyBehaviours()
        {
            if (_partyBehaviours == null)
            {
                _partyBehaviours = _partyBehaviourDataReader.Data
                    .Select(template =>
                    {
                        try
                        {
                            return _partyBehaviourDtoAdapter.Adapt(template);
                        }
                        catch (ArgumentException e)
                        {
                            _messageBoxService.ShowCustomSpawnsErrorMessage(e, "reading party behaviour data");
                            return null;
                        }
                    })
                    .Where(partyBehaviour => partyBehaviour != null)
                    .ToList()!;
            }
            return _partyBehaviours;
        }

        public IList<PartyBehaviourDto> FindAll()
        {
            return PartyBehaviours();
        }

        public PartyBehaviourDto? FindById(string id)
        {
            return PartyBehaviours()
                .FirstOrDefault(partyBehaviour => string.Equals(partyBehaviour.Id, id, StringComparison.Ordinal));
        }
    }
}
