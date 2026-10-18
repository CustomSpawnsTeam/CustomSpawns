using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.AI.Adapters;
using CustomSpawns.AI.Offensives;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Invasions.CapitalObjectives;
using NLog;

namespace CustomSpawns.Invasions.Adapters
{
    public sealed class BannerlordInvasionAssaultConfigurationProvider
        : IInvasionAssaultConfigurationProvider
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly ISpawnDao _spawnDao;
        private readonly SpawnPartyBehaviourLookup _spawnPartyBehaviourLookup;
        private readonly Action<string> _warn;
        private readonly ISet<string> _reportedMultipleConfigurations =
            new HashSet<string>(StringComparer.Ordinal);

        public BannerlordInvasionAssaultConfigurationProvider(ISpawnDao spawnDao,
            SpawnPartyBehaviourLookup spawnPartyBehaviourLookup)
            : this(spawnDao, spawnPartyBehaviourLookup, message => Logger.Warn(message))
        {
        }

        public BannerlordInvasionAssaultConfigurationProvider(ISpawnDao spawnDao,
            SpawnPartyBehaviourLookup spawnPartyBehaviourLookup, Action<string> warn)
        {
            _spawnDao = spawnDao ?? throw new ArgumentNullException(nameof(spawnDao));
            _spawnPartyBehaviourLookup = spawnPartyBehaviourLookup ??
                                         throw new ArgumentNullException(nameof(spawnPartyBehaviourLookup));
            _warn = warn ?? throw new ArgumentNullException(nameof(warn));
        }

        public InvasionAssaultRequirements? Find(string spawnTemplateId)
        {
            SpawnDto? spawn = _spawnDao.FindByPartyTemplateId(spawnTemplateId);
            if (spawn?.Invasion == null || !TryMapSupplyLine(spawn.Invasion.SupplyLine,
                    out InvasionSupplyLineKind supplyLine))
            {
                return null;
            }

            List<BesiegeClosestSettlementData> configurations = _spawnPartyBehaviourLookup
                .FindBehaviours(spawnTemplateId)
                .OfType<BesiegeClosestSettlementData>()
                .Where(configuration => configuration.TargetTypes.Contains(SpawnSettlementType.Town))
                .ToList();
            BesiegeClosestSettlementData? selected = configurations.FirstOrDefault();
            if (selected == null)
            {
                return null;
            }

            if (configurations.Count > 1 && _reportedMultipleConfigurations.Add(spawnTemplateId))
            {
                _warn("Invasion spawn '" + spawnTemplateId + "' has several Town-capable " +
                      "BesiegeClosestSettlement behaviours; the first configured behaviour is used " +
                      "for the capital objective.");
            }

            IReadOnlyList<SiegeTargetKind> targetKinds = selected.TargetTypes
                .Select(MapTargetKind)
                .Where(kind => kind.HasValue)
                .Select(kind => kind!.Value)
                .Distinct()
                .ToList();

            return new InvasionAssaultRequirements(targetKinds,
                selected.MinimumAttackerToTargetRatio,
                selected.MinimumAttackerToRoamingHostilesRatio,
                spawn.Invasion.RequiresFirstCoastalConquest, supplyLine);
        }

        private static SiegeTargetKind? MapTargetKind(SpawnSettlementType targetType)
        {
            if (targetType == SpawnSettlementType.Town)
            {
                return SiegeTargetKind.Town;
            }

            if (targetType == SpawnSettlementType.Castle)
            {
                return SiegeTargetKind.Castle;
            }

            return null;
        }

        private static bool TryMapSupplyLine(string? configuredSupplyLine,
            out InvasionSupplyLineKind supplyLine)
        {
            if (string.IsNullOrWhiteSpace(configuredSupplyLine) || configuredSupplyLine == "Coastal")
            {
                supplyLine = InvasionSupplyLineKind.Coastal;
                return true;
            }

            if (configuredSupplyLine == "Any")
            {
                supplyLine = InvasionSupplyLineKind.Any;
                return true;
            }

            if (configuredSupplyLine == "None")
            {
                supplyLine = InvasionSupplyLineKind.None;
                return true;
            }

            supplyLine = default;
            return false;
        }
    }
}
