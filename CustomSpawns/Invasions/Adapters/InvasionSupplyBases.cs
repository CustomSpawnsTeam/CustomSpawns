using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * The one place that answers whether a settlement can supply an invasion. Three questions used to ask
     * it separately -- whether a capture winds the reinforcement clocks, where an arrival may land, and
     * which held settlement keeps being fed -- so a change of supply doctrine had to be made in three
     * places. Coastal supply is the only doctrine today, so this defers to the sea test -- including the
     * kind of settlement that may be one, which callers must not test again for themselves.
     */
    internal sealed class InvasionSupplyBases
    {
        private readonly InvasionCoastalTownResolver _invasionCoastalTownResolver = new();

        internal bool IsSupplyBase(Settlement? settlement) =>
            _invasionCoastalTownResolver.IsCoastalTown(settlement);

        internal void ClearCache() => _invasionCoastalTownResolver.ClearCache();
    }
}
