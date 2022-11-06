using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using CustomSpawns.Data.Model;
using CustomSpawns.ModIntegration;
using CustomSpawns.Utils;
using Path = System.IO.Path;

namespace CustomSpawns.Data.Reader.Impl
{
    public class SpawnDataReader : AbstractDataReader<SpawnDataReader, IList<Model.Spawn>>
    {
        private readonly MessageBoxService _messageBoxService;
        private readonly IList<Model.Spawn> _spawns;

        public SpawnDataReader(SubModService subModService, MessageBoxService messageBoxService)
        {
            _messageBoxService = messageBoxService;
            _spawns = LoadAllSpawnDataFromAllSubMods(subModService);
        }

        public override IList<Model.Spawn> Data => _spawns;

        private IList<Model.Spawn> LoadAllSpawnDataFromAllSubMods(SubModService subModService)
        {
            List<Model.Spawn> spawns = new();
            foreach (var subMod in subModService.GetAllLoadedSubMods())
            {
                string v1FileNaming = Path.Combine(subMod.CustomSpawnsDirectoryPath, "RegularBanditDailySpawn.xml");
                string v2FileNaming = Path.Combine(subMod.CustomSpawnsDirectoryPath, "CustomDailySpawn.xml");
                if (File.Exists(v1FileNaming))
                {
                    spawns.AddRange(ConstructListFromXML(v1FileNaming).AllSpawns);
                } else if (File.Exists(v2FileNaming))
                {
                    spawns.AddRange(ConstructListFromXML(v2FileNaming).AllSpawns);
                }
            }

            EnsureWarnIDQUalities(spawns); // refactor to a data validator object
            return spawns;
        }

        private Spawns ConstructListFromXML(string filePath)
        {
            XmlSerializer serialiser = new XmlSerializer(typeof(Spawns));
            using Stream writer = new FileStream(filePath, FileMode.Open);
            try
            {
                return (Spawns) serialiser.Deserialize(writer);
            }
            catch (ArgumentException e)
            {
                _messageBoxService.ShowCustomSpawnsErrorMessage(e, "the parsing of " + filePath);
                return new Spawns
                {
                    AllSpawns = new()
                };
            }
        }
        
        private void EnsureWarnIDQUalities(IList<Model.Spawn> data)
        {
            List<string> parsedIDs = new();
            List<string> problematicIDs = new();
            foreach(var d in data)
            {
                string id = d.PartyTemplate;
                if (parsedIDs.Contains(id) && !problematicIDs.Contains(id))
                {
                    problematicIDs.Add(id);
                }
                parsedIDs.Add(id);
            }

            if(problematicIDs.Count != 0)
            {
                var msg = "DO NOT WORRY PLAYER, BUT MODDERS BEWARE! \n Duplicate party template IDs have been detected for different spawns. This will not lead to any crashes, but it might lead to behaviour " + 
                          "that you may not have intended, especially regarding spawn numbers. Also, it is bad practice. \n In short, You should have only one party template for one spawn type. The duplicate IDs are: \n";
                foreach(var pr in problematicIDs)
                {
                    msg += pr + "\n";
                }
                _messageBoxService.ShowMessage(msg);
            }
        }
    }
}
