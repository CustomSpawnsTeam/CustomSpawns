using CustomSpawns.Spawn.PartyComponents;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Invasions.Adapters
{
    public sealed class BannerlordCapitalObjectivePartyEligibility
    {
        public bool IsCombatParty(MobileParty party) =>
            party.IsLordParty || party.PartyComponent is CustomSpawnsPartyComponent;

        // Native CreateArmy needs an active party leader; its strategic selector excludes custom war parties.
        public bool CanCommand(MobileParty party) =>
            IsCombatParty(party) && party.LeaderHero?.IsActive == true &&
            party.LeaderHero.PartyBelongedTo == party && !party.IsCurrentlyAtSea &&
            (party.Army == null || party.Army.LeaderParty == party);
    }
}
