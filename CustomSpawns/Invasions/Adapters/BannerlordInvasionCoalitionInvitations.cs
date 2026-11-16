using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CustomSpawns.Invasions.Adapters
{
    // Asks the ruler of the player's kingdom, in a pausing popup, whether to join the coalition against an invader.
    public sealed class BannerlordInvasionCoalitionInvitations : IInvasionCoalitionInvitations
    {
        private readonly BannerlordInvasionCoalitions _bannerlordInvasionCoalitions;
        private readonly BannerlordInvasionRealms _bannerlordInvasionRealms;

        public BannerlordInvasionCoalitionInvitations(BannerlordInvasionCoalitions bannerlordInvasionCoalitions,
            BannerlordInvasionRealms bannerlordInvasionRealms)
        {
            _bannerlordInvasionCoalitions = bannerlordInvasionCoalitions;
            _bannerlordInvasionRealms = bannerlordInvasionRealms;
        }

        // A vassal's kingdom is not the player's to bring in: its ruler answers as any other kingdom's does.
        public string? FindPlayerKingdomId()
        {
            Kingdom? kingdom = Clan.PlayerClan?.Kingdom;

            return kingdom is { IsEliminated: false } && kingdom.RulingClan == Clan.PlayerClan &&
                   _bannerlordInvasionRealms.FindInvasionClanId(kingdom) == null
                ? kingdom.StringId
                : null;
        }

        public void AskToJoin(Action join, Action decline)
        {
            TextObject invader = _bannerlordInvasionCoalitions.FindInvader()?.Name ??
                                 new TextObject("{=cs_coalition_invader}invaders");
            TextObject title = new("{=cs_coalition_title}Calradia Unites");
            TextObject text = new("{=cs_coalition_text}Calradia unites under one banner to defeat the {INVADER} " +
                                  "invasion and oust the {INVADER} from Calradia.\n\nAre you willing to join the " +
                                  "coalition against the {INVADER}?");
            TextObject joinHint = new("{=cs_coalition_join_hint}All factions will be allied until the {INVADER} " +
                                      "are defeated.");
            text.SetTextVariable("INVADER", invader);
            joinHint.SetTextVariable("INVADER", invader);

            InformationManager.ShowInquiry(new InquiryData(title.ToString(), text.ToString(), true, true,
                new TextObject("{=cs_coalition_join}Join").ToString(),
                new TextObject("{=cs_coalition_decline}No").ToString(), join, decline,
                isAffirmativeOptionEnabled: () => (true, joinHint.ToString())), true);
        }
    }
}
