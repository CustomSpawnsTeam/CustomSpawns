using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    public sealed class BannerlordInvasionTreasury : IInvasionTreasury
    {
        public InvasionClanPurse? FindPurse(string clanId)
        {
            Clan? clan = Find(clanId);

            if (clan == null || clan.IsEliminated || clan.Leader == null)
            {
                return null;
            }

            List<InvasionWarbandUpkeep> warbands = clan.WarPartyComponents
                .Select(component => component.MobileParty)
                .Select(party => new InvasionWarbandUpkeep(party.StringId, party.Party.PartySizeLimit,
                    party.ItemRoster.TotalFood))
                .ToList();

            return new InvasionClanPurse(clanId, clan.Leader.Gold, clan.IsMinorFaction,
                clan.Fiefs.Sum(fief => fief.GarrisonParty?.TotalWage ?? 0), warbands);
        }

        public float? FindGarrisonWagePerMan(string? capitalId)
        {
            TroopRoster? garrison = capitalId == null
                ? null
                : Settlement.Find(capitalId)?.Town?.GarrisonParty?.MemberRoster;
            int men = garrison == null ? 0 : garrison.TotalManCount - garrison.TotalHeroes;

            if (men == 0)
            {
                return null;
            }

            int wages = 0;

            for (int index = 0; index < garrison!.Count; index++)
            {
                TroopRosterElement element = garrison.GetElementCopyAtIndex(index);

                if (!element.Character.IsHero)
                {
                    wages += element.Character.TroopWage * element.Number;
                }
            }

            return (float)wages / men;
        }

        public float FindAverageWage() => Campaign.Current.AverageWage;

        public void Give(string clanId, int gold)
        {
            Hero? leader = Find(clanId)?.Leader;

            if (leader != null)
            {
                GiveGoldAction.ApplyBetweenCharacters(null, leader, gold, true);
            }
        }

        private static Clan? Find(string clanId) => Clan.FindFirst(candidate => candidate.StringId == clanId);
    }
}
