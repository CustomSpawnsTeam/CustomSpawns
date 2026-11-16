using System.Linq;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Declares the wars a coalition keeps with the line it stands against. The declaration is made outright, not put
     * to a vote: vanilla rules out a war on a realm whose borders are far away, which the coalition fights anyway.
     */
    public sealed class BannerlordInvasionKingdomWars : IInvasionKingdomWars
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public void DeclareWar(string kingdomId, string enemyKingdomId)
        {
            Kingdom? kingdom = FindKingdom(kingdomId);
            Kingdom? enemy = FindKingdom(enemyKingdomId);

            if (kingdom == null || enemy == null || kingdom == enemy || kingdom.IsAtWarWith(enemy))
            {
                return;
            }

            DeclareWarAction.ApplyByDefault(kingdom, enemy);
            Logger.Info("Invasion coalition war: kingdom=" + kingdom.StringId + " enemy=" + enemy.StringId +
                        " at_war=" + kingdom.IsAtWarWith(enemy) + " day=" + CampaignTime.Now.ToDays);
        }

        private static Kingdom? FindKingdom(string kingdomId) =>
            Kingdom.All.FirstOrDefault(kingdom => kingdom.StringId == kingdomId && !kingdom.IsEliminated);
    }
}
