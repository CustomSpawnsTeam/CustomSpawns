using System;
using CustomSpawns.Invasions.Adapters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.PlayerSparing.Adapters
{
    // Finds the invasion a besieging party serves and asks whether that invasion spares the settlement.
    public sealed class BannerlordInvasionLandingSparing : IInvasionLandingSparing
    {
        private readonly Func<InvasionState> _state;
        private readonly BannerlordInvasionConfigurations _bannerlordInvasionConfigurations;
        private readonly InvasionPlayerSparing _invasionPlayerSparing;

        public BannerlordInvasionLandingSparing(Func<InvasionState> state,
            BannerlordInvasionConfigurations bannerlordInvasionConfigurations,
            InvasionPlayerSparing invasionPlayerSparing)
        {
            _state = state;
            _bannerlordInvasionConfigurations = bannerlordInvasionConfigurations;
            _invasionPlayerSparing = invasionPlayerSparing;
        }

        public bool MayBesiege(MobileParty party, Settlement settlement)
        {
            Invasion? invasion = _state().FindInvasionOfParty(party.StringId);

            if (invasion == null || invasion.IsSuspended || !invasion.HoldsClan(party.ActualClan?.StringId))
            {
                return true;
            }

            return _invasionPlayerSparing.MayBesiege(
                _bannerlordInvasionConfigurations.FindLineTemplateId(invasion.SpawnTemplateId),
                invasion.CapitalSettlementId != null, settlement.StringId);
        }
    }
}
