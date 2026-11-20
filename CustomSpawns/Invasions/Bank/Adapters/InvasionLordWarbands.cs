using NLog;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Invasions.Bank.Adapters
{
    // Takes the campaign's lord parties to the bank and logs what each visit did.
    public sealed class InvasionLordWarbands
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly InvasionWarbandBank _invasionWarbandBank;
        private readonly BannerlordInvasionWarbands _bannerlordInvasionWarbands;

        public InvasionLordWarbands(InvasionWarbandBank invasionWarbandBank,
            BannerlordInvasionWarbands bannerlordInvasionWarbands)
        {
            _invasionWarbandBank = invasionWarbandBank;
            _bannerlordInvasionWarbands = bannerlordInvasionWarbands;
        }

        public void DrawFromGarrison(MobileParty party)
        {
            InvasionBankVisit? visit =
                _invasionWarbandBank.ReplaceFreeWarband(_bannerlordInvasionWarbands.Snapshot(party));

            if (visit == null)
            {
                return;
            }

            if (visit.Value.KeptFreeWarband)
            {
                Logger.Debug("Invasion lord kept his free warband, the capital cannot match it: " +
                            Describe(visit.Value) + " men=" + visit.Value.Warband.FreeTroops);
                return;
            }

            Logger.Debug("Invasion lord drew his warband from the capital: " + Describe(visit.Value) +
                        " free_troops_returned=" + visit.Value.Returned + " men=" + visit.Value.Drawn);
        }

        public void Refill(MobileParty party) =>
            LogRefill(_invasionWarbandBank.Refill(_bannerlordInvasionWarbands.Snapshot(party)));

        public void RefillAtCapitals()
        {
            foreach (InvasionBankVisit visit in _invasionWarbandBank.RefillAtCapitals())
            {
                LogRefill(visit);
            }
        }

        private static void LogRefill(InvasionBankVisit? visit)
        {
            if (visit != null)
            {
                Logger.Debug("Invasion lord refilled his warband from the capital: " + Describe(visit.Value) +
                            " men=" + visit.Value.Drawn);
            }
        }

        private static string Describe(InvasionBankVisit visit) =>
            "clan=" + visit.Warband.ClanId + " party=" + visit.Warband.PartyId + " garrison=" + visit.GarrisonLeft;
    }
}
