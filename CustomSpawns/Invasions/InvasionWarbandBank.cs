using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    // A lord's warband as the bank sees it.
    public readonly struct InvasionWarband
    {
        public InvasionWarband(string partyId, string? clanId, bool isLordParty, string? settlementId,
            int freeTroops, int room)
        {
            PartyId = partyId;
            ClanId = clanId;
            IsLordParty = isLordParty;
            SettlementId = settlementId;
            FreeTroops = freeTroops;
            Room = room;
        }

        public string PartyId { get; }
        public string? ClanId { get; }

        // Only a lord's own party banks: the clan's caravans and militias never draw.
        public bool IsLordParty { get; }

        // Where the warband stands, or null while it is on the map.
        public string? SettlementId { get; }

        // The men of the warband who are not heroes.
        public int FreeTroops { get; }

        // How many more men its lord can lead.
        public int Room { get; }
    }

    // What one lord's visit to the bank did.
    public readonly struct InvasionBankVisit
    {
        public InvasionBankVisit(InvasionWarband warband, int returned, int drawn, int garrisonLeft,
            bool keptFreeWarband)
        {
            Warband = warband;
            Returned = returned;
            Drawn = drawn;
            GarrisonLeft = garrisonLeft;
            KeptFreeWarband = keptFreeWarband;
        }

        public InvasionWarband Warband { get; }
        public int Returned { get; }
        public int Drawn { get; }
        public int GarrisonLeft { get; }
        public bool KeptFreeWarband { get; }
    }

    // The warbands of the campaign. Bannerlord access stays behind this boundary.
    public interface IInvasionWarbands
    {
        IReadOnlyList<InvasionWarband> FindIn(string settlementId);

        // Sends the warband's men who are not heroes home and says how many went.
        int ReturnFreeTroops(string partyId);

        // Moves up to that many men from the settlement's garrison to the warband, the newest first, and says
        // how many moved.
        int DrawFromGarrison(string partyId, string settlementId, int men);
    }

    // The garrisons of the campaign. Bannerlord access stays behind this boundary.
    public interface IInvasionGarrisons
    {
        // A settlement with no garrison can be neither paid into nor drawn on. A siege closes neither.
        bool HasGarrison(string settlementId);

        int CountMen(string settlementId);
    }

    /**
     * The capital garrison as the bank of every lord of the invasion, whichever of its clans he belongs to.
     * Vanilla only lets the clan that owns a garrison take men out of it, which left every other clan's lords
     * to wither after their first fight.
     *
     * Vanilla also hands every lord it fields a free warband rolled from his clan's party template, for a
     * lord just beaten and come back exactly as for one who has just arrived with a wave. That free warband
     * is what would make the bank meaningless, so it is given back and the lord draws from the bank instead,
     * unless the bank cannot give him at least as many men as he came with.
     */
    public sealed class InvasionWarbandBank
    {
        private readonly InvasionState _invasionState;
        private readonly IInvasionConfigurations _invasionConfigurations;
        private readonly IInvasionWarbands _invasionWarbands;
        private readonly IInvasionGarrisons _invasionGarrisons;
        private readonly InvasionTroopBank _invasionTroopBank;

        public InvasionWarbandBank(InvasionState invasionState, IInvasionConfigurations invasionConfigurations,
            IInvasionWarbands invasionWarbands, IInvasionGarrisons invasionGarrisons,
            InvasionTroopBank invasionTroopBank)
        {
            _invasionState = invasionState;
            _invasionConfigurations = invasionConfigurations;
            _invasionWarbands = invasionWarbands;
            _invasionGarrisons = invasionGarrisons;
            _invasionTroopBank = invasionTroopBank;
        }

        public InvasionBankVisit? ReplaceFreeWarband(InvasionWarband warband)
        {
            InvasionBankAccount? account = FindAccount(warband);

            if (account == null)
            {
                return null;
            }

            int garrison = _invasionGarrisons.CountMen(account.Value.CapitalId);

            // A lord who came with no free warband has nothing to keep, and a bank this short nothing to give.
            if (!_invasionTroopBank.ReplacesFreeWarband(warband.FreeTroops, garrison, account.Value.Floor))
            {
                return warband.FreeTroops == 0 ? null : new InvasionBankVisit(warband, 0, 0, garrison, true);
            }

            int returned = _invasionWarbands.ReturnFreeTroops(warband.PartyId);
            int drawn = Withdraw(account.Value, warband.PartyId, warband.Room + returned);

            return new InvasionBankVisit(warband, returned, drawn,
                _invasionGarrisons.CountMen(account.Value.CapitalId), false);
        }

        // A lord tops his warband up whenever he stands in the capital.
        public InvasionBankVisit? Refill(InvasionWarband warband)
        {
            InvasionBankAccount? account = FindAccount(warband);

            if (account == null || warband.SettlementId != account.Value.CapitalId)
            {
                return null;
            }

            int drawn = Withdraw(account.Value, warband.PartyId, warband.Room);

            if (drawn == 0)
            {
                return null;
            }

            return new InvasionBankVisit(warband, 0, drawn, _invasionGarrisons.CountMen(account.Value.CapitalId),
                false);
        }

        // The lords already waiting in a capital when a wave is paid into it.
        public IReadOnlyList<InvasionBankVisit> RefillAtCapitals() => _invasionState.Invasions
            .Where(invasion => invasion.CapitalSettlementId != null)
            .SelectMany(invasion => _invasionWarbands.FindIn(invasion.CapitalSettlementId!))
            .Select(Refill)
            .Where(visit => visit != null)
            .Select(visit => visit!.Value)
            .ToList();

        // The lord's invasion's capital, while the invasion holds it and it has a garrison.
        private InvasionBankAccount? FindAccount(InvasionWarband warband)
        {
            Invasion? invasion = _invasionState.FindInvasionHolding(warband.ClanId);

            if (!warband.IsLordParty || invasion == null || invasion.IsSuspended ||
                !_invasionState.HoldsCapital(invasion))
            {
                return null;
            }

            string capitalId = invasion.CapitalSettlementId!;
            int? floor = _invasionConfigurations.FindMinimumCapitalGarrison(invasion.SpawnTemplateId);

            if (floor == null || !_invasionGarrisons.HasGarrison(capitalId))
            {
                return null;
            }

            return new InvasionBankAccount(capitalId, floor.Value);
        }

        private int Withdraw(InvasionBankAccount account, string partyId, int room)
        {
            int men = _invasionTroopBank.Drawable(room, _invasionGarrisons.CountMen(account.CapitalId),
                account.Floor);

            return men > 0 ? _invasionWarbands.DrawFromGarrison(partyId, account.CapitalId, men) : 0;
        }

        private readonly struct InvasionBankAccount
        {
            public InvasionBankAccount(string capitalId, int floor)
            {
                CapitalId = capitalId;
                Floor = floor;
            }

            public string CapitalId { get; }

            // The minimum capital garrison: only the men standing above it are ever spendable.
            public int Floor { get; }
        }
    }
}
