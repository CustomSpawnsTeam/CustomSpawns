using System.Collections.Generic;
using CustomSpawns.Data.Dto;

namespace CustomSpawns.Data.Dao
{
    public interface IPartyBehaviourDao
    {
        IList<PartyBehaviourDto> FindAll();
        PartyBehaviourDto? FindById(string id);
    }
}
