using System;
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
        /**
         * The lord of a party the ledger is about to forget, or nothing if that party was fighting no
         * live invasion. The two statements are in this order and not the other one: forgetting the
         * party can spend its group or reconcile it away, and the question would then have no answer
         * left to give. Forgetting is passed in so the order is stated once, here, rather than left to
         * whichever caller happens to hold the ledger. It takes rather than finds: the forgetting is the
         * point of the call, and answering is what it does on the way.
         */
        internal static Hero? TakeLordOfRemovedInvasionParty(InvasionState invasionState, string? clanId,
            string partyId, Hero? leader, Action forgetParty)
        {
            bool wasFightingAnInvasion = leader != null &&
                invasionState.FindFightingGroup(clanId)?.MemberPartyIds.Contains(partyId) == true;

            forgetParty();

            return wasFightingAnInvasion ? leader : null;
        }

        internal static void RemoveDefeated(InvasionState invasionState, Hero hero, bool lostItsInvasionParty)
        {
            if (!IsRemovableInvasionLord(invasionState, hero.Clan?.StringId,
                    isMainHero: hero == Hero.MainHero,
                    belongsToPlayerClan: hero.Clan == Clan.PlayerClan,
                    isChild: hero.IsChild,
                    isAlive: hero.IsAlive,
                    isLord: hero.IsLord,
                    lostItsInvasionParty: lostItsInvasionParty))
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
         *
         * A lord that has just lost its invasion party is asked about no further: flight destroys the
         * party before it says the lord fled, and destroying the last party of a group spends it or
         * deletes it outright, so by the time the answer is wanted the ledger no longer holds it. The
         * answer taken while the party was still a member stands in for the one the ledger can no
         * longer give, and it is the same answer: the group that party belonged to was fighting.
         */
        internal static bool IsRemovableInvasionLord(InvasionState invasionState, string? clanId, bool isMainHero,
            bool belongsToPlayerClan, bool isChild, bool isAlive, bool isLord, bool lostItsInvasionParty) =>
            !isMainHero && !belongsToPlayerClan && !isChild && isAlive && isLord &&
            (lostItsInvasionParty || invasionState.FindFightingGroup(clanId) != null);
    }
}
