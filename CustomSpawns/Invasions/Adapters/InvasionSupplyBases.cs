using CustomSpawns.Data.Dto;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * The one place that answers whether a settlement can supply an invasion. Three questions used to ask
     * it separately -- whether a capture winds the reinforcement clocks, where an arrival may land, and
     * which held settlement keeps being fed -- so a change of supply doctrine had to be made in three
     * places. Here the invasion's configured doctrine answers all three, the kind of settlement that may
     * be a base included, which callers must not test again for themselves.
     */
    internal sealed class InvasionSupplyBases
    {
        private readonly InvasionCoastalTownResolver _invasionCoastalTownResolver = new();

        internal bool IsSupplyBase(Settlement? settlement, InvasionData? configuration)
        {
            IInvasionSupplyLine? supplyLine = InvasionSupplyLines.Find(configuration?.SupplyLine);

            return settlement != null && supplyLine != null && supplyLine.IsSupplyBase(
                new InvasionSettlementFacts(settlement.IsTown,
                    () => _invasionCoastalTownResolver.IsCoastalTown(settlement)));
        }

        // Whether holding a supply base is something this invasion has to fight for at all.
        internal bool DependsOnASupplyBase(InvasionData? configuration) =>
            InvasionSupplyLines.Find(configuration?.SupplyLine)?.DependsOnASupplyBase == true;

        internal void ClearCache() => _invasionCoastalTownResolver.ClearCache();
    }
}
