using System.Linq;
using CustomSpawns.Invasions.Bank.Adapters;
using CustomSpawns.Invasions.Lords.Adapters;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace CustomSpawns.Invasions.GarrisonArmies.Adapters
{
    /**
     * A garrison army's one-time lord and his party: made the way the invasion fields any of its lords, so vanilla
     * treats him as one of the realm's, and unmade the way a beaten invasion lord is, so nothing of him lingers.
     */
    public sealed class BannerlordInvasionGarrisonArmyLordParties
    {
        private readonly InvaderHeroCreator _invaderHeroCreator;
        private readonly BannerlordInvasionWarbands _bannerlordInvasionWarbands;

        public BannerlordInvasionGarrisonArmyLordParties(InvaderHeroCreator invaderHeroCreator,
            BannerlordInvasionWarbands bannerlordInvasionWarbands)
        {
            _invaderHeroCreator = invaderHeroCreator;
            _bannerlordInvasionWarbands = bannerlordInvasionWarbands;
        }

        // A new lord of the clan that holds the capital, never one it already has.
        public Hero? CreateLord(Settlement capital) => _invaderHeroCreator.CreateNewLord(capital.OwnerClan, capital);

        /**
         * His party at the capital, with that many of the garrison's men. Its creation already draws a lord's
         * warband from the bank, sized to the limit he was recorded with; whatever that left short is drawn here.
         */
        public MobileParty Field(Hero lord, Settlement capital, int men, int food)
        {
            MobileParty party = MobilePartyHelper.SpawnLordParty(lord, capital);
            int missing = men - (party.MemberRoster.TotalManCount - party.MemberRoster.TotalHeroes);

            if (missing > 0)
            {
                _bannerlordInvasionWarbands.DrawFromGarrison(party.StringId, capital.StringId, missing);
            }

            party.ItemRoster.AddToCounts(DefaultItems.Grain, food);
            return party;
        }

        // His men, wounded and all, join the settlement's garrison and their prisoners its dungeon; heroes stay.
        public int Return(MobileParty party, Settlement settlement)
        {
            if (settlement.Town.GarrisonParty == null)
            {
                settlement.AddGarrisonParty();
            }

            int men = Move(party.MemberRoster, settlement.Town.GarrisonParty!.MemberRoster);
            Move(party.PrisonRoster, settlement.Party.PrisonRoster);
            return men;
        }

        /**
         * Removal, as for a beaten invasion lord: no killer, no notification, captivity ended first, so a ransom is
         * never offered. His emptied party goes with him rather than waiting for a lord who will never come.
         */
        public void Remove(Hero lord, MobileParty? party)
        {
            if (lord.IsAlive)
            {
                KillCharacterAction.ApplyByRemove(lord, showNotification: false, isForced: true);
            }

            if (party is { IsActive: true } && party.MapEvent == null)
            {
                DestroyPartyAction.Apply(null, party);
            }
        }

        private static int Move(TroopRoster from, TroopRoster to)
        {
            int moved = 0;

            foreach (TroopRosterElement element in from.GetTroopRoster().Where(element => !element.Character.IsHero)
                         .ToList())
            {
                to.AddToCounts(element.Character, element.Number, false, element.WoundedNumber, element.Xp);
                from.AddToCounts(element.Character, -element.Number, false, -element.WoundedNumber);
                moved += element.Number;
            }

            return moved;
        }
    }
}
