using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Invasions.Adapters
{
    public sealed class BannerlordInvasionConfigurations : IInvasionConfigurations
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
                configuration!.MaxLords, configuration.LordsPerTownHeld);
        }

        public int? FindMinimumCapitalGarrison(string spawnTemplateId) =>
            Find(spawnTemplateId)?.MinimumCapitalGarrison;

        private InvasionData? Find(string spawnTemplateId) =>
            _spawnDao.FindByPartyTemplateId(spawnTemplateId)?.Invasion;
    }
}
