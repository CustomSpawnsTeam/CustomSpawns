using System.Collections.Generic;
using CustomSpawns.Data.Dto;

namespace CustomSpawns.Data.Dao
{
    public interface ISpawnDao
    {
        IList<SpawnDto> FindAll();
        SpawnDto? FindByPartyTemplateId(string partyTemplateId);
        SpawnDto? FindParentSpawnByPartyTemplateId(string partyTemplateId);
        ISet<string> FindAllSubPartyTemplateId();
        ISet<string> FindAllPartyTemplateId();
    }
}