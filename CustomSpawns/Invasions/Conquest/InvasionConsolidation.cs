using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions.Conquest
{
    public readonly struct InvasionFiefGarrison
    {
        public InvasionFiefGarrison(string settlementId, int men, bool isCapital, bool isBesieged)
        {
            SettlementId = settlementId;
            Men = men;
            IsCapital = isCapital;
            IsBesieged = isBesieged;
        }

        public string SettlementId { get; }
        public int Men { get; }
        public bool IsCapital { get; }
        public bool IsBesieged { get; }
    }

    public readonly struct InvasionGarrisonTransfer
    {
        public InvasionGarrisonTransfer(string settlementId, int men)
        {
            SettlementId = settlementId;
            Men = men;
        }

        public string SettlementId { get; }
        public int Men { get; }
    }

    /**
     * An invasion holds what it takes before it reaches for more. Every fief it holds besides the capital
     * is garrisoned out of the capital's bank up to a minimum, and while any of them stands below that
     * minimum its lords take no new fief: an invasion that spreads its garrisons thinner than that loses
     * its conquests one after another to whoever comes for them.
     */
    public sealed class InvasionConsolidation
    {
        public bool AllowsExpansion(IEnumerable<InvasionFiefGarrison> fiefs, int minimumFiefGarrison) =>
            minimumFiefGarrison <= 0 || fiefs.All(fief => fief.IsCapital || fief.Men >= minimumFiefGarrison);

        /**
         * A siege an invasion that cannot expand holds back. Retaking its capital is never held, nor is a siege of
         * the kingdom it conquers: once the invasion is as strong as it may grow its waves stop, its capital's bank
         * runs dry and its garrisons may never fill again, so holding those sieges would stall the conquest for good.
         */
        public bool HoldsSiege(bool allowsExpansion, bool isCapital, bool isConquestTarget) =>
            !allowsExpansion && !isCapital && !isConquestTarget;

        // The thinnest fief is served first, out of what stands above the capital's own floor. A besieged
        // fief cannot be reached and waits for the siege to end.
        public IReadOnlyList<InvasionGarrisonTransfer> PlanTopUps(int capitalMen, int minimumCapitalGarrison,
            IEnumerable<InvasionFiefGarrison> fiefs, int minimumFiefGarrison)
        {
            var transfers = new List<InvasionGarrisonTransfer>();
            int spendable = capitalMen - minimumCapitalGarrison;

            if (minimumFiefGarrison <= 0)
            {
                return transfers;
            }

            foreach (InvasionFiefGarrison fief in fiefs
                         .Where(fief => !fief.IsCapital && !fief.IsBesieged && fief.Men < minimumFiefGarrison)
                         .OrderBy(fief => fief.Men))
            {
                int men = Math.Min(minimumFiefGarrison - fief.Men, spendable);

                if (men <= 0)
                {
                    break;
                }

                transfers.Add(new InvasionGarrisonTransfer(fief.SettlementId, men));
                spendable -= men;
            }

            return transfers;
        }
    }
}
