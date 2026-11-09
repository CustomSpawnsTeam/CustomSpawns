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
        private readonly InvasionTroopBank _invasionTroopBank;

        public BannerlordInvasionGarrisons(InvasionTroopBank invasionTroopBank)
        {
            _invasionTroopBank = invasionTroopBank;
        }

        public bool HasGarrison(string settlementId) => Garrison(settlementId) != null;

        public int CountMen(string settlementId) => Garrison(settlementId)?.TotalManCount ?? 0;

        /**
         * One roll per kind of troop in the template, between the numbers it declares. Heroes in a template
         * are skipped: the lords of a wave are fielded as lords, not banked as men. The template is rolled
         * again until it reaches maxMen, and the total is cut down to it, keeping the mix.
         */
        public int Deposit(string settlementId, string partyTemplateId, int maxMen)
        {
            TroopRoster? garrison = Garrison(settlementId);
            PartyTemplateObject? template =
                Campaign.Current.ObjectManager.GetObject<PartyTemplateObject>(partyTemplateId);

            if (garrison == null || template == null)
            {
                return 0;
            }

            PartyTemplateStack[] stacks =
                template.Stacks.Where(stack => stack.Character is { IsHero: false }).ToArray();
            int[] men = _invasionTroopBank.RollWave(
                () => stacks.Select(stack => MBRandom.RandomInt(stack.MinValue, stack.MaxValue + 1)).ToArray(),
                maxMen);

            for (int index = 0; index < stacks.Length; index++)
            {
                garrison.AddToCounts(stacks[index].Character, men[index]);
            }

            return men.Sum();
        }

        private static TroopRoster? Garrison(string settlementId) =>
            Settlement.Find(settlementId)?.Town?.GarrisonParty?.MemberRoster;
    }
}
