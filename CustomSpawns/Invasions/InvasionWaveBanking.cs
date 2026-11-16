namespace CustomSpawns.Invasions
{
    // What one reinforcement rule of an invasion sends with each wave.
    public readonly struct InvasionWave
    {
        public InvasionWave(string partyTemplateId, float intervalInDays, int lordsPerWave, int maxLords,
            int lordsPerTownHeld, int maxMenPerWave = 0, int menPerTownHeld = 0)
        {
            MaxMenPerWave = maxMenPerWave;
            MenPerTownHeld = menPerTownHeld;
            PartyTemplateId = partyTemplateId;
            IntervalInDays = intervalInDays;
            LordsPerWave = lordsPerWave;
            MaxLords = maxLords;
            LordsPerTownHeld = lordsPerTownHeld;
        }

        // The men are rolled off this template.
        public string PartyTemplateId { get; }
        public float IntervalInDays { get; }
        public int LordsPerWave { get; }
        public int MaxLords { get; }
        public int LordsPerTownHeld { get; }

        // The most men one wave brings, the template's mix kept. Zero banks the template's roll whole.
        public int MaxMenPerWave { get; }

        // The men a wave brings over MaxMenPerWave for every town the invasion holds, its capital included.
        public int MenPerTownHeld { get; }

        // A wave grows with the realm the way the bank limit and the lord cap do. Zero still banks a whole roll.
        public int FindMaxMen(int townsHeld) => MaxMenPerWave <= 0 ? 0 : MaxMenPerWave + MenPerTownHeld * townsHeld;

        /**
         * The bank limit is a hard ceiling: a wave brings no more men than the bank has room for, so it never
         * fills the bank past its limit. Zero still banks a whole roll, which only an unlimited bank allows.
         */
        public int FindMaxMen(int townsHeld, int bankHeadroom)
        {
            int maxMen = FindMaxMen(townsHeld);

            if (bankHeadroom == int.MaxValue)
            {
                return maxMen;
            }

            return maxMen <= 0 ? bankHeadroom : System.Math.Min(maxMen, bankHeadroom);
        }
    }

    // What a wave paid into the capital came to.
    public readonly struct InvasionWaveReceipt
    {
        public InvasionWaveReceipt(string capitalId, int men, int lords, int pay, int garrison)
        {
            CapitalId = capitalId;
            Men = men;
            Lords = lords;
            Pay = pay;
            Garrison = garrison;
        }

        public string CapitalId { get; }
        public int Men { get; }
        public int Lords { get; }
        public int Pay { get; }
        public int Garrison { get; }
    }

    // The invasion configuration. Null wherever the configuration, or what it names, is gone.
    public interface IInvasionConfigurations
    {
        InvasionWave? FindWave(string spawnTemplateId, string ruleId);

        int? FindMinimumCapitalGarrison(string spawnTemplateId);
    }

    // Where a wave's men land. Bannerlord access stays behind this boundary.
    public interface IInvasionGarrisonDeposits
    {
        // Rolls a party template's men, at most maxMen of them, into the settlement's garrison and says how
        // many came. Zero maxMen keeps the whole roll.
        int Deposit(string settlementId, string partyTemplateId, int maxMen);
    }

    /**
     * What a reinforcement wave is: men paid into the capital's garrison, lords who come to take them out
     * again, and the gold and food to keep them. Nothing marches in, and the garrison takes the wave only up
     * to its bank limit: a wave drawn into a nearly full bank brings just the men that fill it.
     */
    public sealed class InvasionWaveBanking
    {
        private readonly IInvasionConfigurations _invasionConfigurations;
        private readonly IInvasionGarrisons _invasionGarrisons;
        private readonly IInvasionGarrisonDeposits _invasionGarrisonDeposits;
        private readonly IInvasionLords _invasionLords;
        private readonly InvasionLordLevy _invasionLordLevy;
        private readonly InvasionWaveFunding _invasionWaveFunding;

        public InvasionWaveBanking(IInvasionConfigurations invasionConfigurations,
            IInvasionGarrisons invasionGarrisons, IInvasionGarrisonDeposits invasionGarrisonDeposits,
            IInvasionLords invasionLords, InvasionLordLevy invasionLordLevy, InvasionWaveFunding invasionWaveFunding)
        {
            _invasionConfigurations = invasionConfigurations;
            _invasionGarrisons = invasionGarrisons;
            _invasionGarrisonDeposits = invasionGarrisonDeposits;
            _invasionLords = invasionLords;
            _invasionLordLevy = invasionLordLevy;
            _invasionWaveFunding = invasionWaveFunding;
        }

        // Null when the wave could not be paid in, which keeps it from counting against the rule's allowance.
        public InvasionWaveReceipt? Bank(InvasionReinforcementDraw draw)
        {
            Invasion invasion = draw.Invasion;
            InvasionWave? wave = _invasionConfigurations.FindWave(invasion.SpawnTemplateId, draw.RuleId);
            string? capitalId = invasion.CapitalSettlementId;

            if (wave == null || capitalId == null || !_invasionGarrisons.HasGarrison(capitalId))
            {
                return null;
            }

            // The lords are fielded before the gold is paid, so that their warbands are paid for too.
            int men = _invasionGarrisonDeposits.Deposit(capitalId, wave.Value.PartyTemplateId,
                wave.Value.FindMaxMen(_invasionLords.CountTownsHeld(invasion), draw.BankHeadroom));
            int lords = _invasionLordLevy.Levy(invasion, wave.Value, capitalId);
            int pay = _invasionWaveFunding.Fund(invasion, wave.Value.IntervalInDays);

            return new InvasionWaveReceipt(capitalId, men, lords, pay, _invasionGarrisons.CountMen(capitalId));
        }

        /**
         * The bank limit holds back a wave's men, not its lords: an invasion with a full bank still has the
         * lords the wave brings, up to its lord cap, and they take their men out of the bank that is already
         * there. Without them a full bank is a bank nobody can draw on once the lords it has are captured.
         */
        public int LevyLordsOnly(InvasionReinforcementDraw draw)
        {
            if (draw.Outcome != InvasionReinforcementOutcome.BankFull)
            {
                return 0;
            }

            Invasion invasion = draw.Invasion;
            InvasionWave? wave = _invasionConfigurations.FindWave(invasion.SpawnTemplateId, draw.RuleId);
            string? capitalId = invasion.CapitalSettlementId;

            return wave == null || capitalId == null || !_invasionGarrisons.HasGarrison(capitalId)
                ? 0
                : _invasionLordLevy.Levy(invasion, wave.Value, capitalId);
        }

        // An invasion with a full bank, or one with no waves left, still pays its army until the next slot.
        public int? PayOnly(InvasionReinforcementDraw draw)
        {
            if (draw.Outcome != InvasionReinforcementOutcome.BankFull &&
                draw.Outcome != InvasionReinforcementOutcome.AllWavesSent)
            {
                return null;
            }

            InvasionWave? wave = _invasionConfigurations.FindWave(draw.Invasion.SpawnTemplateId, draw.RuleId);
            return wave == null ? null : _invasionWaveFunding.Fund(draw.Invasion, wave.Value.IntervalInDays);
        }
    }
}
