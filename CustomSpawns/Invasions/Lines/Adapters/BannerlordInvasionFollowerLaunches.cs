using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CustomSpawns.Data.Dto;
using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Invasions.Conquest;
using CustomSpawns.Invasions.Conquest.Adapters;
using NLog;
using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Invasions.Lines.Adapters
{
    /**
     * Launches the next follower of an invasion line, next to the line's first realm, when the line is owed a
     * realm (see BannerlordInvasionLineRefills). A line
     * sends its configured followers first, and once they are all in the field a copy of its first invasion,
     * founded for the purpose, so every fall of the line opens a new realm.
     */
    public sealed class BannerlordInvasionFollowerLaunches
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Func<InvasionState> _state;
        private readonly BannerlordInvasionConfigurations _bannerlordInvasionConfigurations;
        private readonly BannerlordInvasionLaunches _bannerlordInvasionLaunches;
        private readonly BannerlordInvasionClones _bannerlordInvasionClones;
        private readonly BannerlordInvasionRealms _bannerlordInvasionRealms;
        private readonly InvasionConquests _invasionConquests;
        private readonly InvasionLandingSiteChoice _invasionLandingSiteChoice;
        private readonly InvasionFollowerChoice _invasionFollowerChoice;

        public BannerlordInvasionFollowerLaunches(Func<InvasionState> state,
            BannerlordInvasionConfigurations bannerlordInvasionConfigurations,
            BannerlordInvasionLaunches bannerlordInvasionLaunches, BannerlordInvasionClones bannerlordInvasionClones,
            BannerlordInvasionRealms bannerlordInvasionRealms, InvasionConquests invasionConquests,
            InvasionLandingSiteChoice invasionLandingSiteChoice, InvasionFollowerChoice invasionFollowerChoice)
        {
            _state = state;
            _bannerlordInvasionConfigurations = bannerlordInvasionConfigurations;
            _bannerlordInvasionLaunches = bannerlordInvasionLaunches;
            _bannerlordInvasionClones = bannerlordInvasionClones;
            _bannerlordInvasionRealms = bannerlordInvasionRealms;
            _invasionConquests = invasionConquests;
            _invasionLandingSiteChoice = invasionLandingSiteChoice;
            _invasionFollowerChoice = invasionFollowerChoice;
        }

        // Lands the next follower near the line's first realm, in a kingdom the whole line then conquers.
        public void LaunchFollower(InvasionConquest launcherConquest)
        {
            Invasion? launcher = _state().FindFightingInvasion(launcherConquest.InvasionClanId);
            string? lineTemplateId = launcher == null
                ? null
                : _bannerlordInvasionConfigurations.FindLeaderTemplateId(launcher.SpawnTemplateId) ??
                  launcher.SpawnTemplateId;
            SpawnDto? lineSpawn = lineTemplateId == null
                ? null
                : _bannerlordInvasionConfigurations.FindSpawn(lineTemplateId);

            if (lineSpawn == null)
            {
                Logger.Info("Invasion conquest: no follower leader=" + launcherConquest.InvasionClanId + " line=" +
                            (lineTemplateId ?? "none") + " day=" + CampaignTime.Now.ToDays);
                return;
            }

            SpawnDto? follower = FindConfiguredFollower(lineSpawn.PartyTemplate.StringId);
            string spawnId = (follower ?? lineSpawn).PartyTemplate.StringId;
            InvasionLandingSite? site = FindLandingSite(launcherConquest);
            string? clanId = site == null ? null
                : follower != null
                    ? _bannerlordInvasionLaunches.Launch(follower, site.Value.SettlementId, follower.SpawnClan)
                    : _bannerlordInvasionClones.Launch(lineSpawn, site.Value.SettlementId,
                        launcherConquest.InvasionClanId);
            Invasion? launched = _state().FindFightingInvasion(clanId);
            InvasionConquest? followerConquest = launched == null
                ? null
                : _invasionConquests.BeginFollowing(launched.ClanId, launcherConquest, site!.Value.KingdomId,
                    CampaignTime.Now.ToDays);

            if (launched == null || followerConquest == null)
            {
                Logger.Info("Invasion conquest: launch failed spawn=" + spawnId + " clone=" + (follower == null) +
                            " leader=" + launcherConquest.InvasionClanId + " landing=" +
                            (site?.SettlementId ?? "none") + " day=" + CampaignTime.Now.ToDays);
                return;
            }

            Logger.Info("Invasion conquest: launched spawn=" + launched.SpawnTemplateId + " clan=" + launched.ClanId +
                        " clone=" + (follower == null) + " name=" + FindClanName(launched.ClanId) +
                        " leader=" + launcherConquest.InvasionClanId + " landing=" + site!.Value.SettlementId +
                        " target=" + followerConquest.TargetKingdomId + " distance=" +
                        site.Value.DistanceFromFirstRealm.ToString("0", CultureInfo.InvariantCulture) +
                        " day=" + CampaignTime.Now.ToDays);
            _bannerlordInvasionRealms.DeclareWarOnTarget(launched, followerConquest);
        }

        // The next follower configured for the line's first invasion that can still land; null sends a copy.
        private SpawnDto? FindConfiguredFollower(string lineTemplateId)
        {
            IReadOnlyList<SpawnDto> followers = _bannerlordInvasionConfigurations.FindFollowers(lineTemplateId);
            string? templateId = _invasionFollowerChoice.FindConfiguredFollower(followers.Select(follower =>
                new InvasionFollowerCandidate(follower.PartyTemplate.StringId,
                    _state().HasInvasionOf(follower.PartyTemplate.StringId), follower.SpawnClan.IsEliminated)));

            return followers.FirstOrDefault(follower => follower.PartyTemplate.StringId == templateId);
        }

        private InvasionLandingSite? FindLandingSite(InvasionConquest launcherConquest)
        {
            List<InvasionConquest> line = _invasionConquests.FindAllies(launcherConquest).Append(launcherConquest)
                .ToList();
            List<IFaction> realms = line.Select(_bannerlordInvasionRealms.FindRealm).OfType<IFaction>().Distinct()
                .ToList();

            return _invasionLandingSiteChoice.FindLandingSite(_bannerlordInvasionLaunches.FindLandingSites(realms,
                _bannerlordInvasionRealms.FindRealm(launcherConquest)));
        }

        private static string FindClanName(string clanId) =>
            Clan.FindFirst(clan => clan.StringId == clanId)?.Name?.ToString() ?? "none";
    }
}
