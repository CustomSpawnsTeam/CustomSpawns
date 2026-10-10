using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    // What the invasion supplying a town pays for: feeding that many men, plus the town's own allowance.
    internal readonly struct InvasionFoodTerms
    {
        internal InvasionFoodTerms(int minimumGarrison, float townAllowance)
        {
            MinimumGarrison = minimumGarrison;
            TownAllowance = townAllowance;
        }

        internal int MinimumGarrison { get; }
        internal float TownAllowance { get; }
    }

    internal interface IInvasionFoodSupply
    {
        // Null when no invasion supplies the town, whether because none holds it or its supply line is cut.
        InvasionFoodTerms? FindTerms(Town town);
    }
}
