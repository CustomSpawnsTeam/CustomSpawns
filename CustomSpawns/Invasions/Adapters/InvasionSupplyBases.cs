using CustomSpawns.Data.Dto;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * The one place that answers whether a settlement can supply an invasion -- which is what decides
     * whether a capture winds the reinforcement clocks. The invasion's configured doctrine answers it, the
     * kind of settlement that may be a base included, which callers must not test again for themselves.
     */
    public sealed class InvasionSupplyBases
    {
        private readonly InvasionCoastalTownResolver _invasionCoastalTownResolver = new();

        public bool IsSupplyBase(Settlement? settlement, InvasionData? configuration)
        {
            IInvasionSupplyLine? supplyLine = InvasionSupplyLines.Find(configuration?.SupplyLine);

            return settlement != null && supplyLine != null && supplyLine.IsSupplyBase(
                new InvasionSettlementFacts(settlement.IsTown,
                    () => _invasionCoastalTownResolver.IsCoastalTown(settlement)));
        }

        // Whether holding a supply base is something this invasion has to fight for at all.
        public bool DependsOnASupplyBase(InvasionData? configuration) =>
            InvasionSupplyLines.Find(configuration?.SupplyLine)?.DependsOnASupplyBase == true;

        public void ClearCache() => _invasionCoastalTownResolver.ClearCache();
    }
}
