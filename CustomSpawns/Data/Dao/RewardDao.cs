using System.Collections.Generic;
using CustomSpawns.Data.Model.Reward;
using CustomSpawns.Data.Reader.Impl;

namespace CustomSpawns.Data.Dao
{
    public class RewardDao
    {
        private readonly RewardDataReader _rewardDataReader;
        // TODO create PartyRewardDto with the concrete objects ie PartyTemplate etc.. instead of stringIds  
        private PartyRewards? _partyRewards = new();

        public RewardDao(RewardDataReader rewardDataReader)
        {
            _rewardDataReader = rewardDataReader;
        }

        private PartyRewards PartyRewards()
        {
            if (_partyRewards == null)
            {
                _partyRewards = _rewardDataReader.Data;
            }
            return _partyRewards;
        }

        public IList<PartyReward> FindAll()
        {
            return PartyRewards().AllPartyRewards.AsReadOnly();
        }
    }
}