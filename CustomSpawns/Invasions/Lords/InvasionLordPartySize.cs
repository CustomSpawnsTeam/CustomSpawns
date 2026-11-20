using System;

namespace CustomSpawns.Invasions.Lords
{
    // The invasion configuration's party size bonus. Bannerlord access stays behind this boundary.
    public interface IInvasionLordPartySizes
    {
        // Zero when the invasion leaves vanilla's party size limit alone.
        int FindLordPartySizeBonus(string spawnTemplateId);
    }

    /**
     * How many more men an invasion's lord may lead than vanilla allows him. Vanilla sizes a lord's party
     * by his clan's tier alone, which leaves the invasion's lords with a hundred men each against a bank
     * that holds thousands: the bonus is what lets each of them field a real share of it. More lords still
     * only come with more towns; this changes what each of them leads, not how many there are.
     *
     * Only a lord's own party grows. The clans' caravans keep vanilla's limit, and a clan whose invasion
     * is beaten or suspended goes back to it as well.
     */
    public sealed class InvasionLordPartySize
    {
        private readonly Func<InvasionState> _invasionState;
        private readonly IInvasionLordPartySizes _invasionLordPartySizes;

        public InvasionLordPartySize(Func<InvasionState> invasionState,
            IInvasionLordPartySizes invasionLordPartySizes)
        {
            _invasionState = invasionState;
            _invasionLordPartySizes = invasionLordPartySizes;
        }

        public int FindBonus(string? clanId, bool isLordParty)
        {
            Invasion? invasion = isLordParty ? _invasionState().FindFightingInvasion(clanId) : null;

            return invasion == null ? 0 : _invasionLordPartySizes.FindLordPartySizeBonus(invasion.SpawnTemplateId);
        }
    }
}
