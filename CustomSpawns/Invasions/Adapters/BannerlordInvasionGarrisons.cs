using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace CustomSpawns.Invasions.Adapters
{
    public sealed class BannerlordInvasionGarrisons : IInvasionGarrisons, IInvasionGarrisonDeposits
    {
        public bool HasGarrison(string settlementId) => Garrison(settlementId) != null;

        public int CountMen(string settlementId) => Garrison(settlementId)?.TotalManCount ?? 0;

        /**
         * One roll per kind of troop in the template, between the numbers it declares. Heroes in a template
         * are skipped: the lords of a wave are fielded as lords, not banked as men.
         */
        public int Deposit(string settlementId, string partyTemplateId)
        {
            TroopRoster? garrison = Garrison(settlementId);
            PartyTemplateObject? template =
                Campaign.Current.ObjectManager.GetObject<PartyTemplateObject>(partyTemplateId);

            if (garrison == null || template == null)
            {
                return 0;
            }

            int banked = 0;

            foreach (PartyTemplateStack stack in template.Stacks.Where(stack => stack.Character is { IsHero: false }))
            {
                int number = MBRandom.RandomInt(stack.MinValue, stack.MaxValue + 1);
                garrison.AddToCounts(stack.Character, number);
                banked += number;
            }

            return banked;
        }

        private static TroopRoster? Garrison(string settlementId) =>
            Settlement.Find(settlementId)?.Town?.GarrisonParty?.MemberRoster;
    }
}
