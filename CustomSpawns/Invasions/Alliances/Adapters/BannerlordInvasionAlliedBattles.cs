using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Invasions.Conquest.Adapters;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace CustomSpawns.Invasions.Alliances.Adapters
{
    /**
     * Sends a lord into the field battles fought nearby against an invasion realm it is at war with, or, for a lord of
     * an invasion realm, against a kingdom it is at war with, as vanilla does for a battle of the lord's own realm, a
     * coalition formed or not. A lord riding in another's army, at sea or held by a siege is left to vanilla.
     */
    public sealed class BannerlordInvasionAlliedBattles
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // How far vanilla looks for a battle to join, in encounter joining radii.
        private const float SightInJoiningRadii = 6f;
        private const float MinimumAggressiveness = 0.01f;
        private readonly BannerlordInvasionRealms _bannerlordInvasionRealms;
        private readonly InvasionAlliedBattles _invasionAlliedBattles;

        // The battle each lord was last logged joining: vanilla asks a lord's initiative several times a tick.
        private readonly Dictionary<MobileParty, MapEvent> _loggedBattles = new();

        public BannerlordInvasionAlliedBattles(BannerlordInvasionRealms bannerlordInvasionRealms,
            InvasionAlliedBattles invasionAlliedBattles)
        {
            _bannerlordInvasionRealms = bannerlordInvasionRealms;
            _invasionAlliedBattles = invasionAlliedBattles;
        }

        public void JoinAlliedBattle(MobileParty party, ref AiBehavior behavior, ref MobileParty targetParty,
            ref float score)
        {
            if (party.IsMainParty || !party.IsLordParty || party.LeaderHero == null || party.IsCurrentlyAtSea ||
                party.MapEvent != null || (party.Army != null && party.Army.LeaderParty != party) ||
                party.BesiegerCamp != null || party.CurrentSettlement?.SiegeEvent != null ||
                party.Aggressiveness < MinimumAggressiveness || !IsInvasionSide(party.MapFaction))
            {
                return;
            }

            float strength = party.Army?.EstimatedStrength ?? party.Party.EstimatedStrength;
            MapEventSide? bestEnemySide = null;
            float bestScore = 0f;
            bool fled = false;

            foreach (MapEventSide enemySide in FindEnemySides(party))
            {
                float joinScore = _invasionAlliedBattles.FindJoinScore(strength, FindStrength(enemySide.OtherSide),
                    FindStrength(enemySide));
                bool isFleeingTheBattle = behavior == AiBehavior.FleeToPoint &&
                                          targetParty?.MapEvent == enemySide.MapEvent;

                if (joinScore > bestScore &&
                    _invasionAlliedBattles.OverridesInitiative(joinScore, score, isFleeingTheBattle))
                {
                    bestEnemySide = enemySide;
                    bestScore = joinScore;
                    fled = isFleeingTheBattle;
                }
            }

            if (bestEnemySide == null)
            {
                return;
            }

            behavior = AiBehavior.EngageParty;
            targetParty = bestEnemySide.LeaderParty.MobileParty;
            score = bestScore;

            if (_loggedBattles.TryGetValue(party, out MapEvent logged) && logged == bestEnemySide.MapEvent)
            {
                return;
            }

            _loggedBattles[party] = bestEnemySide.MapEvent;
            Logger.Info("Invasion allied battle joined: party=" + party.StringId + " faction=" +
                        party.MapFaction.StringId + " enemy=" + targetParty.StringId + " enemy_faction=" +
                        targetParty.MapFaction?.StringId + " strength=" + strength + " allies=" +
                        FindStrength(bestEnemySide.OtherSide) + " enemy_strength=" + FindStrength(bestEnemySide) +
                        " score=" + bestScore + " fled=" + fled + " day=" + (int)CampaignTime.Now.ToDays);
        }

        // The enemy's side of each field battle nearby the lord can join, its allies fighting the other side.
        private IEnumerable<MapEventSide> FindEnemySides(MobileParty party)
        {
            float sight = Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius * SightInJoiningRadii;
            HashSet<MapEvent> battles = new();
            LocatableSearchData<MobileParty> data =
                MobileParty.StartFindingLocatablesAroundPosition(party.Position.ToVec2(), sight);

            for (MobileParty found = MobileParty.FindNextLocatable(ref data);
                 found != null;
                 found = MobileParty.FindNextLocatable(ref data))
            {
                MapEvent? battle = found.MapEvent;

                // Vanilla leaves the player's battle to the player's encounter.
                if (battle == null || !battle.IsFieldBattle || battle == MobileParty.MainParty.MapEvent ||
                    !battles.Add(battle))
                {
                    continue;
                }

                foreach (BattleSideEnum side in new[] { BattleSideEnum.Attacker, BattleSideEnum.Defender })
                {
                    MapEventSide enemySide = battle.GetMapEventSide(side);

                    if (enemySide.LeaderParty?.IsMobile == true && IsRival(party.MapFaction,
                            enemySide.LeaderParty.MapFaction) &&
                        battle.CanPartyJoinBattle(party.Party, enemySide.OtherSide.MissionSide))
                    {
                        yield return enemySide;
                    }
                }
            }
        }

        // An invasion realm, or a kingdom at war with one.
        private bool IsInvasionSide(IFaction faction) =>
            IsInvasionRealm(faction) ||
            Kingdom.All.Any(kingdom => !kingdom.IsEliminated && IsInvasionRealm(kingdom) && faction.IsAtWarWith(kingdom));

        private bool IsRival(IFaction faction, IFaction enemy) =>
            _invasionAlliedBattles.AreRivals(IsInvasionRealm(faction), IsInvasionRealm(enemy), enemy is Kingdom,
                faction.IsAtWarWith(enemy));

        private bool IsInvasionRealm(IFaction faction) =>
            _bannerlordInvasionRealms.FindInvasionClanId(faction) != null;

        private static float FindStrength(MapEventSide side) =>
            side.Parties.Sum(battleParty => battleParty.Party.EstimatedStrength);
    }
}
