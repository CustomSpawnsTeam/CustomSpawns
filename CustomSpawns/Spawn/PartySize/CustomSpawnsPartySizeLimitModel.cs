using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace CustomSpawns.Spawn.PartySize
{
    public class CustomSpawnsPartySizeLimitModel : PartySizeLimitModel
    {
        private readonly PartySizeLimitModel _decorated;
        private readonly PartySizeCalculatedSubject _partySizeCalculatedSubject;

        public CustomSpawnsPartySizeLimitModel(PartySizeLimitModel decorated,
            PartySizeCalculatedSubject partySizeCalculatedSubject)
        {
            _decorated = decorated;
            _partySizeCalculatedSubject = partySizeCalculatedSubject;
        }

        public override int MinimumNumberOfVillagersAtVillagerParty => _decorated.MinimumNumberOfVillagersAtVillagerParty;

        public override ExplainedNumber GetPartyMemberSizeLimit(PartyBase party, bool includeDescriptions = false)
        {
            ExplainedNumber partySizeLimit = _decorated.GetPartyMemberSizeLimit(party, true);
            return _partySizeCalculatedSubject.NotifyObservers(party.MobileParty, partySizeLimit);
        }

        public override ExplainedNumber GetPartyPrisonerSizeLimit(PartyBase party, bool includeDescriptions = false)
        {
            return _decorated.GetPartyPrisonerSizeLimit(party, includeDescriptions);
        }

        public override ExplainedNumber CalculateGarrisonPartySizeLimit(Settlement settlement, bool includeDescriptions = false)
        {
            return _decorated.CalculateGarrisonPartySizeLimit(settlement, includeDescriptions);
        }

        public override int GetClanTierPartySizeEffectForHero(Hero hero)
        {
            return _decorated.GetClanTierPartySizeEffectForHero(hero);
        }

        public override int GetNextClanTierPartySizeEffectChangeForHero(Hero hero)
        {
            return _decorated.GetNextClanTierPartySizeEffectChangeForHero(hero);
        }

        public override int GetAssumedPartySizeForLordParty(Hero leaderHero, IFaction partyMapFaction, Clan actualClan)
        {
            return _decorated.GetAssumedPartySizeForLordParty(leaderHero, partyMapFaction, actualClan);
        }

        public override int GetIdealVillagerPartySize(Village village)
        {
            return _decorated.GetIdealVillagerPartySize(village);
        }

        public override TroopRoster FindAppropriateInitialRosterForMobileParty(MobileParty party,
            PartyTemplateObject partyTemplate)
        {
            return _decorated.FindAppropriateInitialRosterForMobileParty(party, partyTemplate);
        }

        public override List<Ship> FindAppropriateInitialShipsForMobileParty(MobileParty party,
            PartyTemplateObject partyTemplate)
        {
            return _decorated.FindAppropriateInitialShipsForMobileParty(party, partyTemplate);
        }
    }
}
