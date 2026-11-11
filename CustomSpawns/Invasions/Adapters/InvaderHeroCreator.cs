using System.Linq;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

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
        private readonly IInvasionGarrisonArmyLeaders _invasionGarrisonArmyLeaders;
        private readonly BannerlordInvasionClanFounding _bannerlordInvasionClanFounding = new();

        public InvaderHeroCreator(IInvasionClanRepository invasionClans,
            IInvasionGarrisonArmyLeaders invasionGarrisonArmyLeaders)
        {
            _invasionClans = invasionClans;
            _invasionGarrisonArmyLeaders = invasionGarrisonArmyLeaders;
        }

        // The first clan of the invasion that can still take a war party, or a new one founded to take it.
        public Clan? FindClanWithRoom(Invasion invasion, Settlement home)
        {
            string? clanId = invasion.FindClanWithRoom(id =>
            {
                Clan? clan = Clan.FindFirst(candidate => candidate.StringId == id);

                if (clan == null)
                {
                    return null;
                }

                // A garrison army's lord takes no war party place: lords come only with towns.
                return (clan.IsEliminated, () => (clan.WarPartyComponents.Count(component =>
                    !_invasionGarrisonArmyLeaders.LeadsGarrisonArmy(component.Leader?.StringId)), clan.WarPartyLimit));
            });

            return clanId == null
                ? FoundClan(invasion, home)
                : Clan.FindFirst(candidate => candidate.StringId == clanId);
        }

        /**
         * A lord of this clan, ready to be fielded. Founding a clan has already created a leader who has
         * no party yet; he is the lord, rather than a second hero standing beside him.
         */
        public Hero? CreateLord(Invasion invasion, Clan? clan, Settlement home)
        {
            Hero? leader = clan?.Leader;

            // Native relocation must finish before this lord can lead a party.
            if (invasion.CanReuseLeader(leader?.IsActive == true, leader?.PartyBelongedTo != null))
            {
                return leader;
            }

            return CreateNewLord(clan, home);
        }

        // A lord added to this clan, never one it already has.
        public Hero? CreateNewLord(Clan? clan, Settlement? home)
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
            Clan? leadingClan = invasion.FindLeadingClan();
            Kingdom? kingdom = leadingClan?.Kingdom;
            Clan? clan = leadingClan == null || kingdom == null
                ? null
                : _bannerlordInvasionClanFounding.Found(invasion.ClanId + "_" + invasion.ClanIds.Count, leadingClan,
                    home, Banner.CreateRandomClanBanner(), leadingClan.Color, leadingClan.Color2);

            if (clan == null)
            {
                return null;
            }

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
