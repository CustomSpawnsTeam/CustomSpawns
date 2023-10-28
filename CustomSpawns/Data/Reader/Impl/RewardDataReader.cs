using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using CustomSpawns.Data.Model.Reward;
using CustomSpawns.ModIntegration;
using CustomSpawns.Utils;

namespace CustomSpawns.Data.Reader.Impl
{
    public class RewardDataReader : AbstractDataReader<NameSignifierDataReader, PartyRewards>
    {
        private readonly SubModService _subModService;
        private readonly MessageBoxService _messageBoxService;

        public override PartyRewards Data
        {
            get => LoadRewardDataFromAllSubMods();
        }

        public RewardDataReader(SubModService subModService, MessageBoxService messageBoxService)
        {
            _subModService = subModService;
            _messageBoxService = messageBoxService;
        }

        private PartyRewards LoadRewardDataFromAllSubMods()
        {
            string pathToSchema = "";
            pathToSchema = Path.Combine(_subModService.GetCustomSpawnsModule(), "Schema",
                "PartyRewardTemplateSchema.xsd");
            if (!File.Exists(pathToSchema))
            {
                _messageBoxService.ShowMessage("The xsd schema file used for rewards could not be found. " +
                                                   "It is expected to be at " + pathToSchema);
                return new PartyRewards(); // Continue with no loaded rewards
            }
            var rewards = new PartyRewards();
            foreach (var subMod in _subModService.GetAllLoadedSubMods())
            {
                string path = Path.Combine(subMod.CustomSpawnsDirectoryPath, "PartyRewards.xml");
                if (File.Exists(path))
                {
                    List<PartyReward> submodRewards = ParseRewardFile(pathToSchema, path).AllPartyRewards;
                    rewards.AllPartyRewards.AddRange(submodRewards);   
                }
            }
            return rewards;
        }

        private void ValidationEventHandler(object sender, ValidationEventArgs e)
        {
            throw new ArgumentException("The document is not valid", e.Exception);
        }

        private XmlSchema ReadXsdSchema(string path)
        {
            using var streamSchema = new StreamReader(path);
            return XmlSchema.Read(streamSchema, ValidationEventHandler);
        }

        private void ValidateXmlFile(string xmlFilePath, XmlSchema schema)
        {
            var settings = new XmlReaderSettings();
            settings.Schemas.Add(schema);
            settings.ValidationType = ValidationType.Schema;
            try
            {
                using XmlReader streamDocument = XmlReader.Create(xmlFilePath, settings);
                var document = new XmlDocument();
                document.Load(streamDocument);
            }
            catch (IOException e)
            {
                throw new ArgumentException(xmlFilePath + " can not be read due to an error", e);
            }
            catch (XmlException e)
            {
                throw new ArgumentException(e.SourceUri + " is not a valid xml file.", e);
            }
            catch (XmlSchemaValidationException e)
            {
                throw new ArgumentException(e.SourceUri + " is structurally not valid.", e);
            }
        }

        private PartyRewards Deserialise(string filePath)
        {
            XmlSerializer serialiser = new(typeof(PartyRewards));
            using Stream writer = new FileStream(filePath, FileMode.Open);
            return (PartyRewards) serialiser.Deserialize(writer);
        }

        private PartyRewards ParseRewardFile(string pathToSchema, string pathToTemplate)
        {
            try
            {
                XmlSchema schema = ReadXsdSchema(pathToSchema);
                ValidateXmlFile(pathToTemplate, schema);
            }
            catch (ArgumentException e)
            {
                _messageBoxService.ShowCustomSpawnsErrorMessage(e.InnerException ?? e, "reading the reward data files");
                return new PartyRewards();
            }
            return Deserialise(pathToTemplate);
        }
    }
}