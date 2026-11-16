using System.Collections.Generic;
using System.Linq;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Sends the lords of a coalition into the field battles the other kingdoms of the coalition fight against the
     * line nearby, and the line's lords into those of the line's other realms, as vanilla does for a battle of the
     * lord's own realm. A lord riding in another's army, at sea or held by a siege is left to vanilla.
     */
    public sealed class BannerlordInvasionAlliedBattles
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // How far vanilla looks for a battle to join, in encounter joining radii.
        private const float SightInJoiningRadii = 6f;
        private const float MinimumAggressiveness = 0.01f;
        private readonly BannerlordInvasionCoalitionFronts _bannerlordInvasionCoalitionFronts;
        private readonly InvasionAlliedBattles _invasionAlliedBattles;

        public BannerlordInvasionAlliedBattles(BannerlordInvasionCoalitionFronts bannerlordInvasionCoalitionFronts,
            InvasionAlliedBattles invasionAlliedBattles)
        {
            _bannerlordInvasionCoalitionFronts = bannerlordInvasionCoalitionFronts;
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
                }
            }

            if (bestEnemySide == null)
            {
                return;
            }

            behavior = AiBehavior.EngageParty;
            targetParty = bestEnemySide.LeaderParty.MobileParty;
            score = bestScore;
            Logger.Debug("Invasion allied battle joined: party=" + party.StringId + " enemy=" +
                         targetParty.StringId + " strength=" + strength + " score=" + bestScore);
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

        private bool IsInvasionSide(IFaction faction) =>
            _bannerlordInvasionCoalitionFronts.IsMember(faction) ||
            _bannerlordInvasionCoalitionFronts.IsLineRealm(faction);

        // A coalition against a line, or a line against a kingdom of the coalition.
        private bool IsRival(IFaction faction, IFaction enemy) =>
            (_bannerlordInvasionCoalitionFronts.IsMember(faction) &&
             _bannerlordInvasionCoalitionFronts.IsLineRealm(enemy)) ||
            (_bannerlordInvasionCoalitionFronts.IsLineRealm(faction) &&
             _bannerlordInvasionCoalitionFronts.IsMember(enemy));

        private static float FindStrength(MapEventSide side) =>
            side.Parties.Sum(battleParty => battleParty.Party.EstimatedStrength);
    }
}
