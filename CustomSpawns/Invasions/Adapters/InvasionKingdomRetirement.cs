using System.Linq;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Vanilla only asks whether a kingdom can be discontinued when a settlement changes hands or when
     * the player switches kingdom, so the kingdom an invasion was given would outlive the invasion
     * itself once its protection stops applying. The missing trigger is supplied here, and so is the end
     * of a realm whose invasion is still fighting but too beaten to come back, which vanilla is kept from
     * disbanding at all.
     */
    public sealed class InvasionKingdomRetirement
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly InvasionState _invasionState;
        private readonly InvasionRealmCollapse _invasionRealmCollapse;
        private readonly InvasionMercenaryClans _invasionMercenaryClans;

        public InvasionKingdomRetirement(InvasionState state, InvasionRealmCollapse invasionRealmCollapse,
            InvasionMercenaryClans invasionMercenaryClans)
        {
            _invasionState = state;
            _invasionRealmCollapse = invasionRealmCollapse;
            _invasionMercenaryClans = invasionMercenaryClans;
        }

        // A spent invasion's kingdom, or the realm of one too weak for too long to retake a fief.
        public void RetireFallen()
        {
            foreach (Invasion invasion in _invasionState.Invasions.ToList())
            {
                Kingdom? retirable = FindRetirableKingdom(invasion);

                if (retirable != null)
                {
                    Logger.Info("Invasion fallen: invasion=" + invasion.ClanId + " kingdom=" + retirable.StringId +
                                " spent=" + _invasionState.IsInvasionSpent(invasion));
                    RecordFall(invasion);
                    DiscontinueKingdom(retirable);
                    continue;
                }

                Clan? leader = invasion.FindLeadingClan();
                Kingdom? realm = leader?.Kingdom;

                if (leader == null || realm == null || realm.IsEliminated || realm == Clan.PlayerClan.Kingdom ||
                    invasion.IsSuspended || leader.Leader?.IsAlive != true)
                {
                    continue;
                }

                if (_invasionRealmCollapse.Observe(invasion, CampaignTime.Now.ToDays, realm.Fiefs.Count,
                        _invasionState.HoldsCapital(invasion)))
                {
                    Logger.Info("Invasion realm collapsed: invasion=" + invasion.ClanId + " kingdom=" + realm.StringId +
                                " mercenary=" + leader.StringId + " weak_since=" + invasion.WeakSinceDays);
                    Collapse(invasion, realm, leader);
                }
            }
        }

        public static Kingdom? FindRetirableKingdom(Invasion invasion)
        {
            Kingdom? kingdom = invasion.FindLeadingClan()?.Kingdom;

            if (kingdom == null)
            {
                return null;
            }

            bool retirable = !kingdom.IsEliminated && kingdom != Clan.PlayerClan.Kingdom &&
                             !kingdom.Settlements.Any();

            if (retirable)
            {
                CampaignEventDispatcher.Instance.CanKingdomBeDiscontinued(kingdom, ref retirable);
            }

            return retirable ? kingdom : null;
        }

        // FactionDiscontinuationCampaignBehavior.DiscontinueKingdom: its clans leave as independents, and
        // vanilla then gives them its usual time to survive on their own before destroying them.
        private static void DiscontinueKingdom(Kingdom kingdom)
        {
            foreach (Clan clan in kingdom.Clans.ToList())
            {
                FinalizeMapEvents(clan);
                ChangeKingdomAction.ApplyByLeaveByKingdomDestruction(clan);
            }

            kingdom.RulingClan = null;
            DestroyKingdomAction.Apply(kingdom);
        }

        /*
         * The leading clan lives on as a band of mercenaries for hire, and every other clan of the invasion goes
         * with its realm, so a beaten horde does not scatter its lords into the kingdoms that beat it. A clan
         * that merely served the realm leaves it as it would any fallen kingdom.
         */
        private void Collapse(Invasion invasion, Kingdom realm, Clan leader)
        {
            foreach (Clan clan in realm.Clans.ToList())
            {
                FinalizeMapEvents(clan);
            }

            foreach (Clan clan in realm.Clans.Where(clan => clan != leader && invasion.HoldsClan(clan.StringId)).ToList())
            {
                DestroyClanAction.Apply(clan);
            }

            foreach (Clan clan in realm.Clans.ToList())
            {
                ChangeKingdomAction.ApplyByLeaveByKingdomDestruction(clan);
            }

            _invasionMercenaryClans.Hire(leader);
            invasion.IsSuspended = true;
            RecordFall(invasion);
            realm.RulingClan = null;
            DestroyKingdomAction.Apply(realm);
        }

        // Its line counts the realm for good, so no new one takes its place; one beaten at its landing is sent again.
        private void RecordFall(Invasion invasion)
        {
            if (invasion.IsRealmFounded)
            {
                _invasionState.FallenRealmTemplateIds.Add(invasion.SpawnTemplateId);
            }
        }

        // A kingdom is only retired once it holds no settlement, so only its parties can still be fighting.
        private static void FinalizeMapEvents(Clan clan)
        {
            foreach (WarPartyComponent component in clan.WarPartyComponents.ToList())
            {
                if (component?.Party.IsActive != true)
                {
                    continue;
                }

                component.MobileParty.MapEvent?.FinalizeEvent();
                component.MobileParty.SiegeEvent?.FinalizeSiegeEvent();
            }
        }
    }
}
