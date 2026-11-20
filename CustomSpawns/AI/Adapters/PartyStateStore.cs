using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.AI.Adapters
{
    /**
     * Where a behaviour keeps one state per party, and how those states survive a save. Parties that have
     * left the campaign are dropped, since nothing can ever ask for their state again and the dictionary
     * is what gets serialized.
     */
    public sealed class PartyStateStore<TState> where TState : class, new()
    {
        private readonly string _saveKey;
        private readonly Func<MobileParty, bool> _isPartyInCampaign;
        private Dictionary<MobileParty, TState> _states = new();

        public PartyStateStore(string saveKey) : this(saveKey, party => party.IsActive)
        {
        }

        public PartyStateStore(string saveKey, Func<MobileParty, bool> isPartyInCampaign)
        {
            _saveKey = saveKey ?? throw new ArgumentNullException(nameof(saveKey));
            _isPartyInCampaign = isPartyInCampaign ?? throw new ArgumentNullException(nameof(isPartyInCampaign));
        }

        public int Count => _states.Count;

        public TState GetOrAdd(MobileParty party)
        {
            if (!_states.TryGetValue(party, out TState? state))
            {
                state = new TState();
                _states.Add(party, state);
            }
            return state;
        }

        // The state a party already keeps, or null. Asking what another party is working on must not
        // enrol that party in the behaviour, which is what GetOrAdd would do.
        public TState? Find(MobileParty party)
        {
            return _states.TryGetValue(party, out TState? state) ? state : null;
        }

        public void SyncData(IDataStore dataStore)
        {
            DropDepartedParties();
            dataStore.SyncData(_saveKey, ref _states);
            _states ??= new Dictionary<MobileParty, TState>();
            DropDepartedParties();
        }

        private void DropDepartedParties()
        {
            List<MobileParty>? departed = null;
            foreach (MobileParty party in _states.Keys)
            {
                if (!_isPartyInCampaign(party))
                {
                    (departed ??= new List<MobileParty>()).Add(party);
                }
            }

            if (departed is null)
            {
                return;
            }

            foreach (MobileParty party in departed)
            {
                _states.Remove(party);
            }
        }
    }
}
