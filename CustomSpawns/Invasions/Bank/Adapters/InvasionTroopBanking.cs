using CustomSpawns.Invasions.Reinforcements;
using NLog;

namespace CustomSpawns.Invasions.Bank.Adapters
{
    // Banks the reinforcement waves the schedule draws and logs what each came to.
    public sealed class InvasionTroopBanking
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly InvasionWaveBanking _invasionWaveBanking;

        public InvasionTroopBanking(InvasionWaveBanking invasionWaveBanking)
        {
            _invasionWaveBanking = invasionWaveBanking;
        }

        // Whether the wave was paid in, which is what decides whether it counts against the rule's allowance.
        public bool Bank(InvasionReinforcementDraw draw)
        {
            InvasionWaveReceipt? receipt = _invasionWaveBanking.Bank(draw);

            if (receipt == null)
            {
                Logger.Debug("Invasion reinforcement slot skipped: invasion=" + draw.Invasion.ClanId +
                            " rule=" + draw.RuleId + " reason=template_or_capital_changed");
                return false;
            }

            Logger.Debug("Invasion reinforcement banked: invasion=" + draw.Invasion.ClanId + " rule=" +
                        draw.RuleId + " town=" + receipt.Value.CapitalId + " men=" + receipt.Value.Men +
                        " lords=" + receipt.Value.Lords + " pay=" + receipt.Value.Pay + " garrison=" +
                        receipt.Value.Garrison + " next_arrival_day=" +
                        draw.Invasion.FindReinforcementSchedule(draw.RuleId)?.NextDeadlineDays);
            return true;
        }

        public void PayOnly(InvasionReinforcementDraw draw)
        {
            // The lords are fielded before the gold is paid, as for a banked wave.
            int lords = _invasionWaveBanking.LevyLordsOnly(draw);
            int? pay = _invasionWaveBanking.PayOnly(draw);

            if (lords > 0)
            {
                Logger.Debug("Invasion lords fielded at full strength: invasion=" + draw.Invasion.ClanId +
                            " rule=" + draw.RuleId + " lords=" + lords);
            }

            if (pay == null)
            {
                return;
            }

            Logger.Debug("Invasion wages paid: invasion=" + draw.Invasion.ClanId + " rule=" + draw.RuleId +
                        " pay=" + pay);
        }
    }
}
