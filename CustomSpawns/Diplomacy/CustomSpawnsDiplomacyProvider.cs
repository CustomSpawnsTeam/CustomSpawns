using TaleWorlds.CampaignSystem;

namespace CustomSpawns.Diplomacy
{
    public class CustomSpawnsDiplomacyProvider
    {
        private readonly CustomSpawnsClanDiplomacyProvider _customSpawnsClanDiplomacyProvider;
        private readonly ISuzerainProvider _suzerainProvider;

        public CustomSpawnsDiplomacyProvider(CustomSpawnsClanDiplomacyProvider customSpawnsClanDiplomacyProvider, ISuzerainProvider suzerainProvider)
        {
            _customSpawnsClanDiplomacyProvider = customSpawnsClanDiplomacyProvider;
            _suzerainProvider = suzerainProvider;
        }

        public bool ShouldBeAtConstantWar(IFaction attacker, IFaction warTarget)
        {
            IFaction enemy = _suzerainProvider.GetSuzerain(warTarget) ?? warTarget;

            return _customSpawnsClanDiplomacyProvider.IsWarDeclarationPossible(attacker, enemy);
        }

        public bool ShouldBeAtPeace(IFaction faction1, IFaction faction2)
        {
            IFaction friend = _suzerainProvider.GetSuzerain(faction2) ?? faction2;

            return _customSpawnsClanDiplomacyProvider.IsWarDeclarationPossible(faction1, friend);
        }
    }
}