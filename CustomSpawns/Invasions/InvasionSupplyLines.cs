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

        internal InvasionSettlementFacts(bool isTown, Func<bool> isCoastalTown)
        {
            IsTown = isTown;
            _isCoastalTown = isCoastalTown;
        }

        internal bool IsTown { get; }
        internal bool IsCoastalTown => _isCoastalTown();
    }

    /**
     * Where an invasion's supplies can come from: which settlement can supply it -- which decides at once
     * which capture winds its reinforcement clocks and which of its settlements keeps being fed -- and
     * whether it depends on holding such a settlement at all, which is what sends it back to retake the
     * one it lost.
     */
    internal interface IInvasionSupplyLine
    {
        bool IsSupplyBase(InvasionSettlementFacts settlement);
        bool DependsOnASupplyBase { get; }
    }

    // Supplies come by sea, so only a town the sea reaches can land them.
    internal sealed class CoastalSupplyLine : IInvasionSupplyLine
    {
        public bool IsSupplyBase(InvasionSettlementFacts settlement) => settlement.IsCoastalTown;
        public bool DependsOnASupplyBase => true;
    }

    // Supplies come overland, so any town can forward them, inland ones included.
    internal sealed class AnySupplyLine : IInvasionSupplyLine
    {
        public bool IsSupplyBase(InvasionSettlementFacts settlement) => settlement.IsTown;
        public bool DependsOnASupplyBase => true;
    }

    /**
     * An invasion whose supplies depend on nothing. Everything it holds supplies it equally, so its
     * configured reinforcements keep coming wherever it happens to be, and no settlement is worth
     * retaking for supply's sake -- there is no base to lose.
     */
    internal sealed class NoSupplyLine : IInvasionSupplyLine
    {
        public bool IsSupplyBase(InvasionSettlementFacts settlement) => true;
        public bool DependsOnASupplyBase => false;
    }

    internal static class InvasionSupplyLines
    {
        internal const string KnownNames = "Coastal, Any, None";

        private static readonly IInvasionSupplyLine CoastalLine = new CoastalSupplyLine();
        private static readonly IInvasionSupplyLine AnyLine = new AnySupplyLine();
        private static readonly IInvasionSupplyLine NoLine = new NoSupplyLine();

        /**
         * The doctrine a configuration asks for, or null for a name no doctrine answers to, which is what
         * the configuration is validated against. An invasion that names none supplies itself by sea, as
         * every invasion did before there was a choice.
         */
        internal static IInvasionSupplyLine? Find(string? name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "Coastal") return CoastalLine;
            if (name == "Any") return AnyLine;
            if (name == "None") return NoLine;
            return null;
        }
    }
}
