using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    public interface IInvasionResupplyRoutes
    {
        bool CanReach(string partyId, string settlementId);
        bool IsUnderSiege(string settlementId);
    }

    // Only the campaign edge knows whether a lord is free to choose a journey home.
    public readonly struct InvasionResupplyParty
    {
        public InvasionResupplyParty(InvasionWarband warband, float sizeRatio, bool isMainParty = false,
            bool isInArmy = false, bool isInEncounter = false, bool isDisbanding = false,
            bool isMovementLocked = false)
        {
            Warband = warband;
            SizeRatio = sizeRatio;
            IsMainParty = isMainParty;
            IsInArmy = isInArmy;
            IsInEncounter = isInEncounter;
            IsDisbanding = isDisbanding;
            IsMovementLocked = isMovementLocked;
        }

        public InvasionWarband Warband { get; }
        public float SizeRatio { get; }
        public bool IsMainParty { get; }
        public bool IsInArmy { get; }
        public bool IsInEncounter { get; }
        public bool IsDisbanding { get; }
        public bool IsMovementLocked { get; }
    }

    // A depleted lord returns to the bank until ready to fight, but never waits on an empty bank.
    public sealed class InvasionResupply
    {
        private readonly InvasionState _invasionState;
        private readonly IInvasionConfigurations _invasionConfigurations;
        private readonly IInvasionGarrisons _invasionGarrisons;
        private readonly IInvasionResupplyRoutes _invasionResupplyRoutes;
        private readonly HashSet<string> _returningPartyIds = new(StringComparer.Ordinal);

        public InvasionResupply(InvasionState invasionState, IInvasionConfigurations invasionConfigurations,
            IInvasionGarrisons invasionGarrisons, IInvasionResupplyRoutes invasionResupplyRoutes)
        {
            _invasionState = invasionState;
            _invasionConfigurations = invasionConfigurations;
            _invasionGarrisons = invasionGarrisons;
            _invasionResupplyRoutes = invasionResupplyRoutes;
        }

        public string? FindDestination(InvasionResupplyParty party, float minimumArmySizeRatio)
        {
            InvasionWarband warband = party.Warband;
            bool returning = _returningPartyIds.Contains(warband.PartyId);
            float resumeRatio = Math.Max(0.8f, Math.Min(1f, minimumArmySizeRatio + 0.1f));

            if (!warband.IsLordParty || warband.Room <= 0 || party.IsMainParty || party.IsInArmy ||
                party.IsInEncounter || party.IsDisbanding || party.IsMovementLocked ||
                float.IsNaN(party.SizeRatio) || float.IsNaN(minimumArmySizeRatio) ||
                party.SizeRatio >= 1f || party.SizeRatio >= resumeRatio ||
                (!returning && party.SizeRatio > minimumArmySizeRatio))
            {
                Remove(warband.PartyId);
                return null;
            }

            Invasion? invasion = _invasionState.FindFightingInvasion(warband.ClanId);

            if (invasion == null || !_invasionState.HoldsCapital(invasion))
            {
                Remove(warband.PartyId);
                return null;
            }

            string capitalId = invasion.CapitalSettlementId!;
            int? floor = _invasionConfigurations.FindMinimumCapitalGarrison(invasion.SpawnTemplateId);

            if (floor == null || !_invasionGarrisons.HasGarrison(capitalId) ||
                _invasionGarrisons.CountMen(capitalId) <= floor.Value)
            {
                Remove(warband.PartyId);
                return null;
            }

            if (_invasionResupplyRoutes.IsUnderSiege(capitalId) ||
                !_invasionResupplyRoutes.CanReach(warband.PartyId, capitalId))
            {
                Remove(warband.PartyId);
                return null;
            }

            _returningPartyIds.Add(warband.PartyId);
            return capitalId;
        }

        public IReadOnlyCollection<string> FindReturningParties() => _returningPartyIds.ToList();

        public void Restore(IEnumerable<string> partyIds)
        {
            List<string> restored = partyIds.ToList();
            _returningPartyIds.Clear();

            foreach (string partyId in restored)
            {
                _returningPartyIds.Add(partyId);
            }
        }

        public void Remove(string partyId) => _returningPartyIds.Remove(partyId);
    }
}
