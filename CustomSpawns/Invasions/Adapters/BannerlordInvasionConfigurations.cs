using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Invasions.Conquest;
using CustomSpawns.Invasions.Lords;
using CustomSpawns.Invasions.Reinforcements;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Invasions.Adapters
{
    public sealed class BannerlordInvasionConfigurations : IInvasionConfigurations, IInvasionLordPartySizes
    {
        private readonly SpawnDao _spawnDao;

        public BannerlordInvasionConfigurations(SpawnDao spawnDao)
        {
            _spawnDao = spawnDao;
        }

        // A rule whose party template the game no longer knows has nothing left to send.
        public InvasionWave? FindWave(string spawnTemplateId, string ruleId)
        {
            InvasionData? configuration = Find(spawnTemplateId);
            InvasionReinforcementRuleData? rule =
                configuration?.ReinforcementRules.FirstOrDefault(candidate => candidate.Id == ruleId);

            if (rule == null ||
                Campaign.Current.ObjectManager.GetObject<PartyTemplateObject>(rule.PartyTemplateId) == null)
            {
                return null;
            }

            return new InvasionWave(rule.PartyTemplateId, rule.IntervalInDays, rule.LordsPerWave,
                configuration!.MaxLords, configuration.LordsPerTownHeld, rule.MenPerWave, rule.MenPerTownHeld);
        }

        public int? FindMinimumCapitalGarrison(string spawnTemplateId) =>
            Find(spawnTemplateId)?.MinimumCapitalGarrison;

        public int FindMinimumFiefGarrison(string spawnTemplateId) =>
            Find(spawnTemplateId)?.MinimumFiefGarrison ?? 0;

        public int FindLordPartySizeBonus(string spawnTemplateId) =>
            Find(spawnTemplateId)?.LordPartySizeBonus ?? 0;

        // Null when the invasion leaves its wars to the diplomacy configuration.
        public InvasionConquestRules? FindConquestRules(string spawnTemplateId)
        {
            InvasionConquestData? conquest = Find(spawnTemplateId)?.Conquest;

            return conquest == null
                ? null
                : new InvasionConquestRules(conquest.ThreatPerFiefHeld, conquest.OpenFrontIntervalInDays,
                    conquest.TargetSiegePreference, conquest.ReconsiderTargetIntervalInDays, conquest.MaxKingdoms);
        }

        // The invasions launched on behalf of this one, one each time a kingdom falls, in configuration order.
        public IReadOnlyList<SpawnDto> FindFollowers(string spawnTemplateId) =>
            _spawnDao.FindAll().Where(spawn => spawn.Invasion?.FollowsInvasion == spawnTemplateId).ToList();

        // The party template of the invasion this one was launched for; null for one that came on its own.
        public string? FindLeaderTemplateId(string spawnTemplateId) => Find(spawnTemplateId)?.FollowsInvasion;

        // The spawn a line is known by: a copy runs its line's first spawn, a configured follower names the one it follows.
        public string FindLineTemplateId(string spawnTemplateId) =>
            FindLeaderTemplateId(spawnTemplateId) ?? spawnTemplateId;

        public SpawnDto? FindSpawn(string spawnTemplateId) => _spawnDao.FindByPartyTemplateId(spawnTemplateId);

        private InvasionData? Find(string spawnTemplateId) =>
            _spawnDao.FindByPartyTemplateId(spawnTemplateId)?.Invasion;
    }
}
