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
     * itself once its protection stops applying. The missing trigger is supplied here; the rule that
     * decides and the actions that end it are vanilla's own.
     */
    public sealed class InvasionKingdomRetirement
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly InvasionState _invasionState;

        public InvasionKingdomRetirement(InvasionState state)
        {
            _invasionState = state;
        }

        // A spent invasion's kingdom, or a landless one whose invasion can no longer retake its capital.
        public void RetireFallen()
        {
            foreach (Invasion invasion in _invasionState.Invasions.ToList())
            {
                Kingdom? kingdom = FindRetirableKingdom(invasion);

                if (kingdom == null)
                {
                    continue;
                }

                Logger.Info("Invasion fallen: invasion=" + invasion.ClanId + " kingdom=" + kingdom.StringId +
                            " spent=" + _invasionState.IsInvasionSpent(invasion));
                DiscontinueKingdom(kingdom);
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
