using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Library;

namespace CustomSpawns.Utils
{
    public static class UX
    {
        private static readonly string SpawnPlaceIdentifier = "spawnplace";

        private static readonly string DeathPlaceIdentifier = "deathplace";

        private static readonly Dictionary<string, string> FlagToMessageColour = new ()
        {
            { "danger", "#FF2300FF"},
            {"error", "#FF2300FF" },
            {"relief",  "#65BF22FF" }
        };

        public static void ShowMessage(string message, Color messageColor, string settlementName = "")
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }
            string resolvedMessage = message;
            if (!string.IsNullOrWhiteSpace(settlementName))
            {
                resolvedMessage = ResolveVariables(message, settlementName);
            }
            InformationManager.DisplayMessage(new InformationMessage(resolvedMessage, messageColor));
        }

        public static string GetMessageColour(string flag)
        {
            return FlagToMessageColour.ContainsKey(flag) ? FlagToMessageColour[flag] : "";
        }

        private static string ResolveVariables(string message, string settlementName)
        {
            string[] codes = message.Split(new [] { "[", "]"}, StringSplitOptions.None);
            if (codes.Length == 1) {
                return message;
            }
            StringBuilder spawnMessageBuilder = new();
            foreach(string code in codes)
            {
                string variable = code.ToLower();
                if (variable.Equals(SpawnPlaceIdentifier) || variable.Equals(DeathPlaceIdentifier))
                {
                    spawnMessageBuilder.Append(settlementName);
                }
                else
                {
                    spawnMessageBuilder.Append(code);
                }
            }
            return spawnMessageBuilder.ToString();
        }
    }
}
