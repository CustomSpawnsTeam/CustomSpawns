using CustomSpawns.Invasions.Coalitions.Adapters;
using CustomSpawns.Invasions.Conquest;
using CustomSpawns.Invasions.Conquest.Adapters;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace CustomSpawns.Invasions.Alliances.Adapters
{
    /**
     * Lets the lords of a conquering invasion join the sieges the allied realms of its line lay, and the lords of a
     * coalition join the sieges its other kingdoms lay to the line's fiefs. Vanilla keeps a siege to the realm that
     * began it: its lords never weigh an ally's siege, and one that marches there anyway is stopped at the gate
     * without joining the camp.
     */
    public sealed class BannerlordInvasionAlliedSieges
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // How near the gate a lord bound for an ally's siege joins its camp; vanilla stops it closer than this.
        private const float JoinDistance = 1f;
        private readonly BannerlordInvasionRealms _bannerlordInvasionRealms;
        private readonly InvasionConquests _invasionConquests;
        private readonly BannerlordInvasionCoalitionFronts _bannerlordInvasionCoalitionFronts;

        public BannerlordInvasionAlliedSieges(BannerlordInvasionRealms bannerlordInvasionRealms,
            InvasionConquests invasionConquests, BannerlordInvasionCoalitionFronts bannerlordInvasionCoalitionFronts)
        {
            _bannerlordInvasionRealms = bannerlordInvasionRealms;
            _invasionConquests = invasionConquests;
            _bannerlordInvasionCoalitionFronts = bannerlordInvasionCoalitionFronts;
        }

        // The camp besieging a fief, when an allied realm of the lord's line lays it, or another kingdom of the
        // lord's coalition lays it to a fief of the line.
        public BesiegerCamp? FindAlliedCamp(MobileParty party, Settlement settlement)
        {
            BesiegerCamp? camp = settlement.SiegeEvent?.BesiegerCamp;

            return camp != null && camp.MapFaction != party.MapFaction &&
                   (IsCoalitionCamp(party, settlement, camp) ||
                    _invasionConquests.AreAllied(_bannerlordInvasionRealms.FindInvasionClanId(party.MapFaction),
                        _bannerlordInvasionRealms.FindInvasionClanId(camp.MapFaction)))
                ? camp
                : null;
        }

        /**
         * A lord that reached the gate of a fief its ally besieges joins the camp, through vanilla's own arrival,
         * which also puts it into an assault already under way. An army joins with its leader.
         */
        public void JoinAlliedSiege(MobileParty party)
        {
            Settlement? settlement = party.TargetSettlement;

            if (party.DefaultBehavior != AiBehavior.BesiegeSettlement || settlement == null ||
                party.BesiegerCamp != null || party.MapEvent != null || party.CurrentSettlement != null ||
                party.AttachedTo != null || party.IsMainParty ||
                party.ShortTermBehavior == AiBehavior.AssaultSettlement)
            {
                return;
            }

            BesiegerCamp? camp = FindAlliedCamp(party, settlement);

            if (camp == null || party.Position.Distance(settlement.GatePosition) > JoinDistance)
            {
                return;
            }

            EncounterManager.StartSettlementEncounter(party, settlement);
            Logger.Debug("Invasion allied siege joined: party=" + party.StringId + " fief=" + settlement.StringId +
                        " camp=" + camp.LeaderParty?.StringId + " joined=" + (party.BesiegerCamp == camp));
        }

        private bool IsCoalitionCamp(MobileParty party, Settlement settlement, BesiegerCamp camp) =>
            _bannerlordInvasionCoalitionFronts.IsLineRealm(settlement.MapFaction) &&
            _bannerlordInvasionCoalitionFronts.IsMember(party.MapFaction) &&
            _bannerlordInvasionCoalitionFronts.IsMember(camp.MapFaction);
    }
}
