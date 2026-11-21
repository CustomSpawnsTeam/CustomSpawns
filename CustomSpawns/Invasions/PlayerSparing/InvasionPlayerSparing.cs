using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Invasions.Conquest;
using CustomSpawns.Invasions.Lines;

namespace CustomSpawns.Invasions.PlayerSparing
{
    /**
     * An invasion opens its war on the world, not on the player. Until its line has beaten a kingdom it lands on
     * no settlement the player holds and takes no kingdom the player serves or rules as its target. Once a kingdom
     * has fallen to the line, the player is fair game like everybody else.
     */
    public sealed class InvasionPlayerSparing
    {
        private readonly IInvasionPlayerRealm _invasionPlayerRealm;
        private readonly InvasionBeatenKingdoms _invasionBeatenKingdoms;

        public InvasionPlayerSparing(IInvasionPlayerRealm invasionPlayerRealm,
            InvasionBeatenKingdoms invasionBeatenKingdoms)
        {
            _invasionPlayerRealm = invasionPlayerRealm;
            _invasionBeatenKingdoms = invasionBeatenKingdoms;
        }

        public bool SparesPlayer(string lineTemplateId) => !_invasionBeatenKingdoms.HasBeatenAny(lineTemplateId);

        /**
         * Whether an invasion may besiege the settlement on its way to its first capital. A settlement the player
         * holds is ruled out, so the nearest one the player does not hold is landed on instead. Once the capital
         * is founded, taking it back or any other siege is no longer the first landing.
         */
        public bool MayBesiege(string lineTemplateId, bool hasFoundedCapital, string settlementId) =>
            hasFoundedCapital || !SparesPlayer(lineTemplateId) || !IsHeldByPlayer(settlementId);

        // The kingdom a first fief taken from it makes the invasion's target; never the player's.
        public string? FindFirstTarget(string lineTemplateId, string? kingdomId) =>
            kingdomId == null || (SparesPlayer(lineTemplateId) && IsPlayerKingdom(kingdomId)) ? null : kingdomId;

        // The kingdoms an invasion may turn on, leaving out the player's until its line has beaten a kingdom.
        public IReadOnlyList<InvasionNeighbour> FindTargetCandidates(string lineTemplateId,
            IReadOnlyList<InvasionNeighbour> neighbours) =>
            SparesPlayer(lineTemplateId)
                ? neighbours.Where(neighbour => !IsPlayerKingdom(neighbour.KingdomId)).ToList()
                : neighbours;

        // Held by the player's own clan, or by any clan of the kingdom the player rules or serves.
        private bool IsHeldByPlayer(string settlementId)
        {
            string? playerClanId = _invasionPlayerRealm.FindPlayerClanId();
            string? playerKingdomId = _invasionPlayerRealm.FindPlayerKingdomId();

            return (playerClanId != null && _invasionPlayerRealm.FindOwnerClanId(settlementId) == playerClanId) ||
                   (playerKingdomId != null &&
                    _invasionPlayerRealm.FindOwnerKingdomId(settlementId) == playerKingdomId);
        }

        private bool IsPlayerKingdom(string kingdomId)
        {
            string? playerKingdomId = _invasionPlayerRealm.FindPlayerKingdomId();

            return playerKingdomId != null && playerKingdomId == kingdomId;
        }
    }
}
