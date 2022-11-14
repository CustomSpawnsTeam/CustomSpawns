using System.Collections.Generic;
using System.Linq;
using CustomSpawns.CampaignData.Config;
using CustomSpawns.UtilityBehaviours;
using CustomSpawns.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.TwoDimension;

namespace CustomSpawns.CampaignData.Implementations
{
    class DevestationMetricData : CampaignBehaviorBase
    {

        private Dictionary<Settlement, float> _settlementToDevestation = new();
        private readonly DevestationMetricConfig _config;
        private readonly MessageBoxService _messageBoxService;
        private readonly MobilePartyTrackingBehaviour _mobilePartyTrackingBehaviour;
        private readonly ModDebug _modDebug;
        
        public DevestationMetricData(MobilePartyTrackingBehaviour mobilePartyTrackingBehaviour,
            CampaignDataConfigLoader campaignDataConfigLoader, SaveInitialiser saveInitialiser,
            MessageBoxService messageBoxService, ModDebug modDebug)
        {
            // TODO refactor mobilePartyTrackingBehaviour so that the stateful data is in another class.
            _mobilePartyTrackingBehaviour = mobilePartyTrackingBehaviour;
            _config = campaignDataConfigLoader.GetConfig<DevestationMetricConfig>();
            _messageBoxService = messageBoxService;
            _modDebug = modDebug;
            saveInitialiser.RunCallbackOnFirstCampaignTick(OnSaveStart);
        }

        #region Custom Campaign Data Implementation

        public override void RegisterEvents()
        {
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.VillageLooted.AddNonSerializedListener(this, OnVillageLooted);

            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnSettlementDaily);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_settlementToDevestation", ref _settlementToDevestation);
        }

        private void OnSaveStart()
        {
            if (_settlementToDevestation.Count != 0) //If you include non-village etc. or add new settlements this approach will break old saves.
            {
                return;
            }

            foreach (Settlement s in Settlement.All) //assuming no new settlements can be created mid-game.
            {
                if (!s.IsVillage)
                {
                    continue;
                }
                _settlementToDevestation.Add(s, 0);
            }
        }

        private void FlushSavedData()
        {
            _settlementToDevestation.Clear();
        }


        #endregion

        #region Event Callbacks

        private void OnMapEventEnded(MapEvent e)
        {
            if (e == null)
                return;

            float increase = (e.AttackerSide.CasualtyStrength + e.DefenderSide.CasualtyStrength) * _config.FightOccuredDevestationPerPower;

            Settlement closestSettlement = CampaignUtils.GetClosestVillage(e.Position);

            if (!_settlementToDevestation.ContainsKey(closestSettlement))
            {
                _messageBoxService.ShowCustomSpawnsErrorMessage(new System.Exception("Devestation value for settlement could not be found!"));
                return;
            }

            ChangeDevestation(closestSettlement, increase);

            _modDebug.ShowMessage("Fight at " + closestSettlement.Name + ". Increasing devestation by " + increase + ". New devestation is: " + _settlementToDevestation[closestSettlement], _config);
        }

        private void OnVillageLooted(Village v)
        {
            if (v == null)
                return;

            ChangeDevestation(v.Settlement, _config.DevestationPerTimeLooted);

            _modDebug.ShowMessage("Successful Looting at " + v.Name + ". Increasing devestation by " + _config.DevestationPerTimeLooted, _config);
        }

        private void OnSettlementDaily(Settlement s)
        {
            if (s == null || !s.IsVillage)
                return;

            if (!_settlementToDevestation.ContainsKey(s))
            {
                _messageBoxService.ShowCustomSpawnsErrorMessage(new System.Exception("Devastation value for settlement could not be found!"));
                return;
            }

            var presentInDay = _mobilePartyTrackingBehaviour.GetSettlementDailyMobilePartyPresences(s);

            float hostileDecay = 0;

            float friendlyGain = 0;

            foreach (var mb in presentInDay)
            {
                if (mb.IsBandit)
                {
                    hostileDecay += _config.HostilePresencePerPowerDaily * mb.Party.TotalStrength;
                }
                else if (s.OwnerClan.MapFaction.IsAtWarWith(mb.Party.MapFaction))
                {
                    hostileDecay += _config.HostilePresencePerPowerDaily * mb.Party.TotalStrength;
                }
                else if (s.OwnerClan.MapFaction == mb.Party.MapFaction)
                {
                    friendlyGain += _config.FriendlyPresenceDecayPerPowerDaily * mb.Party.TotalStrength;
                }
            }

            if(friendlyGain > 0)
            {
                //ModDebug.ShowMessage("Calculating friendly presence devestation decay in " + s.Name + ". Decreasing devestation by " + friendlyGain, campaignConfig);

                ChangeDevestation(s, -friendlyGain);
            }


            if(hostileDecay > 0)
            {
                _modDebug.ShowMessage("Calculating hostile presence devestation gain in " + s.Name + ". Increasing devestation by " + hostileDecay, _config);

                ChangeDevestation(s, hostileDecay);
            }

            if(GetDevestation(s) > 0)
            {
                _modDebug.ShowMessage("Calculating daily Devestation Decay in " + s.Name + ". Decreasing devestation by " + _config.DailyDevestationDecay, _config);
                ChangeDevestation(s, -_config.DailyDevestationDecay);
            }

            if(GetDevestation(s) != 0)
                _modDebug.ShowMessage("Current Devestation at " + s.Name + " is now " + _settlementToDevestation[s], _config);
        }

        #endregion



        private void ChangeDevestation(Settlement s, float change)
        {
            if (!_settlementToDevestation.ContainsKey(s))
            {
                _messageBoxService.ShowCustomSpawnsErrorMessage(new System.Exception("Devastation value for settlement could not be found!"));
                return;
            }

            _settlementToDevestation[s] += change;

            _settlementToDevestation[s] = Mathf.Clamp(_settlementToDevestation[s], _config.MinDevestationPerSettlement, _config.MaxDevestationPerSettlement);
        }

        public float GetDevestation(Settlement s)
        {
            if (!s.IsVillage)
            {
                return 0;
            }
            if (_settlementToDevestation.ContainsKey(s))
                return _settlementToDevestation[s];

            _messageBoxService.ShowCustomSpawnsErrorMessage(new System.Exception("Devestation value for settlement could not be found!"));
            return 0;
        }

        public float GetMinimumDevestation()
        {
            return _config.MinDevestationPerSettlement;
        }

        public float GetMaximumDevestation()
        {
            return _config.MaxDevestationPerSettlement;
        }


        public float GetAverageDevestation()
        {
            if (_settlementToDevestation.Values.Count <= 0)
            {
                return 0f;
            }
            return _settlementToDevestation.Values.Aggregate((c,d) => c+d) / _settlementToDevestation.Count; //TODO make more efficient
        }

        public float GetDevestationLerp()
        {
            return GetAverageDevestation() / GetMaximumDevestation();
        }
    }
}
