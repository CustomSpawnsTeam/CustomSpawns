using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    // A warband as its upkeep sees it: the men it is meant to lead, and the food it carries.
    public readonly struct InvasionWarbandUpkeep
    {
        public InvasionWarbandUpkeep(string partyId, int sizeLimit, int food)
        {
            PartyId = partyId;
            SizeLimit = sizeLimit;
            Food = food;
        }

        public string PartyId { get; }
        public int SizeLimit { get; }
        public int Food { get; }
    }

    // A clan of the invasion as its war chest sees it.
    public readonly struct InvasionClanPurse
    {
        public InvasionClanPurse(string clanId, int gold, bool isMinorClan, int garrisonWages,
            IReadOnlyList<InvasionWarbandUpkeep> warbands)
        {
            ClanId = clanId;
            Gold = gold;
            IsMinorClan = isMinorClan;
            GarrisonWages = garrisonWages;
            Warbands = warbands;
        }

        public string ClanId { get; }

        // What its leader holds, which is the purse vanilla reads the wage limits from.
        public int Gold { get; }

        // Vanilla reads a minor clan's purse against smaller bands.
        public bool IsMinorClan { get; }

        // What the garrisons of its fiefs cost a day.
        public int GarrisonWages { get; }

        public IReadOnlyList<InvasionWarbandUpkeep> Warbands { get; }
    }

    // The clans' gold. Bannerlord access stays behind this boundary.
    public interface IInvasionTreasury
    {
        // Null for a clan that is gone or has no leader to hold its purse.
        InvasionClanPurse? FindPurse(string clanId);

        // What a man of the capital's garrison earns on average, or null when it holds nobody but heroes.
        float? FindGarrisonWagePerMan(string? capitalId);

        float FindAverageWage();

        void Give(string clanId, int gold);
    }

    // The warbands' food. Bannerlord access stays behind this boundary.
    public interface IInvasionCommissariat
    {
        int FindMenPerFoodPerDay();

        // A lord carrying food for fewer days than this is never called to arms, however many men he leads.
        float FindMinimumFoodDaysForArmy();

        void AddFood(string partyId, int food);
    }

    /**
     * The gold and food a wave brings, shared among every clan of the invasion by what each needs to keep
     * its warbands at full strength until the next wave is due. An invasion has no income of its own, and a
     * warband paying more than its clan's purse allows deserts until it fits.
     */
    public sealed class InvasionWaveFunding
    {
        // The purse bands vanilla's daily clan financial evaluation turns into each war party's wage limit.
        private const int ClanLowerBandGold = 30000;
        private const int ClanUpperBandGold = 90000;
        private const int MinorClanLowerBandGold = 10000;
        private const int MinorClanUpperBandGold = 30000;

        private readonly IInvasionTreasury _invasionTreasury;
        private readonly IInvasionCommissariat _invasionCommissariat;
        private readonly InvasionWarChest _invasionWarChest;

        public InvasionWaveFunding(IInvasionTreasury invasionTreasury, IInvasionCommissariat invasionCommissariat,
            InvasionWarChest invasionWarChest)
        {
            _invasionTreasury = invasionTreasury;
            _invasionCommissariat = invasionCommissariat;
            _invasionWarChest = invasionWarChest;
        }

        // Says how much gold was paid in all.
        public int Fund(Invasion invasion, float intervalInDays)
        {
            // The warbands are filled from the bank, so it is the bank's men whose wages they will pay.
            float wagePerMan = _invasionTreasury.FindGarrisonWagePerMan(invasion.CapitalSettlementId) ??
                               _invasionTreasury.FindAverageWage();
            float foodDays = Math.Max(intervalInDays, _invasionCommissariat.FindMinimumFoodDaysForArmy()) + 1f;
            int menPerFoodPerDay = _invasionCommissariat.FindMenPerFoodPerDay();
            int paid = 0;

            foreach (string clanId in invasion.ClanIds)
            {
                InvasionClanPurse? purse = _invasionTreasury.FindPurse(clanId);

                if (purse == null)
                {
                    continue;
                }

                paid += Pay(purse.Value, wagePerMan, intervalInDays);

                foreach (InvasionWarbandUpkeep warband in purse.Value.Warbands)
                {
                    Provision(warband, menPerFoodPerDay, foodDays);
                }
            }

            return paid;
        }

        /**
         * The purse vanilla wants the clan leader to hold before it lets his largest warband pay for all the
         * men it may lead, and every warband's and garrison's wages until the next wave, less what the clan
         * already has.
         */
        private int Pay(InvasionClanPurse purse, float wagePerMan, float intervalInDays)
        {
            List<int> targetWages = purse.Warbands.Select(warband => TargetWage(warband, wagePerMan)).ToList();
            int targetWageLimit = targetWages.DefaultIfEmpty(0).Max();
            int targetDailyWages = targetWages.Sum() + purse.GarrisonWages;
            int pay = purse.IsMinorClan
                ? _invasionWarChest.Allowance(targetWageLimit, targetDailyWages, intervalInDays, purse.Gold,
                    MinorClanLowerBandGold, MinorClanUpperBandGold)
                : _invasionWarChest.Allowance(targetWageLimit, targetDailyWages, intervalInDays, purse.Gold,
                    ClanLowerBandGold, ClanUpperBandGold);

            if (pay > 0)
            {
                _invasionTreasury.Give(purse.ClanId, pay);
            }

            return pay;
        }

        // What a warband filled to its limit costs a day.
        private static int TargetWage(InvasionWarbandUpkeep warband, float wagePerMan) =>
            (int)Math.Ceiling(warband.SizeLimit * wagePerMan);

        // Food for a full warband until the next wave, and never less than an army asks of it.
        private void Provision(InvasionWarbandUpkeep warband, int menPerFoodPerDay, float days)
        {
            int shortfall = _invasionWarChest.FoodShortfall(warband.SizeLimit, menPerFoodPerDay, days, warband.Food);

            if (shortfall > 0)
            {
                _invasionCommissariat.AddFood(warband.PartyId, shortfall);
            }
        }
    }
}
