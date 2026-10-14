using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    public sealed class BannerlordInvasionLords : IInvasionLords
    {
        private readonly InvaderHeroCreator _invaderHeroCreator;

        public BannerlordInvasionLords(InvaderHeroCreator invaderHeroCreator)
        {
            _invaderHeroCreator = invaderHeroCreator;
        }

        // The kingdom's towns when the invasion has one, its own clans' towns until then.
        public int CountTownsHeld(Invasion invasion)
        {
            List<Clan> clans = Clans(invasion);
            Kingdom? kingdom = clans.Select(clan => clan.Kingdom).FirstOrDefault(candidate => candidate != null);

            return kingdom == null
                ? clans.Sum(clan => clan.Fiefs.Count(fief => fief.IsTown))
                : kingdom.Fiefs.Count(fief => fief.IsTown);
        }

        public int CountLivingLords(Invasion invasion) => Clans(invasion)
            .Sum(clan => clan.Heroes.Count(hero => hero.IsAlive && !hero.IsChild && hero.IsLord));

        // His men come from the garrison draw that vanilla's own party creation triggers.
        public bool Field(Invasion invasion, string capitalId)
        {
            Settlement? capital = Settlement.Find(capitalId);
            Hero? lord = capital == null
                ? null
                : _invaderHeroCreator.CreateLord(_invaderHeroCreator.FindClanWithRoom(invasion, capital), capital);

            if (lord == null)
            {
                return false;
            }

            MobilePartyHelper.SpawnLordParty(lord, capital);
            return true;
        }

        private static List<Clan> Clans(Invasion invasion) =>
            Clan.All.Where(clan => invasion.HoldsClan(clan.StringId)).ToList();
    }
}
