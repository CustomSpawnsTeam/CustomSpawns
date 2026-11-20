using System.Collections.Generic;

namespace CustomSpawns.Data.Dto
{
    public class PartyBehaviourDto
    {
        public string Id { get; set; }

        public IReadOnlyList<PriorityPartyBehaviourData> PriorityPartyBehaviours { get; set; }
    }
}
