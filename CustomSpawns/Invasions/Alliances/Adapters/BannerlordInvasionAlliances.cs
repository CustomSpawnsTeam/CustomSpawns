using System.Linq;
using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Invasions.Conquest;
using CustomSpawns.Invasions.Conquest.Adapters;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace CustomSpawns.Invasions.Alliances.Adapters
{
    // Keeps the realms of one invasion line, and the kingdoms of a coalition, at peace and allied with each other.
    public sealed class BannerlordInvasionAlliances : IInvasionKingdomAlliances
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
            AllyRealms(invasion.FindLeadingClan()?.MapFaction, _bannerlordInvasionRealms.FindRealm(allyConquest));
        }

        public void Ally(string kingdomId, string otherKingdomId) =>
            AllyRealms(FindKingdom(kingdomId), FindKingdom(otherKingdomId));

        private static Kingdom? FindKingdom(string kingdomId) =>
            Kingdom.All.FirstOrDefault(kingdom => kingdom.StringId == kingdomId);

        private static void AllyRealms(IFaction? realm, IFaction? allyRealm)
        {
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
