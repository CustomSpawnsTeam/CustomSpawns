using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace CustomSpawns.Invasions.Adapters
{
    // Keeps the realms of one invasion line at peace and allied with each other.
    public sealed class BannerlordInvasionAlliances
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly BannerlordInvasionRealms _bannerlordInvasionRealms;

        public BannerlordInvasionAlliances(BannerlordInvasionRealms bannerlordInvasionRealms)
        {
            _bannerlordInvasionRealms = bannerlordInvasionRealms;
        }

        /**
         * A leader and the followers launched for it are one cause. Whatever drew two of them into a war with
         * each other -- their configurations all make war on everyone -- is ended, and as kingdoms they stay
         * allied, the alliance renewed whenever it lapses.
         */
        public void KeepAlliance(Invasion invasion, InvasionConquest allyConquest)
        {
            IFaction? realm = invasion.FindLeadingClan()?.MapFaction;
            IFaction? allyRealm = _bannerlordInvasionRealms.FindRealm(allyConquest);

            if (realm == null || allyRealm == null || realm == allyRealm)
            {
                return;
            }

            if (realm.IsAtWarWith(allyRealm))
            {
                MakePeaceAction.Apply(realm, allyRealm);
                Logger.Info("Invasion alliance: peace realm=" + realm.StringId + " ally=" + allyRealm.StringId +
                            " day=" + CampaignTime.Now.ToDays);
            }

            IAllianceCampaignBehavior? alliances = Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>();

            if (realm is Kingdom kingdom && allyRealm is Kingdom allyKingdom && alliances != null &&
                !kingdom.IsAllyWith(allyKingdom))
            {
                alliances.StartAlliance(allyKingdom, kingdom);
                Logger.Info("Invasion alliance: allied realm=" + kingdom.StringId + " ally=" + allyKingdom.StringId +
                            " allied=" + kingdom.IsAllyWith(allyKingdom) + " day=" + CampaignTime.Now.ToDays);
            }
        }
    }
}
