using System.Collections.Generic;
using System.Linq;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Draws every realm of an invasion line into a war a kingdom makes with any one of them, whichever side
     * declared it. Vanilla only puts a call to war to the allies' vote; a line answers it at once. The realm the
     * war began with leads it. The rebels of a town go to war with the realms InvasionRebellions names.
     */
    public sealed class BannerlordInvasionCallsToWar
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly BannerlordInvasionRealms _bannerlordInvasionRealms;
        private readonly InvasionConquests _invasionConquests;
        private readonly InvasionRebellions _invasionRebellions;

        public BannerlordInvasionCallsToWar(BannerlordInvasionRealms bannerlordInvasionRealms,
            InvasionConquests invasionConquests, InvasionRebellions invasionRebellions)
        {
            _bannerlordInvasionRealms = bannerlordInvasionRealms;
            _invasionConquests = invasionConquests;
            _invasionRebellions = invasionRebellions;
        }

        public void CallAlliesToWar(IFaction first, IFaction second, DeclareWarAction.DeclareWarDetail detail)
        {
            if (detail == DeclareWarAction.DeclareWarDetail.CausedByRebellion)
            {
                DeclareWarOnRebels(first, second);
                DeclareWarOnRebels(second, first);
                return;
            }

            CallAlliesOf(first, second);
            CallAlliesOf(second, first);
        }

        // A war declared on the rebels, a clan, calls no ally in.
        private void DeclareWarOnRebels(IFaction rebels, IFaction rebelledAgainst)
        {
            if (rebels is not Clan { IsRebelClan: true, IsEliminated: false })
            {
                return;
            }

            List<IFaction> realms = _invasionRebellions
                .FindRealmsAtWarWithRebels(_bannerlordInvasionRealms.FindInvasionClanId(rebelledAgainst),
                    conquest => _bannerlordInvasionRealms.FindRealm(conquest)?.IsAtWarWith(rebelledAgainst) == true)
                .Select(_bannerlordInvasionRealms.FindRealm).OfType<IFaction>().ToList();

            foreach (IFaction realm in realms.Where(realm => !realm.IsEliminated && !realm.IsAtWarWith(rebels)))
            {
                DeclareWarAction.ApplyByDefault(realm, rebels);
                Logger.Info("Invasion war on rebels: realm=" + realm.StringId + " rebels=" + rebels.StringId +
                            " rebelled_against=" + rebelledAgainst.StringId + " at_war=" +
                            realm.IsAtWarWith(rebels) + " day=" + CampaignTime.Now.ToDays);
            }
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

            string? invasionClanId = _bannerlordInvasionRealms.FindInvasionClanId(realm);
            List<IFaction> allyRealms = _invasionConquests
                .FindAlliesCalledToWar(invasionClanId, _bannerlordInvasionRealms.FindInvasionClanId(enemy))
                .Select(_bannerlordInvasionRealms.FindRealm).OfType<IFaction>().Where(ally => ally != enemy).ToList();

            // A war none of the line fought yet began with this realm; one it joins stays its first realm's.
            if (_invasionConquests.Find(invasionClanId) != null && !allyRealms.Any(ally => ally.IsAtWarWith(enemy)))
            {
                _invasionConquests.BeginWar(invasionClanId, enemy.StringId);
                Logger.Info("Invasion war led: realm=" + realm.StringId + " enemy=" + enemy.StringId + " day=" +
                            CampaignTime.Now.ToDays);
            }

            foreach (IFaction allyRealm in allyRealms)
            {
                // A declaration made here calls the line in again, and the realms already at war are skipped.
                if (!allyRealm.IsAtWarWith(enemy))
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
