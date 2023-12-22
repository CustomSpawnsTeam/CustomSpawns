using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using CustomSpawns.Data.Dto;
using CustomSpawns.Data.Model;
using CustomSpawns.Data.Reader.Impl;
using CustomSpawns.Exception;
using CustomSpawns.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace CustomSpawns.Data.Adapter
{
    public class SpawnDtoAdapter
    {
        private readonly NameSignifierDataReader _nameSignifierDataReader;
        private readonly MBObjectManager _objectManager;
        private readonly CampaignObjectManager _campaignObjectManager;

        public SpawnDtoAdapter(NameSignifierDataReader nameSignifierDataReader, MBObjectManager objectManager,
            CampaignObjectManager campaignObjectManager)
        {
            _nameSignifierDataReader = nameSignifierDataReader;
            _objectManager = objectManager;
            _campaignObjectManager = campaignObjectManager;
        }
        
        public SpawnDto Adapt(Model.Spawn spawn)
        {
            SpawnDto dat = new ();

            dat.PartyTemplate = FindPartyTemplate(spawn.PartyTemplate);
            if (spawn.PrisonerPartyTemplate != null)
            {
                dat.PartyTemplatePrisoner = FindPartyTemplate(spawn.PrisonerPartyTemplate);   
            }

            if (spawn.Faction == null && spawn.BanditFaction != null)
                dat.SpawnClan = FindClan(spawn.BanditFaction);
            else if (spawn.Faction != null && spawn.BanditFaction == null)
                dat.SpawnClan = FindClan(spawn.Faction);
            else if (spawn.Faction != null && spawn.BanditFaction != null)
                throw new ArgumentException("Bandit and Spawn Clan are mutual exclusive. Only one may be used");
            else
                throw new ArgumentException("Bandit Clan nor Spawn Clan could be found. The faction is mandatory");

            dat.OverridenSpawnClan = spawn.OverridenClanSpawns
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(FindClan)
                .ToList();
            
            dat.OverridenSpawnKingdoms = spawn.OverridenKingdomSpawns
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(FindKingdom)
                .ToList();

            dat.OverridenSpawnCultures = spawn.OverridenCultureSpawns
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => FindCulture(id).GetCultureCode())
                .ToList();

            dat.OverridenSpawnSettlements = spawn.OverridenSettlementSpawns
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(FindSettlement)
                .ToList();

            dat.MaximumOnMap = spawn.MaximumOnMap;
            dat.InheritClanFromSettlement = spawn.InheritClanFromSettlement;
            dat.PartyType = spawn.PartyType == null ? Track.PartyTypeEnum.Bandit : StringToPartyTypeEnumIfInvalidBandit(spawn.PartyType);
            dat.ChanceOfSpawn = spawn.ChanceOfSpawn;
            dat.Name = spawn.Name;
            dat.ChanceInverseConstant = spawn.ChanceInverseConstant;
            dat.RepeatSpawnRolls = spawn.RepeatSpawnRolls;
            dat.PatrolAroundSpawn = spawn.IsConfiguredToPatrolAroundSpawn;
            dat.MinimumNumberOfDaysUntilSpawn = spawn.MinimumNumberOfDaysUntilSpawn;
            dat.AttackClosestIfIdleForADay = spawn.AttackClosestIfIdleForADay;
            dat.DynamicSpawnChancePeriod = spawn.DynamicSpawnChancePeriod;
            // TODO create an xsd schema to force the DynamicSpawnChanceEffect value between 0 and 1
            dat.DynamicSpawnChanceEffect = spawn.DynamicSpawnChanceEffect;
            if (!string.IsNullOrWhiteSpace(spawn.TrySpawnAt))
            {
                dat.TrySpawnAtList = ConstructTrySettlementList(spawn.TrySpawnAt);
            }

            if (!string.IsNullOrWhiteSpace(spawn.SpawnMessage))
            {
                Color spawnColour = ResolveColour(spawn.SpawnMessageColor);
                dat.SpawnMessage = new InformationMessage(spawn.SpawnMessage, spawnColour);   
            }

            if (!string.IsNullOrWhiteSpace(spawn.DeathMessage))
            {
                Color deathColour = ResolveColour(spawn.DeathMessageColor);
                dat.DeathMessage = new InformationMessage(spawn.DeathMessage, deathColour);   
            }

            dat.SoundEvent = SoundEvent.GetEventIdFromString(spawn.SpawnSound);

            //inquiry message (message box with options)
            string inqTitle = spawn.SpawnMessageBoxTitle;
            string inqText = spawn.SpawnMessageBoxText;
            string inqAffirmativeText = spawn.SpawnMessageBoxButton;

            if (!string.IsNullOrWhiteSpace(inqText))
            {
                dat.inquiryMessage = new InquiryData(inqTitle, inqText, true, false, inqAffirmativeText, "", null, null);
                dat.inquiryPause = spawn.SpawnMessageBoxPause;
            }

            dat.MinimumDevestationToSpawn = spawn.MinimumDevestationToSpawn;
            dat.DevestationLinearMultiplier = spawn.DevestationLinearMultiplier;

            //patrol around closest lest interrupted and switch
            PatrolAroundConfig? patrolAroundConfig = spawn.PatrolAroundConfig;
            if (patrolAroundConfig != null && patrolAroundConfig.IsEnabled)
            {
                List<SpawnSettlementType> patrolLocationTypes = new();
                if (patrolAroundConfig.PatrolLocationType != null)
                {
                    patrolLocationTypes = ConstructTrySettlementList(patrolAroundConfig.PatrolLocationType);   
                }
                dat.PatrolAroundClosestLestInterruptedAndSwitch = new (null, patrolAroundConfig.MinStableDays, patrolAroundConfig.MinStableDays, patrolLocationTypes);
            }

            //Spawn along with
            List<AccompanyingParty> supportingParties = new List<AccompanyingParty>(spawn.SupportingPartyTemplates.Count); 
            foreach (string partyTemplateId in spawn.SupportingPartyTemplates)
            {
                PartyTemplateObject pt = FindPartyTemplate(partyTemplateId);
                supportingParties.Add(new AccompanyingParty(pt, _nameSignifierDataReader.Data[pt.StringId].IdToName,
                    _nameSignifierDataReader.Data[pt.StringId].IdToFollowMainParty));
            }
            dat.SpawnAlongWith = new ReadOnlyCollection<AccompanyingParty>(supportingParties);

            return dat;
        }

        private string ParseId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id must not be null nor empty");
            }
            String pattern = "^[a-zA-Z0-9]+.(?<id>\\w+)$";
            Regex regex = new (@pattern);
            Match match = regex.Match(id);
            if(!match.Success)
                throw new ArgumentException("Invalid value \"" + id + "\". Expected value is ObjectType.Id");
            return match.Groups["id"].Value;
        }

        private T FindObject<T>(string objectId) where T : MBObjectBase
        {
            string parsedId = ParseId(objectId);
            T? obj = _objectManager.GetObject<T>(parsedId) ?? _campaignObjectManager.Find<T>(parsedId);
            if (obj == null)
            {
                // dirty impl for now to detect if a sub-mod is loaded into an existing save 
                throw new TechnicalException("Can not find object with Id " + objectId);
            }
            return obj;
        }

        private Clan FindClan(string factionId)
        {
            Clan? clan = FindObject<Clan>(factionId);
            if (clan == null)
                throw new ArgumentException("Clan " + factionId + " is not defined. You have to add this clan via xml or use an existing clan.");
            return clan;
        }

        private Kingdom FindKingdom(string factionId)
        {
            Kingdom? kingdom = FindObject<Kingdom>(factionId);
            if (kingdom == null)
            {
                throw new ArgumentException("Kingdom " + factionId + " is not defined. You have to add this kingdom via xml or use an existing kingdom.");
            }
            return kingdom;
        }

        private Settlement FindSettlement(string settlementId)
        {
            Settlement? settlement = FindObject<Settlement>(settlementId);
            if(settlement == null)
                throw new ArgumentException("Settlement " + settlementId + " is not defined. You have to add this settlement via xml or use an existing settlement.");
            return settlement;
        }

        private CultureObject FindCulture(string cultureId)
        {
            CultureObject? culture = FindObject<CultureObject>(cultureId);
            if (culture == null)
                throw new ArgumentException("Culture " + cultureId + " is not defined. You have to add this culture via xml or use an existing culture.");
            return culture;
        }

        private PartyTemplateObject FindPartyTemplate(string partyTemplateId)
        {
            PartyTemplateObject? partyTemplate = FindObject<PartyTemplateObject>(partyTemplateId);
            if(partyTemplate == null)
                throw new ArgumentException("PartyTemplate " + partyTemplateId + " is not defined. You have to add this party template via xml or use an existing template.");
            return partyTemplate;
        }

        private Color ResolveColour(string? colour)
        {
            if(string.IsNullOrWhiteSpace(colour))
            {
                return Color.Black;
            }

            string colourCode = UX.GetMessageColour(colour!);
            if(string.IsNullOrWhiteSpace(colourCode))
            {
                if (colour[0] == '#')
                {
                    return Color.ConvertStringToColor(colour);
                }
                return Color.Black;
            }
            return Color.ConvertStringToColor(colourCode);
        }
        
        private static List<SpawnSettlementType> ConstructTrySettlementList(string input)
        {
            string[] trySpawnAtArray = input.Split('|');
            List<SpawnSettlementType> returned = new ();
            foreach (var place in trySpawnAtArray)
            {
                switch (place)
                {
                    case "Village":
                        returned.Add(SpawnSettlementType.Village);
                        break;
                    case "Town":
                        returned.Add(SpawnSettlementType.Town);
                        break;
                    case "Castle":
                        returned.Add(SpawnSettlementType.Castle);
                        break;
                }
            }
            return returned;
        }
        
        private Track.PartyTypeEnum StringToPartyTypeEnumIfInvalidBandit(string s)
        {
            switch (s)
            {
                case "Default":
                    return Track.PartyTypeEnum.Default;
                case "Bandit":
                    return Track.PartyTypeEnum.Bandit;
                case "Caravan":
                    return Track.PartyTypeEnum.Caravan;
                case "GarrisonParty":
                    return Track.PartyTypeEnum.GarrisonParty;
                case "Lord":
                    return Track.PartyTypeEnum.Lord;
                case "Villager":
                    return Track.PartyTypeEnum.Villager;
                default:
                    return Track.PartyTypeEnum.Bandit;
            }
        }
    }

    public struct AccompanyingParty
    {
        public PartyTemplateObject templateObject;
        public string name;
        public bool accompanyMainParty; //TODO implement this!!
    
        public AccompanyingParty(PartyTemplateObject pt, string n, bool accompanyMainParty)
        {
            templateObject = pt;
            name = n;
            this.accompanyMainParty = accompanyMainParty; 
        }
    }
    
    public enum SpawnSettlementType
    {
        Village, Castle, Town
    }
}