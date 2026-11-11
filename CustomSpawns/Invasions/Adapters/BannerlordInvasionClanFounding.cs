using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Founds a noble clan in the campaign after the image of an invasion's clan: same culture and tier, a name of
     * its own and a newly made lord at its head, homed in the given settlement. Whether it joins a kingdom, and
     * when the world hears of it, is up to the caller.
     */
    public sealed class BannerlordInvasionClanFounding
    {
        // Null when the model's culture has no lord to put at the clan's head.
        public Clan? Found(string clanId, Clan model, Settlement home, Banner banner, uint color, uint color2)
        {
            CharacterObject? lordTemplate = model.Culture?.LordTemplates
                .FirstOrDefault(character => character.Occupation == Occupation.Lord);

            if (lordTemplate == null)
            {
                return null;
            }

            // Named before it is created: CreateClan registers the clan in Clan.All, and the generator reads
            // every clan's name there, so a clan registered without one throws.
            TextObject name = NameGenerator.Current.GenerateClanName(model.Culture, home);
            Clan clan = Clan.CreateClan(clanId);
            clan.ChangeClanName(name, name);
            clan.Culture = model.Culture;
            clan.Banner = banner;
            clan.Color = color;
            clan.Color2 = color2;
            clan.IsNoble = true;
            clan.SetInitialHomeSettlement(home);
            clan.CalculateMidSettlement();

            // Tier has no public setter; renown is the way in, and it carries the same war party limit.
            clan.AddRenown(Campaign.Current.Models.ClanTierModel.GetRequiredRenownForTier(model.Tier), false);

            Hero leader = HeroCreator.CreateSpecialHero(lordTemplate, home, clan, null, MBRandom.RandomInt(18, 36));
            leader.ChangeState(Hero.CharacterStates.Active);
            clan.SetLeader(leader);
            EnterSettlementAction.ApplyForCharacterOnly(leader, home);
            return clan;
        }
    }
}
