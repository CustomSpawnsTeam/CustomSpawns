using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CustomSpawns.Utils
{
    public static class UX
    {
        public static readonly string SpawnPlaceIdentifier = "spawnplace";

        public static readonly string DeathPlaceIdentifier = "deathplace";

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
            return new TextObject(message, new Dictionary<string, object>() { { SpawnPlaceIdentifier, settlementName }, { DeathPlaceIdentifier, settlementName} }).ToString();
        }
    }
}
