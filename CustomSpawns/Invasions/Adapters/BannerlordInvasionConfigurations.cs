using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
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
                configuration!.MaxLords, configuration.LordsPerTownHeld, rule.MaxMenPerWave, rule.MenPerTownHeld);
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
                    conquest.TargetSiegePreference);
        }

        private InvasionData? Find(string spawnTemplateId) =>
            _spawnDao.FindByPartyTemplateId(spawnTemplateId)?.Invasion;
    }
}
