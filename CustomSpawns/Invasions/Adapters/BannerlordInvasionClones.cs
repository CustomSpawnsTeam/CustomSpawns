using CustomSpawns.Data.Dto;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Launches a copy of an invasion line's first invasion once the line has no configured follower left to send:
     * the same spawn and settings, fighting for a clan founded for it and named after its place in the line. The
     * clan joins no kingdom; like any invasion clan without one, it founds its own realm, which takes its name,
     * with the first fief it takes.
     */
    public sealed class BannerlordInvasionClones
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly BannerlordInvasionLaunches _bannerlordInvasionLaunches;
        private readonly BannerlordInvasionClanFounding _bannerlordInvasionClanFounding;
        private readonly IInvasionClanRepository _invasionClans;
        private readonly InvasionLineClans _invasionLineClans;
        private readonly InvasionCloneDiplomacy _invasionCloneDiplomacy;
        private readonly InvasionCloneNames _invasionCloneNames = new();

        public BannerlordInvasionClones(BannerlordInvasionLaunches bannerlordInvasionLaunches,
            BannerlordInvasionClanFounding bannerlordInvasionClanFounding, IInvasionClanRepository invasionClans,
            InvasionLineClans invasionLineClans, InvasionCloneDiplomacy invasionCloneDiplomacy)
        {
            _bannerlordInvasionLaunches = bannerlordInvasionLaunches;
            _bannerlordInvasionClanFounding = bannerlordInvasionClanFounding;
            _invasionClans = invasionClans;
            _invasionLineClans = invasionLineClans;
            _invasionCloneDiplomacy = invasionCloneDiplomacy;
        }

        /**
         * The copy's clan id, or null when its clan could not be founded or its spawn failed. A clan whose spawn
         * failed is destroyed again rather than left standing with no invasion to fight for.
         */
        public string? Launch(SpawnDto lineSpawn, string landingSettlementId, string launcherClanId)
        {
            Settlement? landing = Settlement.Find(landingSettlementId);
            Clan model = lineSpawn.SpawnClan;
            Banner banner = Banner.CreateRandomClanBanner();
            TextObject name = new(_invasionCloneNames.Name(model.Name.ToString(),
                _invasionLineClans.NextCloneNumber(model.StringId)));
            Clan? clan = landing == null
                ? null
                : _bannerlordInvasionClanFounding.Found(_invasionLineClans.NextCloneClanId(model.StringId), model,
                    landing, banner, banner.GetPrimaryColor(), banner.GetFirstIconColor(), name);

            if (clan == null)
            {
                return null;
            }

            CampaignEventDispatcher.Instance.OnClanCreated(clan, false);
            string? clanId = _bannerlordInvasionLaunches.Launch(lineSpawn, landingSettlementId, clan);

            if (clanId == null)
            {
                Logger.Info("Invasion conquest: copy failed to land clan=" + clan.StringId + " model=" +
                            model.StringId + " landing=" + landingSettlementId);
                DestroyClanAction.Apply(clan);
                return null;
            }

            _invasionLineClans.Register(clan.StringId, launcherClanId, model.StringId);
            _invasionCloneDiplomacy.Inherit(clan.StringId, model.StringId);
            _invasionClans.Register(lineSpawn.PartyTemplate.StringId, clan.StringId);
            return clanId;
        }
    }
}
