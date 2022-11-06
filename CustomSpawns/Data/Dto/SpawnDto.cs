using System.Collections.Generic;
using System.Collections.ObjectModel;
using CustomSpawns.Data.Adapter;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CustomSpawns.Data.Dto
{
    public class SpawnDto
    {
        public Track.PartyTypeEnum PartyType { get; set; }
        public Clan SpawnClan { get; set; }
        public List<SpawnSettlementType> TrySpawnAtList { get; set; } = new();
        public List<Clan> OverridenSpawnClan { get; set; } = new();
        public List<Kingdom> OverridenSpawnKingdoms { get; set; } = new();
        public List<Settlement> OverridenSpawnSettlements { get; set; } = new();
        public List<CultureCode> OverridenSpawnCultures { get; set; } = new();
        public ReadOnlyCollection<AccompanyingParty> SpawnAlongWith { get; set; }
        public int MaximumOnMap { get; set; }
        public int MinimumNumberOfDaysUntilSpawn { get; set; }
        public bool AttackClosestIfIdleForADay { get; set; }
        public float DynamicSpawnChancePeriod { get; set; }
        public float DynamicSpawnChanceEffect { get; set; }
        public float MinimumDevestationToSpawn { get; set; }
        public float DevestationLinearMultiplier { get; set; }
        public AI.PatrolAroundClosestLestInterruptedAndSwitchBehaviour.PatrolAroundClosestLestInterruptedAndSwitchBehaviourData PatrolAroundClosestLestInterruptedAndSwitch { get; set; }
        public float ChanceOfSpawn { get; set; }
        public float ChanceInverseConstant { get; set; }
        public PartyTemplateObject PartyTemplate { get; set; }
        public PartyTemplateObject PartyTemplatePrisoner { get; set; }
        public string Name { get; set; }
        public int RepeatSpawnRolls { get; set; }
        public float BaseSpeedOverride { get; set; }
        public InformationMessage? SpawnMessage { get; set; }
        public InquiryData inquiryMessage { get; set; }
        public bool inquiryPause { get; set; }
        public InformationMessage? DeathMessage { get; set; }
        public int SoundEvent { get; set; }
        public bool PatrolAroundSpawn { get; set; }
        public bool InheritClanFromSettlement { get; set; }
    }

}