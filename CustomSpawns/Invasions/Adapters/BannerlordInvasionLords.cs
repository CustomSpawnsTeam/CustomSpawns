using System;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    public sealed class BannerlordInvasionLords : IInvasionLords
    {
        private readonly InvaderHeroCreator _invaderHeroCreator;
        private readonly IInvasionGarrisonArmyLeaders _invasionGarrisonArmyLeaders;

        public BannerlordInvasionLords(InvaderHeroCreator invaderHeroCreator,
            IInvasionGarrisonArmyLeaders invasionGarrisonArmyLeaders)
        {
            _invaderHeroCreator = invaderHeroCreator;
            _invasionGarrisonArmyLeaders = invasionGarrisonArmyLeaders;
        }

        public int CountTownsHeld(Invasion invasion) => invasion.CountTownsHeld(Clan.All.Select(clan =>
            (clan.StringId,
                (Func<int?>)(() => clan.Kingdom?.Fiefs.Count(fief => fief.IsTown)),
                (Func<int>)(() => clan.Fiefs.Count(fief => fief.IsTown)))));

        // A garrison army's lord is left out: the realm's lords grow only with the towns it takes.
        public int CountLivingLords(Invasion invasion) => invasion.CountLivingLords(Clan.All.Select(clan =>
            (clan.StringId, clan.Heroes.Where(hero => !_invasionGarrisonArmyLeaders.LeadsGarrisonArmy(hero.StringId))
                .Select(hero => (hero.IsAlive, hero.IsChild, hero.IsLord)))));

        // His men come from the garrison draw that vanilla's own party creation triggers.
        public bool Field(Invasion invasion, string capitalId)
        {
            Settlement? capital = Settlement.Find(capitalId);
            Hero? lord = capital == null
                ? null
                : _invaderHeroCreator.CreateLord(invasion,
                    _invaderHeroCreator.FindClanWithRoom(invasion, capital), capital);

            if (lord == null)
            {
                return false;
            }

            MobilePartyHelper.SpawnLordParty(lord, capital);
            return true;
        }
    }
}
