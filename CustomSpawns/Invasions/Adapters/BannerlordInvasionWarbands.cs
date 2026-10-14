using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    public sealed class BannerlordInvasionWarbands : IInvasionWarbands
    {
        // Only a lord's party banks, so only a lord's party is worth asking vanilla for its size limit.
        public InvasionWarband Snapshot(MobileParty party)
        {
            TroopRoster roster = party.MemberRoster;
            bool isLordParty = party.PartyComponent is LordPartyComponent;
            int room = isLordParty
                ? (int)Campaign.Current.Models.PartySizeLimitModel.GetPartyMemberSizeLimit(party.Party, false)
                    .ResultNumber - roster.TotalManCount
                : 0;

            return new InvasionWarband(party.StringId, party.ActualClan?.StringId, isLordParty,
                party.CurrentSettlement?.StringId, roster.TotalManCount - roster.TotalHeroes, room);
        }

        public IReadOnlyList<InvasionWarband> FindIn(string settlementId) =>
            Settlement.Find(settlementId)?.Parties.Select(Snapshot).ToList() ?? new List<InvasionWarband>();

        // Heroes stay: a party without its leader has nobody to march it.
        public int ReturnFreeTroops(string partyId)
        {
            TroopRoster? roster = Find(partyId)?.MemberRoster;

            if (roster == null)
            {
                return 0;
            }

            int returned = 0;

            for (int index = roster.Count - 1; index >= 0; index--)
            {
                TroopRosterElement element = roster.GetElementCopyAtIndex(index);

                if (!element.Character.IsHero)
                {
                    roster.RemoveTroop(element.Character, element.Number);
                    returned += element.Number;
                }
            }

            return returned;
        }

        // The newest men first, which are the ones the last wave paid in.
        public int DrawFromGarrison(string partyId, string settlementId, int men)
        {
            TroopRoster? garrison = Settlement.Find(settlementId)?.Town?.GarrisonParty?.MemberRoster;
            MobileParty? party = Find(partyId);

            if (garrison == null || party == null)
            {
                return 0;
            }

            int drawn = 0;

            for (int index = garrison.Count - 1; index >= 0 && drawn < men; index--)
            {
                TroopRosterElement element = garrison.GetElementCopyAtIndex(index);

                if (!element.Character.IsHero)
                {
                    int taken = Math.Min(element.Number, men - drawn);
                    garrison.RemoveTroop(element.Character, taken);
                    party.MemberRoster.AddToCounts(element.Character, taken);
                    drawn += taken;
                }
            }

            return drawn;
        }

        private static MobileParty? Find(string partyId) =>
            Campaign.Current.CampaignObjectManager.Find<MobileParty>(partyId);
    }
}
