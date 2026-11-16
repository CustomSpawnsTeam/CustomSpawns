using System.Collections.Generic;
using System.Linq;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * A beaten invasion lord is gone for good. Vanilla treats a captured or fugitive lord as a lord
     * temporarily without a party and hands it a fresh one on its clan's next daily tick, so a lord the
     * player beat returned a day later at full strength. Removing it at the moment of defeat stops that
     * at its source instead of cleaning up after it.
     *
     * The removal waits for the next campaign tick. Capture and flight fire from inside vanilla's own
     * loops over a settlement's heroes, and removing the lord there -- or making his successor -- shrinks
     * the list under the loop and crashes the game.
     */
    public sealed class InvasionLordRemoval
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly InvasionState _invasionState;
        private readonly InvaderHeroCreator _invaderHeroCreator;
        private readonly List<(Hero Hero, bool LostItsInvasionParty)> _defeated = new();

        public InvasionLordRemoval(InvasionState invasionState, InvaderHeroCreator invaderHeroCreator)
        {
            _invasionState = invasionState;
            _invaderHeroCreator = invaderHeroCreator;
        }

        public void Defer(Hero hero, bool lostItsInvasionParty)
        {
            _defeated.Add((hero, lostItsInvasionParty));
        }

        public void RemoveDeferred()
        {
            if (_defeated.Count == 0)
            {
                return;
            }

            var defeated = _defeated.ToList();
            _defeated.Clear();

            foreach (var (hero, lostItsInvasionParty) in defeated)
            {
                RemoveDefeated(hero, lostItsInvasionParty);
            }
        }

        private void RemoveDefeated(Hero hero, bool lostItsInvasionParty)
        {
            if (!_invasionState.IsRemovableInvasionLord(hero.Clan?.StringId,
                    isMainHero: hero == Hero.MainHero,
                    belongsToPlayerClan: hero.Clan == Clan.PlayerClan,
                    isChild: hero.IsChild,
                    isAlive: hero.IsAlive,
                    isLord: hero.IsLord,
                    lostItsInvasionParty: lostItsInvasionParty))
            {
                return;
            }

            Logger.Info("Invasion defeated lord removal: day=" + CampaignTime.Now.ToDays +
                        " hero=" + hero.StringId + " clan=" + hero.Clan?.StringId +
                        " state=" + hero.HeroState + " party=" + hero.PartyBelongedTo?.StringId +
                        " lost_invasion_party=" + lostItsInvasionParty +
                        " cause=forced_remove_after_defeat");

            /*
             * Vanilla destroys a clan whose leader dies with no living adult lord left to succeed him, and
             * every party of the clan goes with it -- the invasion's and every other spawn sharing the
             * clan. A clan always keeps a leader, so one is made before the last is removed, and vanilla's
             * own succession hands it the clan.
             */
            if (hero.Clan is { } clan && _invasionState.NeedsSuccessor(clan.Leader == hero,
                    clan.Heroes.Select(other => (other == hero, other.IsChild, other.IsAlive, other.IsLord))))
            {
                _invaderHeroCreator.CreateNewLord(clan, clan.HomeSettlement);
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
    }
}
