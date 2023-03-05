using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Library;

namespace CustomSpawns.Diplomacy
{
    public class DiplomacyCheats
    {
        [CommandLineFunctionality.CommandLineArgumentFunction("cs_clan_join_kingdom_as_mercenary", "campaign")]
        public static string MakeClanJoinAKingdomAsMercenary(List<string> strings)
        {
            if (!CampaignCheats.CheckCheatUsage(ref CampaignCheats.ErrorType))
            {
                return CampaignCheats.ErrorType;
            }

            string result = "Format is \"cs_clan_join_kingdom_as_mercenary [clanId] [kingdomId]\".";
            if (CampaignCheats.CheckHelp(strings))
            {
                result += "\n\nAvailable clans:\n\n";
                result += Clan.All.Select(c => c.StringId).OrderBy(id => id).Aggregate("", (ids, clanId) => ids + clanId + "\n");
                result += "\n\nAvailable kingdoms:\n\n";
                result += Kingdom.All.Select(k => k.StringId).OrderBy(id => id).Aggregate("", (ids, kingdomId) => ids + kingdomId + "\n");
                return result;
            }
            if (!CampaignCheats.CheckParameters(strings, 2))
            {
                return result;
            }

            Clan clan = Clan.All.Find(c => c.StringId.Equals(strings[0]));
            Kingdom kingdom = Kingdom.All.Find(k => k.StringId.Equals(strings[1]));

            if (clan == null)
            {
                return strings[0] + " is not a valid clan id";
            }
            if (kingdom == null)
            {
                return strings[1] + " is not a valid kingdom id";
            }
            ChangeKingdomAction.ApplyByJoinFactionAsMercenary(clan, kingdom);
            return "Clan " + strings[0] + " joined " + strings[1] + "as a mercenary";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("cs_declare_peace", "campaign")]
        public static string PeaceOutFactions(List<string> strings)
        {
            if (!CampaignCheats.CheckCheatUsage(ref CampaignCheats.ErrorType))
            {
                return CampaignCheats.ErrorType;
            }

            string result = "Format is \"cs_declare_peace [Faction1] [Faction2]\".";
            if (CampaignCheats.CheckHelp(strings))
            {
                result += "\n\nAvailable factions:\n\n";
                result += Campaign.Current.Factions.Select(faction => faction.StringId).OrderBy(id => id).Aggregate("", (ids, factionId) => ids + factionId + "\n");
                return result;
            }
            if (!CampaignCheats.CheckParameters(strings, 2))
            {
                return result;
            }

            IFaction leftFaction = Campaign.Current.Factions.ToList().Find(faction => faction.StringId.Equals(strings[0]));
            IFaction rightFaction = Campaign.Current.Factions.ToList().Find(faction => faction.StringId.Equals(strings[1]));

            if (leftFaction == null)
            {
                return strings[0] + " is not a valid faction id";
            }
            if (rightFaction == null)
            {
                return strings[1] + " is not a valid faction id";
            }

            MakePeaceAction.Apply(leftFaction, rightFaction);
            return leftFaction + " peace out with " + rightFaction;
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("cs_declare_war", "campaign")]
        public static string DeclareWarBetweenFactions(List<string> strings)
        {
            if (!CampaignCheats.CheckCheatUsage(ref CampaignCheats.ErrorType))
            {
                return CampaignCheats.ErrorType;
            }

            string result = "Format is \"cs_declare_war [Faction1] [Faction2]\".";
            if (CampaignCheats.CheckHelp(strings))
            {
                result += "\n\nAvailable factions:\n\n";
                result += Campaign.Current.Factions.Select(faction => faction.StringId).OrderBy(id => id).Aggregate("", (ids, factionId) => ids + factionId + "\n");
                return result;
            }
            if (!CampaignCheats.CheckParameters(strings, 2))
            {
                return result;
            }

            IFaction leftFaction = Campaign.Current.Factions.ToList().Find(faction => faction.StringId.Equals(strings[0]));
            IFaction rightFaction = Campaign.Current.Factions.ToList().Find(faction => faction.StringId.Equals(strings[1]));

            if (leftFaction == null)
            {
                return strings[0] + " is not a valid faction id";
            }
            if (rightFaction == null)
            {
                return strings[1] + " is not a valid faction id";
            }

            DeclareWarAction.ApplyByDefault(leftFaction, rightFaction);
            return leftFaction + " declared war against " + rightFaction;
        }
    }
}