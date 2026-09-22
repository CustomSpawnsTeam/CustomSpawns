using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * A beaten invasion lord is gone for good. Vanilla treats a captured or fugitive lord as a lord
     * temporarily without a party and hands it a fresh one on its clan's next daily tick, so a lord the
     * player beat returned a day later at full strength. Removing it at the moment of defeat stops that
     * at its source instead of cleaning up after it.
     */
    internal static class InvasionLordRemoval
    {
        internal static void RemoveDefeated(InvasionState invasionState, Hero hero)
        {
            if (!IsRemovableInvasionLord(invasionState, hero.Clan?.StringId, hero == Hero.MainHero,
                    hero.Clan == Clan.PlayerClan))
            {
                return;
            }

            /*
             * Removal rather than execution: it kills with no killer and the "Lost" detail, and vanilla
             * charges the relation and honor penalties only for an execution the player performed, so the
             * lord merely disappears. It also ends captivity first, which is what makes capture, escape and
             * ransom all end the same way -- a ransom needs no handling of its own, the lord is already
             * gone by the time one could be offered.
             */
            KillCharacterAction.ApplyByRemove(hero, showNotification: false, isForced: true);
        }

        /**
         * Only a hero of a clan that is still fighting a live invasion is ours to remove, and the player
         * and its clan are never ours at all.
         */
        internal static bool IsRemovableInvasionLord(InvasionState invasionState, string? clanId, bool isMainHero,
            bool belongsToPlayerClan) =>
            !isMainHero && !belongsToPlayerClan && invasionState.FindFightingGroups(clanId).Any();
    }
}
