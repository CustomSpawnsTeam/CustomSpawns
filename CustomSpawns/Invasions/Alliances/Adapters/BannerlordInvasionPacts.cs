using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Invasions.Conquest;
using CustomSpawns.Invasions.Conquest.Adapters;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace CustomSpawns.Invasions.Alliances.Adapters
{
    /**
     * Keeps every other kingdom from allying or trading with an invasion's realm: only the realms of one line deal
     * with each other. A pact made before the rule held, or before a kingdom became an invasion's realm, is ended.
     */
    public sealed class BannerlordInvasionPacts
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly BannerlordInvasionRealms _bannerlordInvasionRealms;
        private readonly InvasionConquests _invasionConquests;

        public BannerlordInvasionPacts(BannerlordInvasionRealms bannerlordInvasionRealms,
            InvasionConquests invasionConquests)
        {
            _bannerlordInvasionRealms = bannerlordInvasionRealms;
            _invasionConquests = invasionConquests;
        }

        public bool MayMakePact(Kingdom first, Kingdom second) =>
            _invasionConquests.MayMakePact(_bannerlordInvasionRealms.FindInvasionClanId(first),
                _bannerlordInvasionRealms.FindInvasionClanId(second));

        public void EndForbiddenPacts()
        {
            IAllianceCampaignBehavior? alliances = Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>();
            ITradeAgreementsCampaignBehavior? tradeAgreements =
                Campaign.Current.GetCampaignBehavior<ITradeAgreementsCampaignBehavior>();
            List<Kingdom> kingdoms = Kingdom.All.Where(kingdom => !kingdom.IsEliminated).ToList();

            for (int index = 0; index < kingdoms.Count; index++)
            {
                foreach (Kingdom other in kingdoms.Skip(index + 1).Where(other => !MayMakePact(kingdoms[index], other)))
                {
                    EndPacts(kingdoms[index], other, alliances, tradeAgreements);
                }
            }
        }

        private static void EndPacts(Kingdom kingdom, Kingdom other, IAllianceCampaignBehavior? alliances,
            ITradeAgreementsCampaignBehavior? tradeAgreements)
        {
            if (alliances != null && kingdom.IsAllyWith(other))
            {
                alliances.EndAlliance(kingdom, other);
                Logger.Info("Invasion pact: alliance ended kingdom=" + kingdom.StringId + " other=" + other.StringId +
                            " day=" + CampaignTime.Now.ToDays);
            }

            if (tradeAgreements != null && tradeAgreements.HasTradeAgreement(kingdom, other, out _))
            {
                tradeAgreements.EndTradeAgreement(kingdom, other);
                Logger.Info("Invasion pact: trade agreement ended kingdom=" + kingdom.StringId + " other=" +
                            other.StringId + " day=" + CampaignTime.Now.ToDays);
            }
        }
    }
}
