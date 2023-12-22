using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Spawn;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace CustomSpawns.Utils
{
    public class SpawnCheats
    {
        private static SpawnDao? _spawnDao;
        private static Spawner? _spawner;
        
        public SpawnCheats(Spawner spawner, SpawnDao spawnDao)
        {
            _spawner = spawner;
            _spawnDao = spawnDao;
        }
        
        [CommandLineFunctionality.CommandLineArgumentFunction("cs_spawn", "campaign")]
        public static string SpawnCustomSpawnParty(List<string> strings)
        {
            if (_spawner == null || _spawnDao == null)
            {
                return "Error: CustomSpawns' dependencies are not initialised.";
            }
            if (!CampaignCheats.CheckCheatUsage(ref CampaignCheats.ErrorType))
            {
                return CampaignCheats.ErrorType;
            }

            string result = "Format is \"campaign.spawn [SpawnPartyTemplateId]\".";
            if (!CampaignCheats.CheckParameters(strings, 1))
            {
                return result;
            } 
            if (CampaignCheats.CheckHelp(strings))
            {
                result += "\n\nAvailable spawns:\n\n";
                result += _spawnDao.FindAllPartyTemplateId().OrderBy(id => id).Aggregate("", (ids, spawnId) => ids + spawnId + "\n");
                return result;
            }

            SpawnDto? spawn = _spawnDao.FindByPartyTemplateId(strings[0]);
            if (spawn == null)
            {
                return strings[0] + " is not a valid spawn.\n\nUse \"campaign.spawn help\" to get the complete list of spawn template ids.";
            }

            Settlement? settlement = CampaignUtils.GetNearestSettlement(Settlement.All.ToList(), new List<IMapPoint>()
            {
                MobileParty.MainParty
            });
            if (settlement == null)
            {
                return "Could not find any settlements to spawn " + strings[0] + ".";
            }
            _spawner.SpawnParty(settlement, spawn.SpawnClan, spawn.PartyTemplate);
            return "Party " + strings[0] + " spawned at " + settlement.Name;
        }
    }
}