using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Who is fighting an invasion. A spawned party joins the group its template invades for, keeps
     * that membership across a save, and leaves when it dies; reconciliation is the same question
     * asked of the whole map, since parties, clans and configurations all disappear behind our back.
     */
    internal sealed class InvasionMembership
    {
        private readonly InvasionState _state;
        private readonly SpawnDao _spawns;
        private readonly DynamicSpawnData _parties;
        private readonly InvasionCaptureCorrelation _correlation;

        internal InvasionMembership(InvasionState state, SpawnDao spawns, DynamicSpawnData parties,
            InvasionCaptureCorrelation correlation)
        {
            _state = state;
            _spawns = spawns;
            _parties = parties;
            _correlation = correlation;
        }

        internal void Register(MobileParty party, CsPartyData data)
        {
            // Saved membership survives removed configurations so cleanup remains possible.
            InvasionGroupState? saved = _state.FindGroup(data.InvasionGroupId);
            if (saved != null && saved.HoldsClan(party.ActualClan?.StringId) && saved.SpawnTemplateId == data.OwningSpawnTemplateId)
            {
                saved.Members.Add(party.StringId);
                data.SetInvasionIdentity(saved.Id, data.InvasionRole ?? RoleFor(data));
                return;
            }
            string? templateId = data.OwningSpawnTemplateId;
            Clan? clan = party.ActualClan;
            if (templateId == null || clan == null || clan.IsEliminated || clan.IsBanditFaction ||
                _spawns.FindByPartyTemplateId(templateId)?.Invasion == null)
            {
                data.SetInvasionIdentity(null, null);
                return;
            }
            var group = _state.RegisterMember(templateId, clan.StringId, party.StringId);
            data.SetInvasionIdentity(group.Id, RoleFor(data));
        }

        internal void RegisterLive()
        {
            foreach (MobileParty party in MobileParty.All.Where(p => p.IsActive))
            {
                CsPartyData? data = _parties.GetDynamicSpawnData(party);
                if (data != null) Register(party, data);
            }
        }

        internal void Remove(MobileParty party)
        {
            _state.RemoveMember(party.StringId);
            Reconcile();
        }

        internal void Reconcile()
        {
            _state.Reconcile(MobileParty.All.Where(p => p.IsActive).Select(p => p.StringId),
                id => Settlement.Find(id)?.OwnerClan?.StringId,
                group => GetConfiguration(group) != null && Clan.FindFirst(c => c.StringId == group.ClanId)?.IsEliminated == false,
                group => InvasionKingdomRetirement.FindRetirableKingdom(group) != null);
            _correlation.Prune();
        }

        internal IReadOnlyList<MobileParty> FindLiveMembers(MobileParty party)
        {
            InvasionGroupState? group = _state.FindMemberGroup(party.StringId);
            if (group == null) return new[] { party };
            return MobileParty.All.Where(member => member.IsActive && group.Members.Contains(member.StringId))
                .ToList();
        }

        private static string RoleFor(CsPartyData data) => data.IsIndependentReinforcement ? "Reinforcement" :
            data.IsPrimarySpawn ? "Original" : "Escort";

        private InvasionData? GetConfiguration(InvasionGroupState group) => _spawns.FindByPartyTemplateId(group.SpawnTemplateId)?.Invasion;
    }
}
