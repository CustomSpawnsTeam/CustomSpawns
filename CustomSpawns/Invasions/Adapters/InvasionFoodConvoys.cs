using System;
using System.Collections.Generic;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Invasions.Adapters
{
    // Unloads each invasion's due food convoy into its capital's food stocks.
    public sealed class InvasionFoodConvoys
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly InvasionState _invasionState;
        private readonly SpawnDao _spawnDao;

        public InvasionFoodConvoys(InvasionState state, SpawnDao spawns)
        {
            _invasionState = state;
            _spawnDao = spawns;
        }

        public void UnloadDue()
        {
            double nowDays = CampaignTime.Now.ToDays;

            foreach (Invasion invasion in new List<Invasion>(_invasionState.Invasions))
            {
                InvasionData? configuration = GetConfiguration(invasion);

                if (configuration == null || !InvasionFoodConvoyRules.IsConfigured(configuration.FoodPerConvoy,
                        configuration.FoodConvoyIntervalInDays))
                {
                    continue;
                }

                Settlement? capital = invasion.CapitalSettlementId == null
                    ? null
                    : Settlement.Find(invasion.CapitalSettlementId);
                bool reachable = !invasion.IsSuspended && _invasionState.HoldsCapital(invasion) &&
                                 capital?.Town != null && capital.SiegeEvent == null;

                InvasionFoodConvoyDecision decision = InvasionFoodConvoyRules.Decide(invasion.NextFoodConvoyDays,
                    nowDays, configuration.FoodConvoyIntervalInDays, reachable);
                invasion.NextFoodConvoyDays = decision.NextConvoyDays;

                if (!decision.Unloads)
                {
                    continue;
                }

                // Straight into the town's food stocks: grain left in the market only feeds the town once it
                // is sold, which a besieger-ringed invasion capital rarely manages.
                Town town = capital!.Town;
                town.FoodStocks = Math.Min(town.FoodStocks + configuration.FoodPerConvoy, town.FoodStocksUpperLimit());
                Logger.Info("Invasion food convoy unloaded: invasion=" + invasion.ClanId + " capital=" +
                            capital.StringId + " food=" + configuration.FoodPerConvoy + " stocks=" + town.FoodStocks);
            }
        }

        private InvasionData? GetConfiguration(Invasion invasion) =>
            _spawnDao.FindByPartyTemplateId(invasion.SpawnTemplateId)?.Invasion;
    }
}
