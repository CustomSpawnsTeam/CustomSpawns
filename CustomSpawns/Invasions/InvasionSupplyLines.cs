using System;

namespace CustomSpawns.Invasions
{
    /**
     * A settlement as a supply doctrine needs to know it. Whether the sea reaches it is a walk of the
     * campaign map, so it is asked as a question rather than passed as a fact: the doctrines that do not
     * care about the sea never pay for the answer.
     */
    internal readonly struct InvasionSettlementFacts
    {
        private readonly Func<bool> _isCoastalTown;

        internal InvasionSettlementFacts(bool isTown, bool isCastle, Func<bool> isCoastalTown)
        {
            IsTown = isTown;
            IsCastle = isCastle;
            _isCoastalTown = isCoastalTown;
        }

        internal bool IsTown { get; }
        internal bool IsCastle { get; }
        internal bool IsCoastalTown => _isCoastalTown();
    }

    /**
     * Where an invasion's supplies can come from. One question -- can this settlement supply it -- which
     * decides at once which capture winds its reinforcement clocks, where its arrivals may land, and which
     * of its settlements keeps being fed.
     */
    internal interface IInvasionSupplyLine
    {
        bool IsSupplyBase(InvasionSettlementFacts settlement);
    }

    // Supplies come by sea, so only a town the sea reaches can land them.
    internal sealed class CoastalSupplyLine : IInvasionSupplyLine
    {
        public bool IsSupplyBase(InvasionSettlementFacts settlement) => settlement.IsCoastalTown;
    }

    // Supplies come overland, so any fortification can forward them, inland ones included.
    internal sealed class FreeSupplyLine : IInvasionSupplyLine
    {
        public bool IsSupplyBase(InvasionSettlementFacts settlement) => settlement.IsTown || settlement.IsCastle;
    }

    /**
     * An invasion that lives off what it brought. Nothing supplies it, so its reinforcement clocks are
     * never wound -- its configured rules are read and then never come due -- and nothing it holds is fed.
     * It still conquers and still founds its kingdom; none of that is supply.
     */
    internal sealed class NoSupplyLine : IInvasionSupplyLine
    {
        public bool IsSupplyBase(InvasionSettlementFacts settlement) => false;
    }

    internal static class InvasionSupplyLines
    {
        internal const string KnownNames = "Coastal, Free, None";

        private static readonly IInvasionSupplyLine CoastalLine = new CoastalSupplyLine();
        private static readonly IInvasionSupplyLine FreeLine = new FreeSupplyLine();
        private static readonly IInvasionSupplyLine NoLine = new NoSupplyLine();

        /**
         * The doctrine a configuration asks for, or null for a name no doctrine answers to, which is what
         * the configuration is validated against. An invasion that names none supplies itself by sea, as
         * every invasion did before there was a choice.
         */
        internal static IInvasionSupplyLine? Find(string? name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "Coastal") return CoastalLine;
            if (name == "Free") return FreeLine;
            if (name == "None") return NoLine;
            return null;
        }
    }
}
