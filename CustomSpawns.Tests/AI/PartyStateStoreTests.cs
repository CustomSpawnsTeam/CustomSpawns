using System.Collections.Generic;
using System.Runtime.Serialization;
using CustomSpawns.AI;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Tests.AI
{
    public class PartyStateStoreTests
    {
        [Test]
        public void SyncData_DoesNotSaveTheStateOfAPartyThatLeftTheCampaign()
        {
            MobileParty departed = DummyParty();
            MobileParty remaining = DummyParty();
            var store = new PartyStateStore<State>("_key", party => party != departed);
            store.GetOrAdd(departed);
            store.GetOrAdd(remaining);

            var dataStore = new DataStoreStub();
            store.SyncData(dataStore);

            Assert.That(dataStore.Captured, Has.Count.EqualTo(1));
            Assert.That(dataStore.Captured, Does.ContainKey(remaining));
        }

        [Test]
        public void SyncData_DropsLoadedStatesOfPartiesThatNoLongerExist()
        {
            MobileParty departed = DummyParty();
            var store = new PartyStateStore<State>("_key", _ => false);

            store.SyncData(new DataStoreStub(new Dictionary<MobileParty, State>
            {
                [departed] = new State()
            }));

            Assert.That(store.Count, Is.Zero);
        }

        [Test]
        public void SyncData_RepairsANullLoadedDictionary()
        {
            var store = new PartyStateStore<State>("_key", _ => true);

            store.SyncData(new DataStoreStub(null));
            store.SyncData(new DataStoreStub());

            Assert.That(store.Count, Is.Zero);
        }

        private static MobileParty DummyParty()
        {
            return (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        }

        private sealed class State
        {
        }

        private sealed class DataStoreStub : IDataStore
        {
            private readonly Dictionary<MobileParty, State>? _loaded;
            private readonly bool _hasLoadedValue;

            public DataStoreStub()
            {
            }

            public DataStoreStub(Dictionary<MobileParty, State>? loaded)
            {
                _loaded = loaded;
                _hasLoadedValue = true;
            }

            public bool IsSaving => !_hasLoadedValue;
            public bool IsLoading => _hasLoadedValue;
            public Dictionary<MobileParty, State> Captured { get; private set; } = new();

            public bool SyncData<T>(string key, ref T data)
            {
                if (_hasLoadedValue)
                {
                    data = (T)(object)_loaded!;
                    return true;
                }

                Captured = (Dictionary<MobileParty, State>)(object)data!;
                return true;
            }
        }
    }
}
