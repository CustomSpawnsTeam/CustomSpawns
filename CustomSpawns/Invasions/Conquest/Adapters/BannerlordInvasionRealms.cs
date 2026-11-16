using System;
using System.Linq;
using CustomSpawns.Invasions.Adapters;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace CustomSpawns.Invasions.Conquest.Adapters
{
    // The realm a conquering invasion rules and the kingdom it conquers, as the campaign holds them.
    public sealed class BannerlordInvasionRealms
    {
        private readonly Func<InvasionState> _state;

        public BannerlordInvasionRealms(Func<InvasionState> state)
        {
            _state = state;
        }

        public IFaction? FindRealm(InvasionConquest conquest) =>
            _state().FindFightingInvasion(conquest.InvasionClanId)?.FindLeadingClan()?.MapFaction;

        public string? FindInvasionClanId(IFaction faction) =>
            _state().FindFightingInvasion((faction as Clan ?? (faction as Kingdom)?.RulingClan)?.StringId)?.ClanId;

        public Kingdom? FindKingdom(string? kingdomId) =>
            Kingdom.All.FirstOrDefault(kingdom => kingdom.StringId == kingdomId);

        public bool HasFallen(string kingdomId) => FindKingdom(kingdomId)?.IsEliminated != false;

        public void DeclareWarOnTarget(Invasion invasion, InvasionConquest conquest)
        {
            IFaction? invader = invasion.FindLeadingClan()?.MapFaction;
            Kingdom? target = FindKingdom(conquest.TargetKingdomId);

            if (invader != null && target != null && !target.IsEliminated && invader != target &&
                !invader.IsAtWarWith(target))
            {
                DeclareWarAction.ApplyByDefault(invader, target);
            }
        }
    }
}
