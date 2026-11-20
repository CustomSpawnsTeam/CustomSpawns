namespace CustomSpawns.Invasions.CapitalObjectives
{
    public interface IInvasionAssaultConfigurationProvider
    {
        InvasionAssaultRequirements? Find(string spawnTemplateId);
    }
}
