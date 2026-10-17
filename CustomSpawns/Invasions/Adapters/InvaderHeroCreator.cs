using System.Linq;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Who an invasion's arrivals belong to. An arriving party and an arriving wave lord both need the same
     * thing -- a clan of the invasion with room for another war party, and a lord to lead it -- so both ask
     * here for it rather than each growing its own answer.
     *
     * Vanilla caps a clan's war parties by tier, so an invasion that outgrows its clans expands into
     * another one instead of into a single oversized clan: its kingdom then distributes fiefs across all
     * of them, and fields a party for each lord that has none, the way it does for any kingdom of its own.
     * A lord beaten in the field is no longer among them: it is removed rather than fielded again.
     */
    public sealed class InvaderHeroCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly IInvasionClanRepository _invasionClans;

        public InvaderHeroCreator(IInvasionClanRepository invasionClans)
        {
            _invasionClans = invasionClans;
        }

        public static bool HasPartySlot(string clanId)
        {
            Clan? clan = Clan.FindFirst(candidate => candidate.StringId == clanId);

            return clan?.IsEliminated == false && clan.WarPartyComponents.Count < clan.WarPartyLimit;
        }

        // The first clan of the invasion that can still take a war party, or a new one founded to take it.
        public Clan? FindClanWithRoom(Invasion invasion, Settlement home)
        {
            string? clanId = invasion.ClanIds.FirstOrDefault(HasPartySlot);

            return clanId == null
                ? FoundClan(invasion, home)
                : Clan.FindFirst(candidate => candidate.StringId == clanId);
        }

        /**
         * A lord of this clan, ready to be fielded. Founding a clan has already created a leader who has
         * no party yet; he is the lord, rather than a second hero standing beside him.
         */
        public Hero? CreateLord(Clan? clan, Settlement home)
        {
            if (clan?.Leader is { IsAlive: true, PartyBelongedTo: null } leader)
            {
                return leader;
            }

            return CreateNewLord(clan, home);
        }

        // A lord added to this clan, never one it already has.
        public static Hero? CreateNewLord(Clan? clan, Settlement? home)
        {
            CharacterObject? lordTemplate = clan?.Culture?.LordTemplates
                .FirstOrDefault(character => character.Occupation == Occupation.Lord);

            if (clan == null || lordTemplate == null)
            {
                return null;
            }

            Hero lord = HeroCreator.CreateSpecialHero(lordTemplate, home, clan, null, MBRandom.RandomInt(18, 36));
            lord.ChangeState(Hero.CharacterStates.Active);
            return lord;
        }

        public Clan? FoundClan(Invasion invasion, Settlement home)
        {
            Clan? founder = Clan.FindFirst(candidate => candidate.StringId == invasion.ClanId);
            Kingdom? kingdom = founder?.Kingdom;
            CharacterObject? lordTemplate = founder?.Culture?.LordTemplates
                .FirstOrDefault(character => character.Occupation == Occupation.Lord);

            if (founder == null || kingdom == null || lordTemplate == null)
            {
                return null;
            }

            // Named before it is created: CreateClan registers the clan in Clan.All, and the generator reads
            // every clan's name there, so a clan registered without one throws.
            TextObject name = NameGenerator.Current.GenerateClanName(founder.Culture, home);
            Clan clan = Clan.CreateClan(founder.StringId + "_" + invasion.ClanIds.Count);
            clan.ChangeClanName(name, name);
            clan.Culture = founder.Culture;
            clan.Banner = Banner.CreateRandomClanBanner();
            clan.Color = founder.Color;
            clan.Color2 = founder.Color2;
            clan.IsNoble = true;
            clan.SetInitialHomeSettlement(home);
            clan.CalculateMidSettlement();

            // Tier has no public setter; renown is the way in, and it carries the same war party limit.
            clan.AddRenown(Campaign.Current.Models.ClanTierModel.GetRequiredRenownForTier(founder.Tier), false);

            Hero leader = HeroCreator.CreateSpecialHero(lordTemplate, home, clan, null, MBRandom.RandomInt(18, 36));
            leader.ChangeState(Hero.CharacterStates.Active);
            clan.SetLeader(leader);
            EnterSettlementAction.ApplyForCharacterOnly(leader, home);
            ChangeKingdomAction.ApplyByJoinToKingdom(clan, kingdom, default, false);
            CampaignEventDispatcher.Instance.OnClanCreated(clan, false);
            invasion.AddClan(clan.StringId);
            _invasionClans.Register(invasion.SpawnTemplateId, clan.StringId);
            Logger.Info("Invasion clan founded: invasion=" + invasion.ClanId + " clan=" + clan.StringId +
                        " kingdom=" + kingdom.StringId);
            return clan;
        }
    }
}
