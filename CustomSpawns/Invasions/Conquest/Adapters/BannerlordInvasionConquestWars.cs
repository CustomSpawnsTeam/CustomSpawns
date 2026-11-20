using System;
using System.Linq;
using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Invasions.Alliances.Adapters;
using CustomSpawns.Invasions.Lines;
using NLog;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Invasions.Conquest.Adapters
{
    /**
     * Keeps an invasion at war with the kingdom it is conquering and leaves every other kingdom to its own
     * diplomacy. Once that kingdom has fallen its line is credited with beating it, and the invader turns on the
     * bordering kingdom it gains most by fighting.
     */
    public sealed class BannerlordInvasionConquestWars
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Func<InvasionState> _state;
        private readonly BannerlordInvasionConfigurations _bannerlordInvasionConfigurations;
        private readonly BannerlordInvasionRealms _bannerlordInvasionRealms;
        private readonly BannerlordInvasionAlliances _bannerlordInvasionAlliances;
        private readonly BannerlordInvasionRetargets _bannerlordInvasionRetargets;
        private readonly BannerlordInvasionFrontOpenings _bannerlordInvasionFrontOpenings;
        private readonly BannerlordInvasionConquestStances _bannerlordInvasionConquestStances;
        private readonly InvasionConquests _invasionConquests;
        private readonly InvasionLineClans _invasionLineClans;
        private readonly InvasionBeatenKingdoms _invasionBeatenKingdoms;

        public BannerlordInvasionConquestWars(Func<InvasionState> state,
            BannerlordInvasionConfigurations bannerlordInvasionConfigurations,
            BannerlordInvasionRealms bannerlordInvasionRealms,
            BannerlordInvasionAlliances bannerlordInvasionAlliances,
            BannerlordInvasionRetargets bannerlordInvasionRetargets,
            BannerlordInvasionFrontOpenings bannerlordInvasionFrontOpenings,
            BannerlordInvasionConquestStances bannerlordInvasionConquestStances,
            InvasionConquests invasionConquests, InvasionLineClans invasionLineClans,
            InvasionBeatenKingdoms invasionBeatenKingdoms)
        {
            _state = state;
            _bannerlordInvasionConfigurations = bannerlordInvasionConfigurations;
            _bannerlordInvasionRealms = bannerlordInvasionRealms;
            _bannerlordInvasionAlliances = bannerlordInvasionAlliances;
            _bannerlordInvasionRetargets = bannerlordInvasionRetargets;
            _bannerlordInvasionFrontOpenings = bannerlordInvasionFrontOpenings;
            _bannerlordInvasionConquestStances = bannerlordInvasionConquestStances;
            _invasionConquests = invasionConquests;
            _invasionLineClans = invasionLineClans;
            _invasionBeatenKingdoms = invasionBeatenKingdoms;
        }

        // The first fief an invasion that conquers takes from a kingdom makes that kingdom its target.
        public void ObserveConquest(Clan? newOwnerClan, Clan? previousOwnerClan)
        {
            Invasion? invasion = _state().FindFightingInvasion(newOwnerClan?.StringId);

            if (invasion == null || _bannerlordInvasionConfigurations.FindConquestRules(invasion.SpawnTemplateId) == null)
            {
                return;
            }

            // A follower that lost track of its leader picks up the alliance again rather than standing alone.
            InvasionConquest? leader = FindLeaderConquest(invasion);
            InvasionConquest? conquest = leader != null
                ? _invasionConquests.BeginFollowing(invasion.ClanId, leader, previousOwnerClan?.Kingdom?.StringId,
                    CampaignTime.Now.ToDays)
                : _invasionConquests.Begin(invasion.ClanId, previousOwnerClan?.Kingdom?.StringId,
                    CampaignTime.Now.ToDays);

            if (conquest == null)
            {
                return;
            }

            Logger.Info("Invasion conquest: begun spawn=" + invasion.SpawnTemplateId + " leader=" +
                        (conquest.LeaderClanId ?? "none") +
                        " target=" + conquest.TargetKingdomId + " day=" + conquest.StartedDay);
            _bannerlordInvasionRealms.DeclareWarOnTarget(invasion, conquest);
        }

        public void UpdateDaily()
        {
            InvasionState state = _state();

            foreach (InvasionConquest conquest in _invasionConquests.All.ToList())
            {
                Invasion? invasion = state.FindFightingInvasion(conquest.InvasionClanId);
                InvasionConquestRules? rules = invasion == null
                    ? null
                    : _bannerlordInvasionConfigurations.FindConquestRules(invasion.SpawnTemplateId);

                if (invasion == null || rules == null)
                {
                    _invasionConquests.Forget(conquest.InvasionClanId);
                    continue;
                }

                foreach (InvasionConquest ally in _invasionConquests.FindAllies(conquest).ToList())
                {
                    _bannerlordInvasionAlliances.KeepAlliance(invasion, ally);
                }

                bool hasFallen = _bannerlordInvasionRealms.HasFallen(conquest.TargetKingdomId);

                // Seen before the invasion turns away, while the fallen kingdom is still its target.
                if (hasFallen)
                {
                    _invasionBeatenKingdoms.ObserveFall(FindLineTemplateId(invasion), conquest.TargetKingdomId);
                }

                if ((hasFallen || IsAllyRealm(invasion, conquest.TargetKingdomId)) &&
                    !_bannerlordInvasionRetargets.Retarget(invasion, conquest))
                {
                    continue;
                }

                // A realm that joins the newest realm's front stays there rather than turn away from it.
                if (!JoinFront(invasion, conquest))
                {
                    _bannerlordInvasionRetargets.Reconsider(invasion, conquest, rules.Value);
                }

                _bannerlordInvasionRealms.DeclareWarOnTarget(invasion, conquest);
                _bannerlordInvasionFrontOpenings.OpenFront(invasion, conquest, rules.Value);
                _bannerlordInvasionConquestStances.LogThreat(invasion, conquest);
            }
        }

        /**
         * Turns an older realm of the line on the kingdom its newest realm conquers, bordering it or not; false while
         * there is no such standing kingdom to join. The war on it is declared with the daily war on the target.
         */
        private bool JoinFront(Invasion invasion, InvasionConquest conquest)
        {
            string? kingdomId = _invasionConquests.FindFrontToJoin(conquest);

            if (kingdomId == null || _bannerlordInvasionRealms.HasFallen(kingdomId) ||
                IsAllyRealm(invasion, kingdomId))
            {
                return false;
            }

            if (kingdomId == conquest.TargetKingdomId)
            {
                return true;
            }

            string previousTargetId = conquest.TargetKingdomId;
            conquest.Retarget(kingdomId, CampaignTime.Now.ToDays);
            Logger.Info("Invasion conquest: joined spawn=" + invasion.SpawnTemplateId + " from=" + previousTargetId +
                        " target=" + kingdomId + " ally=" + _invasionConquests.FindLauncher(conquest).FollowerClanId +
                        " day=" + CampaignTime.Now.ToDays);
            return true;
        }

        /**
         * The conquest of the invasion this one follows, while that one still conquers: the invasion a copy was
         * launched for, or the one a configured follower is configured to follow. A copy runs its line leader's spawn
         * template, so the template alone would mistake it for that leader.
         */
        private InvasionConquest? FindLeaderConquest(Invasion invasion)
        {
            string? leaderTemplateId = _bannerlordInvasionConfigurations.FindLeaderTemplateId(invasion.SpawnTemplateId);

            return _invasionConquests.All.FirstOrDefault(conquest => conquest.LeaderClanId == null &&
                _invasionLineClans.CanLead(invasion.ClanId, conquest.InvasionClanId, leaderTemplateId != null &&
                    _state().FindFightingInvasion(conquest.InvasionClanId)?.SpawnTemplateId == leaderTemplateId));
        }

        // A copy runs its line's first spawn; a configured follower names the spawn it follows.
        private string FindLineTemplateId(Invasion invasion) =>
            _bannerlordInvasionConfigurations.FindLeaderTemplateId(invasion.SpawnTemplateId) ?? invasion.SpawnTemplateId;

        // A target that is an allied invasion's realm, as a save from before allies were left out of the choice holds.
        private bool IsAllyRealm(Invasion invasion, string kingdomId)
        {
            Kingdom? kingdom = _bannerlordInvasionRealms.FindKingdom(kingdomId);

            return kingdom != null &&
                   _invasionConquests.AreAllied(invasion.ClanId, _bannerlordInvasionRealms.FindInvasionClanId(kingdom));
        }
    }
}
