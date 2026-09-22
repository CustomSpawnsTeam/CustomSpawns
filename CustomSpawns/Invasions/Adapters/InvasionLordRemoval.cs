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
            if (!IsRemovableInvasionLord(invasionState, hero.Clan?.StringId,
                    isMainHero: hero == Hero.MainHero,
                    belongsToPlayerClan: hero.Clan == Clan.PlayerClan,
                    isChild: hero.IsChild,
                    isAlive: hero.IsAlive,
                    isLord: hero.IsLord))
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
         * and its clan are never ours at all. An invasion may be configured to use an existing clan, and a
         * retaken town takes prisoner every clan hero residing in it, so the clan's spouses, children and
         * quest heroes reach us as readily as its lords do. The living adult lord test is vanilla's own,
         * the one it uses to decide which clan hero can succeed a dead leader: an occupation alone does not
         * settle it, because a lord's child carries the lord occupation from birth.
         */
        internal static bool IsRemovableInvasionLord(InvasionState invasionState, string? clanId, bool isMainHero,
            bool belongsToPlayerClan, bool isChild, bool isAlive, bool isLord) =>
            !isMainHero && !belongsToPlayerClan && !isChild && isAlive && isLord &&
            invasionState.FindFightingGroups(clanId).Any();
    }
}
