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
        public List<CultureObject> OverridenSpawnCultures { get; set; } = new();
        public ReadOnlyCollection<AccompanyingParty> SpawnAlongWith { get; set; }
        public int MaximumOnMap { get; set; }
        public int MinimumNumberOfDaysUntilSpawn { get; set; }
        public float DynamicSpawnChancePeriod { get; set; }
        public float DynamicSpawnChanceEffect { get; set; }
        public float MinimumDevestationToSpawn { get; set; }
        public float DevestationLinearMultiplier { get; set; }
        public PatrolAroundClosestLestInterruptedAndSwitchData? PatrolAroundClosestLestInterruptedAndSwitch { get; set; }
        public IReadOnlyList<PriorityPartyBehaviourData> PriorityPartyBehaviours { get; set; } =
            new List<PriorityPartyBehaviourData>();
        public float ChanceOfSpawn { get; set; }
        public float ChanceInverseConstant { get; set; }
        public PartyTemplateObject PartyTemplate { get; set; }
        public PartyTemplateObject PartyTemplatePrisoner { get; set; }
        public string Name { get; set; }
        public int RepeatSpawnRolls { get; set; }
        public InformationMessage? SpawnMessage { get; set; }
        public InquiryData inquiryMessage { get; set; }
        public bool inquiryPause { get; set; }
        public InformationMessage? DeathMessage { get; set; }
        public int SoundEvent { get; set; }
        public bool InheritClanFromSettlement { get; set; }
    }

    public abstract class PriorityPartyBehaviourData
    {
    }

    public sealed class BesiegeClosestSettlementData : PriorityPartyBehaviourData
    {
        public IReadOnlyList<SpawnSettlementType> TargetTypes { get; }
        public float MinimumAttackerToTargetRatio { get; }
        public float MinimumAttackerToRoamingHostilesRatio { get; }
        public float MinimumRequiredClearTimeInDays { get; }
        public float MaximumRequiredClearTimeInDays { get; }
        public float SkipWhenTargetStrengthRatio { get; }
        public float MinimumOperationWindowInDays { get; }
        public float MaximumOperationWindowInDays { get; }
        public float GracePeriodInDays { get; }
        public int MaximumFailedAttempts { get; }
        public float CooldownInDays { get; }

        public BesiegeClosestSettlementData(IReadOnlyList<SpawnSettlementType> targetTypes,
            float minimumAttackerToTargetRatio, float minimumAttackerToRoamingHostilesRatio,
            float minimumRequiredClearTimeInDays, float maximumRequiredClearTimeInDays,
            float skipWhenTargetStrengthRatio, float minimumOperationWindowInDays,
            float maximumOperationWindowInDays, float gracePeriodInDays, int maximumFailedAttempts,
            float cooldownInDays)
        {
            TargetTypes = new ReadOnlyCollection<SpawnSettlementType>(
                new List<SpawnSettlementType>(targetTypes));
            MinimumAttackerToTargetRatio = minimumAttackerToTargetRatio;
            MinimumAttackerToRoamingHostilesRatio = minimumAttackerToRoamingHostilesRatio;
            MinimumRequiredClearTimeInDays = minimumRequiredClearTimeInDays;
            MaximumRequiredClearTimeInDays = maximumRequiredClearTimeInDays;
            SkipWhenTargetStrengthRatio = skipWhenTargetStrengthRatio;
            MinimumOperationWindowInDays = minimumOperationWindowInDays;
            MaximumOperationWindowInDays = maximumOperationWindowInDays;
            GracePeriodInDays = gracePeriodInDays;
            MaximumFailedAttempts = maximumFailedAttempts;
            CooldownInDays = cooldownInDays;
        }
    }

    public sealed class RaidClosestVillageData : PriorityPartyBehaviourData
    {
        public float MinimumAttackerToTargetRatio { get; }
        public float MinimumAttackerToRoamingHostilesRatio { get; }
        public float MinimumRequiredClearTimeInDays { get; }
        public float MaximumRequiredClearTimeInDays { get; }
        public float SkipWhenTargetStrengthRatio { get; }
        public float MinimumOperationWindowInDays { get; }
        public float MaximumOperationWindowInDays { get; }
        public float GracePeriodInDays { get; }
        public int MaximumFailedAttempts { get; }
        public float CooldownInDays { get; }

        public RaidClosestVillageData(float minimumAttackerToTargetRatio,
            float minimumAttackerToRoamingHostilesRatio, float minimumRequiredClearTimeInDays,
            float maximumRequiredClearTimeInDays, float skipWhenTargetStrengthRatio,
            float minimumOperationWindowInDays, float maximumOperationWindowInDays,
            float gracePeriodInDays, int maximumFailedAttempts, float cooldownInDays)
        {
            MinimumAttackerToTargetRatio = minimumAttackerToTargetRatio;
            MinimumAttackerToRoamingHostilesRatio = minimumAttackerToRoamingHostilesRatio;
            MinimumRequiredClearTimeInDays = minimumRequiredClearTimeInDays;
            MaximumRequiredClearTimeInDays = maximumRequiredClearTimeInDays;
            SkipWhenTargetStrengthRatio = skipWhenTargetStrengthRatio;
            MinimumOperationWindowInDays = minimumOperationWindowInDays;
            MaximumOperationWindowInDays = maximumOperationWindowInDays;
            GracePeriodInDays = gracePeriodInDays;
            MaximumFailedAttempts = maximumFailedAttempts;
            CooldownInDays = cooldownInDays;
        }
    }

    public sealed class PatrolAroundClosestLestInterruptedAndSwitchData : PriorityPartyBehaviourData
    {
        public float MinStableDays { get; }
        public float MaxStableDays { get; }
        public List<SpawnSettlementType> PreferredSettlementTypes { get; }

        public PatrolAroundClosestLestInterruptedAndSwitchData(float minStableDays, float maxStableDays,
            List<SpawnSettlementType> preferredSettlementTypes)
        {
            MinStableDays = minStableDays;
            MaxStableDays = maxStableDays;
            PreferredSettlementTypes = preferredSettlementTypes;
        }
    }

}
