using System.Collections.Generic;
using CustomSpawns.CampaignData.Implementations;
using CustomSpawns.Spawn.PartyComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.UtilityBehaviours
{
    public class AiDecisionLoggerBehaviour : CampaignBehaviorBase
    {
        private readonly DailyLogger _dailyLogger;
        private readonly Dictionary<MobileParty, string> _previousStates = new();

        public AiDecisionLoggerBehaviour(DailyLogger dailyLogger)
        {
            _dailyLogger = dailyLogger;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.TickPartialHourlyAiEvent.AddNonSerializedListener(this, OnPartialHourlyAiTick);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnPartialHourlyAiTick(MobileParty party)
        {
            if (party.PartyComponent is not CustomSpawnsPartyComponent)
            {
                return;
            }

            string currentState = DescribeState(party);
            if (!_previousStates.TryGetValue(party, out string previousState))
            {
                _previousStates.Add(party, currentState);
                _dailyLogger.Info("[" + party.StringId + "][" + party.Name + "] AI state observed" +
                    " | source=partial_hourly_ai" +
                    " | " + currentState);
                return;
            }

            if (previousState == currentState)
            {
                return;
            }

            _previousStates[party] = currentState;
            _dailyLogger.Info("[" + party.StringId + "][" + party.Name + "] AI state changed" +
                " | source=partial_hourly_ai" +
                " | previous={" + previousState + "}" +
                " | current={" + currentState + "}");
        }

        private void OnMobilePartyDestroyed(MobileParty party, PartyBase destroyer)
        {
            _previousStates.Remove(party);
        }

        private static string DescribeState(MobileParty party)
        {
            return "default_behavior=" + party.DefaultBehavior +
                " | short_term_behavior=" + party.ShortTermBehavior +
                " | default_target_settlement_id=" + GetSettlementId(party.TargetSettlement) +
                " | default_target_party_id=" + GetPartyId(party.TargetParty) +
                " | default_target_position=" + party.TargetPosition +
                " | short_target_settlement_id=" + GetSettlementId(party.ShortTermTargetSettlement) +
                " | short_target_party_id=" + GetPartyId(party.ShortTermTargetParty) +
                " | short_target_position=" + party.AiBehaviorTarget;
        }

        private static string GetSettlementId(Settlement settlement)
        {
            return settlement?.StringId ?? "none";
        }

        private static string GetPartyId(MobileParty party)
        {
            return party?.StringId ?? "none";
        }
    }
}
