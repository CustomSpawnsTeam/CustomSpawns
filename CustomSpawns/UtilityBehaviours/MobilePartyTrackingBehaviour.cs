using System.Collections.Generic;
using CustomSpawns.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.UtilityBehaviours
{
    class MobilePartyTrackingBehaviour: CampaignBehaviorBase
    {
        private readonly ModDebug _modDebug;

        public MobilePartyTrackingBehaviour(SaveInitialiser saveInitialiser, ModDebug modDebug)
        {
            _modDebug = modDebug;
            saveInitialiser.RunCallbackOnFirstCampaignTick(OnGameStart);
        }
        
        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, OnMobilePartyHourlyTick);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, LateDailyTick);
        }

        public override void SyncData(IDataStore dataStore) //maybe make this work between loads too? it is just a daily event tho.
        { }

        private readonly Dictionary<MobileParty, List<Settlement>> _dailyPresences = new();
        private readonly Dictionary<Settlement, List<MobileParty>> _settlementDailyPresences = new();
        private readonly List<MobileParty> _toBeRemoved = new();

        private void OnGameStart()
        {
            foreach(Settlement s in Settlement.All)
            {
                _settlementDailyPresences.Add(s, new List<MobileParty>());
            }

        }

        private void OnMobilePartyHourlyTick(MobileParty mb)
        {
            if (mb == null)
                return;

            Settlement closest = CampaignUtils.GetClosestSettlement(mb);

            if (!_dailyPresences.ContainsKey(mb))
                _dailyPresences.Add(mb, new List<Settlement>());

            if (!_dailyPresences[mb].Contains(closest))
            {
                _dailyPresences[mb].Add(closest);
                _settlementDailyPresences[closest].Add(mb);
            }
        }

        private void OnMobilePartyDestroyed(MobileParty mb, PartyBase pb)
        {
            _toBeRemoved.Add(mb);
        }

        private void LateDailyTick() 
        {
            foreach(var mb in _toBeRemoved)
            {
                _dailyPresences.Remove(mb);
            }

            _toBeRemoved.Clear();

            foreach(var l in _settlementDailyPresences.Values)
            {
                l.Clear();
            }

            foreach(var l in _dailyPresences.Values)
            {
                l.Clear();
            }
        }

        /// <summary>
        /// Returns a list of all settlements that this party has been closest to within the day.
        /// </summary>
        /// <param name="mb"></param>
        /// <returns></returns>
        public List<Settlement> GetMobilePartyDailyPresences(MobileParty mb)
        {
            if (_dailyPresences.ContainsKey(mb))
            {
                return _dailyPresences[mb];
            }
            else
            {
                _modDebug.ShowMessage("Tried to get daily presences of an invalid mobile party!", DebugMessageType.Development);
                return new List<Settlement>();
            }
        }

        /// <summary>
        /// Gets a list of all mobile parties that have visited this settlement today. 
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public List<MobileParty> GetSettlementDailyMobilePartyPresences(Settlement s)
        {
            if (_settlementDailyPresences.ContainsKey(s))
            {
                return _settlementDailyPresences[s];
            }
            _modDebug.ShowMessage("Tried to get daily presences of an invalid settlement!", DebugMessageType.Development);
            return new List<MobileParty>();
        }

    }
}
