using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;

namespace CustomSpawns.Invasions.GarrisonArmies.Adapters
{
    public sealed class BannerlordInvasionGarrisonArmyConfigurations
    {
        private readonly SpawnDao _spawnDao;

        public BannerlordInvasionGarrisonArmyConfigurations(SpawnDao spawnDao)
        {
            _spawnDao = spawnDao;
        }

        // Null when the invasion never raises an army out of its capital's garrison.
        public InvasionGarrisonArmyRules? FindRules(string spawnTemplateId)
        {
            InvasionData? configuration = _spawnDao.FindByPartyTemplateId(spawnTemplateId)?.Invasion;
            InvasionGarrisonArmyData? garrisonArmy = configuration?.Conquest?.GarrisonArmy;

            return garrisonArmy == null
                ? null
                : new InvasionGarrisonArmyRules(garrisonArmy.IntervalInDays, garrisonArmy.GarrisonShare,
                    configuration!.MinimumCapitalGarrison);
        }
    }
}
