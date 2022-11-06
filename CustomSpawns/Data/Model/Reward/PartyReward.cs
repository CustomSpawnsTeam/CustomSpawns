using System.Collections.Generic;

namespace CustomSpawns.Data.Model.Reward
{
    public class PartyReward
    {
        public string PartyId { get; set; }

        public List<Reward> RewardsList { get; set; } = new();
    }
}