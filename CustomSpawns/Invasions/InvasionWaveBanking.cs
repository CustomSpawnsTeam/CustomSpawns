namespace CustomSpawns.Invasions
{
    // What one reinforcement rule of an invasion sends with each wave.
    public readonly struct InvasionWave
    {
        public InvasionWave(string partyTemplateId, float intervalInDays, int lordsPerWave, int maxLords,
            int lordsPerTownHeld)
        {
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
        // Rolls a party template's men into the settlement's garrison and says how many came.
        int Deposit(string settlementId, string partyTemplateId);
    }

    /**
     * What a reinforcement wave is: men paid into the capital's garrison, lords who come to take them out
     * again, and the gold and food to keep them. Nothing marches in, and the garrison takes the wave whole
     * however large it has grown -- the strength ceiling is what decides whether a wave comes at all.
     */
    public sealed class InvasionWaveBanking
    {
        private readonly IInvasionConfigurations _invasionConfigurations;
        private readonly IInvasionGarrisons _invasionGarrisons;
        private readonly IInvasionGarrisonDeposits _invasionGarrisonDeposits;
        private readonly InvasionLordLevy _invasionLordLevy;
        private readonly InvasionWaveFunding _invasionWaveFunding;

        public InvasionWaveBanking(IInvasionConfigurations invasionConfigurations,
            IInvasionGarrisons invasionGarrisons, IInvasionGarrisonDeposits invasionGarrisonDeposits,
            InvasionLordLevy invasionLordLevy, InvasionWaveFunding invasionWaveFunding)
        {
            _invasionConfigurations = invasionConfigurations;
            _invasionGarrisons = invasionGarrisons;
            _invasionGarrisonDeposits = invasionGarrisonDeposits;
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
            int men = _invasionGarrisonDeposits.Deposit(capitalId, wave.Value.PartyTemplateId);
            int lords = _invasionLordLevy.Levy(invasion, wave.Value, capitalId);
            int pay = _invasionWaveFunding.Fund(invasion, wave.Value.IntervalInDays);

            return new InvasionWaveReceipt(capitalId, men, lords, pay, _invasionGarrisons.CountMen(capitalId));
        }

        // A strong invasion is sent no men, but its army still has to be paid till the next wave.
        public int? PayOnly(InvasionReinforcementDraw draw)
        {
            InvasionWave? wave = _invasionConfigurations.FindWave(draw.Invasion.SpawnTemplateId, draw.RuleId);
            return wave == null ? null : _invasionWaveFunding.Fund(draw.Invasion, wave.Value.IntervalInDays);
        }
    }
}
