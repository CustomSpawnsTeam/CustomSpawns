using System;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Where an invasion lord's men come from once the invasion holds a capital: that capital's garrison,
     * and nowhere else.
     *
     * Vanilla hands every lord it fields a free warband rolled from his clan's party template, and it does
     * so for a lord who has just been beaten and come back exactly as for one who has just arrived with a
     * wave. That free warband is what would make the bank meaningless, so it is given back and the lord
     * takes out whatever stands above the minimum garrison instead. An empty bank means he rides out alone
     * and has to recruit Calradians like anybody else.
     */
    internal sealed class InvasionLordWarbands
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly InvasionState _invasionState;
        private readonly SpawnDao _spawnDao;

        internal InvasionLordWarbands(InvasionState state, SpawnDao spawns)
        {
            _invasionState = state;
            _spawnDao = spawns;
        }

        internal void DrawFromGarrison(MobileParty party)
        {
            Invasion? invasion = _invasionState.FindInvasionHolding(party.ActualClan?.StringId);

            if (party.PartyComponent is not LordPartyComponent || invasion == null || invasion.IsSuspended ||
                !_invasionState.HoldsCapital(invasion))
            {
                return;
            }

            InvasionData? configuration = _spawnDao.FindByPartyTemplateId(invasion.SpawnTemplateId)?.Invasion;
            Settlement? capital = Settlement.Find(invasion.CapitalSettlementId!);
            TroopRoster? garrison = capital?.Town?.GarrisonParty?.MemberRoster;

            // A besieged capital is cut off both ways: the men in it are holding its walls, so no wave is
            // paid in and no lord takes any out either.
            if (configuration == null || garrison == null || capital?.SiegeEvent != null)
            {
                return;
            }

            int returned = Surrender(party.MemberRoster);
            int drawn = Draw(garrison, party, configuration.MinimumGarrison);
            Logger.Info("Invasion lord drew his warband from the capital: clan=" + party.ActualClan!.StringId +
                        " party=" + party.StringId + " free_troops_returned=" + returned + " men=" + drawn +
                        " garrison=" + garrison.TotalManCount);
        }

        // The free warband goes back. Heroes stay: a party without its leader has nobody to march it.
        private static int Surrender(TroopRoster roster)
        {
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

        /**
         * He fills his party to the size he is able to lead, and takes the garrison's newest men first,
         * which are the ones the last wave paid in. The floor is never broken into.
         */
        private static int Draw(TroopRoster garrison, MobileParty party, int minimumGarrison)
        {
            int wanted = InvasionTroopBank.Drawable(
                (int)Campaign.Current.Models.PartySizeLimitModel.GetPartyMemberSizeLimit(party.Party, false)
                    .ResultNumber - party.MemberRoster.TotalManCount,
                garrison.TotalManCount, minimumGarrison);
            int drawn = 0;

            for (int index = garrison.Count - 1; index >= 0 && drawn < wanted; index--)
            {
                TroopRosterElement element = garrison.GetElementCopyAtIndex(index);

                if (!element.Character.IsHero)
                {
                    int taken = Math.Min(element.Number, wanted - drawn);
                    garrison.RemoveTroop(element.Character, taken);
                    party.MemberRoster.AddToCounts(element.Character, taken);
                    drawn += taken;
                }
            }

            return drawn;
        }
    }
}
