using System;
using System.Collections.Generic;
using CustomSpawns.AI.Model;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.AI
{
    /**
     * Where a settlement offensive keeps one state per party, and how those states survive a save.
     */
    internal sealed class SettlementOffensiveStateStore
    {
        private readonly string _saveKey;
        private Dictionary<MobileParty, SettlementOffensiveBehaviourState> _states = new();

        internal SettlementOffensiveStateStore(string saveKey)
        {
            _saveKey = saveKey ?? throw new ArgumentNullException(nameof(saveKey));
        }

        internal SettlementOffensiveBehaviourState GetOrAdd(MobileParty party)
        {
            if (!_states.TryGetValue(party, out SettlementOffensiveBehaviourState? state))
            {
                state = new SettlementOffensiveBehaviourState();
                _states.Add(party, state);
            }
            return state;
        }

        internal void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(_saveKey, ref _states);
            _states ??= new Dictionary<MobileParty, SettlementOffensiveBehaviourState>();
        }
    }
}
