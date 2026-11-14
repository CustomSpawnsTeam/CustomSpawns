using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Draws every realm of an invasion line into a war a kingdom makes with any one of them, whichever side
     * declared it. Vanilla only puts a call to war to the allies' vote; a line answers it at once.
     */
    public sealed class BannerlordInvasionCallsToWar
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly BannerlordInvasionRealms _bannerlordInvasionRealms;
        private readonly InvasionConquests _invasionConquests;

        public BannerlordInvasionCallsToWar(BannerlordInvasionRealms bannerlordInvasionRealms,
            InvasionConquests invasionConquests)
        {
            _bannerlordInvasionRealms = bannerlordInvasionRealms;
            _invasionConquests = invasionConquests;
        }

        public void CallAlliesToWar(IFaction first, IFaction second)
        {
            CallAlliesOf(first, second);
            CallAlliesOf(second, first);
        }

        /**
         * Only a war with a kingdom calls the allies in: clans and minor factions that leave a fallen kingdom
         * declare their wars clan by clan, and the line is not drawn into each of those.
         */
        private void CallAlliesOf(IFaction realm, IFaction enemy)
        {
            if (enemy is not Kingdom { IsEliminated: false })
            {
                return;
            }

            foreach (InvasionConquest ally in _invasionConquests.FindAlliesCalledToWar(
                         _bannerlordInvasionRealms.FindInvasionClanId(realm),
                         _bannerlordInvasionRealms.FindInvasionClanId(enemy)))
            {
                IFaction? allyRealm = _bannerlordInvasionRealms.FindRealm(ally);

                // A declaration made here calls the line in again, and the realms already at war are skipped.
                if (allyRealm != null && allyRealm != enemy && !allyRealm.IsAtWarWith(enemy))
                {
                    DeclareWarAction.ApplyByDefault(allyRealm, enemy);
                    Logger.Info("Invasion call to war: ally=" + allyRealm.StringId + " realm=" + realm.StringId +
                                " enemy=" + enemy.StringId + " at_war=" + allyRealm.IsAtWarWith(enemy) +
                                " day=" + CampaignTime.Now.ToDays);
                }
            }
        }
    }
}
