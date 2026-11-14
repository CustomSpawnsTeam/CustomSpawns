using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Brings every realm of an invasion line into a peace one of them makes with a kingdom, as the line went to
     * war as one. A realm the line's stance keeps at war with that kingdom, such as one whose target it is,
     * fights on.
     */
    public sealed class BannerlordInvasionLinePeace
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly BannerlordInvasionRealms _bannerlordInvasionRealms;
        private readonly InvasionConquests _invasionConquests;
        private readonly BannerlordInvasionConquestStances _bannerlordInvasionConquestStances;

        public BannerlordInvasionLinePeace(BannerlordInvasionRealms bannerlordInvasionRealms,
            InvasionConquests invasionConquests, BannerlordInvasionConquestStances bannerlordInvasionConquestStances)
        {
            _bannerlordInvasionRealms = bannerlordInvasionRealms;
            _invasionConquests = invasionConquests;
            _bannerlordInvasionConquestStances = bannerlordInvasionConquestStances;
        }

        public void ShareLinePeace(IFaction first, IFaction second)
        {
            ShareLinePeaceOf(first, second);
            ShareLinePeaceOf(second, first);
        }

        private void ShareLinePeaceOf(IFaction realm, IFaction enemy)
        {
            if (enemy is not Kingdom { IsEliminated: false })
            {
                return;
            }

            InvasionConquest? conquest = _invasionConquests.Find(_bannerlordInvasionRealms.FindInvasionClanId(realm));

            if (conquest == null)
            {
                return;
            }

            foreach (InvasionConquest ally in _invasionConquests.FindAllies(conquest))
            {
                IFaction? allyRealm = _bannerlordInvasionRealms.FindRealm(ally);

                // A peace made here shares itself again, and the realms already at peace are skipped.
                if (allyRealm != null && allyRealm != enemy && allyRealm.IsAtWarWith(enemy) &&
                    _bannerlordInvasionConquestStances.FindStance(allyRealm, enemy) != InvasionWarStance.War)
                {
                    MakePeaceAction.Apply(allyRealm, enemy);
                    Logger.Info("Invasion line peace: ally=" + allyRealm.StringId + " realm=" + realm.StringId +
                                " enemy=" + enemy.StringId + " at_war=" + allyRealm.IsAtWarWith(enemy) +
                                " day=" + CampaignTime.Now.ToDays);
                }
            }
        }
    }
}
