using System;
using System.Linq;
using CustomSpawns.Spawn;
using CustomSpawns.Spawn.PartyComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * The Bannerlord edge of the capital-retake use case. Only clans recorded by the invasion repository
     * are eligible, so a native Calradian clan in the same kingdom is deliberately left alone.
     */
    public sealed class InvasionCapitalRetakeOrders : IInvasionCapitalRetakeOrders
    {
        private readonly IInvasionClanRepository _clans;
        private readonly DynamicSpawnData _dynamicSpawnData;

        public InvasionCapitalRetakeOrders(IInvasionClanRepository clans, DynamicSpawnData dynamicSpawnData)
        {
            _clans = clans ?? throw new ArgumentNullException(nameof(clans));
            _dynamicSpawnData = dynamicSpawnData ?? throw new ArgumentNullException(nameof(dynamicSpawnData));
        }

        public void Retake(InvasionCapital capital)
        {
            Settlement? target = capital.CapitalSettlementId == null
                ? null
                : Settlement.Find(capital.CapitalSettlementId);
            if (target == null) return;

            foreach (MobileParty party in MobileParty.All.Where(candidate => candidate.IsActive &&
                         candidate.LordPartyComponent != null &&
                         string.Equals(candidate.ActualClan?.Kingdom?.StringId, capital.InvaderKingdomId,
                             StringComparison.Ordinal)).ToList())
            {
                ConvertIfInvasionParty(party, target);
            }
        }

        private void ConvertIfInvasionParty(MobileParty party, Settlement capital)
        {
            string? spawnTemplateId = party.ActualClan == null
                ? null
                : _clans.FindSpawnTemplate(party.ActualClan.StringId);
            Hero? leader = party.LeaderHero;
            if (spawnTemplateId == null || leader == null || party.PartyComponent is not LordPartyComponent)
            {
                return;
            }

            Settlement home = party.HomeSettlement ?? capital;
            TextObject name = party.Name;
            var component = new CustomSpawnsPartyComponent(leader, name, home);
            party.SetPartyComponent(component);
            component.AssignLeader(leader);

            // OwningSpawnTemplateId is what the resolver uses to select the invasion's configured AI.
            // Re-registering also enrolls this formerly vanilla lord party in the invasion's pooled force.
            _dynamicSpawnData.RegisterAdoptedParty(party, spawnTemplateId);
        }
    }
}
