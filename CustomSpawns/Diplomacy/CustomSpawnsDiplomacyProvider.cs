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
            (attacker, enemy) = ResolveCustomSpawnsKingdoms(attacker, enemy);

            return _customSpawnsClanDiplomacyProvider.IsWarDeclarationPossible(attacker, enemy);
        }

        public bool ShouldBeAtPeace(IFaction faction1, IFaction faction2)
        {
            IFaction friend = _suzerainProvider.GetSuzerain(faction2) ?? faction2;
            (faction1, friend) = ResolveCustomSpawnsKingdoms(faction1, friend);

            return _customSpawnsClanDiplomacyProvider.IsPeaceDeclarationPossible(faction1, friend);
        }

        /// <summary>
        /// A kingdom founded by a custom spawns clan (an invasion taking its capital) has an id no diplomacy
        /// data is keyed by, so it would be judged as a vanilla kingdom and fall into a peace/war loop with
        /// the clan-level reconciliation. Such a kingdom is judged by its ruling clan, and the other side by
        /// its own ruling clan, so the clan-in-a-kingdom rules apply.
        /// </summary>
        private (IFaction, IFaction) ResolveCustomSpawnsKingdoms(IFaction faction1, IFaction faction2)
        {
            if (!IsCustomSpawnsKingdom(faction1) && !IsCustomSpawnsKingdom(faction2))
            {
                return (faction1, faction2);
            }

            return (_suzerainProvider.GetRulingClan(faction1) ?? faction1,
                _suzerainProvider.GetRulingClan(faction2) ?? faction2);
        }

        private bool IsCustomSpawnsKingdom(IFaction faction)
        {
            IFaction? rulingClan = _suzerainProvider.GetRulingClan(faction);
            return rulingClan != null && _customSpawnsClanDiplomacyProvider.HasForcedWarPeaceBehaviour(rulingClan);
        }
    }
}
