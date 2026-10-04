using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dto;

namespace CustomSpawns.Data.Adapter
{
    internal static class InvasionRuntimeConfigurationValidator
    {
        // Each rule brings its own troop template, so existence is asked per rule rather than once.
        public static void ValidateReferences(InvasionData invasion, string spawnPartyTemplateId, bool spawnClanIsBandit,
            Func<string, bool> reinforcementTemplateExists)
        {
            if (spawnClanIsBandit)
            {
                throw new ArgumentException("Invasion spawn '" + spawnPartyTemplateId + "' cannot use a bandit clan.");
            }

            foreach (InvasionReinforcementRuleData rule in invasion.ReinforcementRules)
            {
                if (!reinforcementTemplateExists(rule.PartyTemplateId))
                {
                    throw new ArgumentException("Invasion spawn '" + spawnPartyTemplateId +
                                                "' reinforcement rule '" + rule.Id +
                                                "' references unknown reinforcement party template '" +
                                                rule.PartyTemplateId + "'.");
                }
            }
        }

        /**
         * One faction, one invasion. An invasion is identified by the clan it invades for, so the clan a
         * spawn invades for has to be settled by the configuration and not by the spawn. Overriden clans
         * are drawn at random for every spawn, and an inherited clan is whoever happens to own the landing
         * site, so either would scatter one invasion across as many factions as it rolled.
         */
        public static void ValidateFixedInvasionClan(string spawnPartyTemplateId, int overridenClanCount,
            bool inheritClanFromSettlement)
        {
            if (overridenClanCount != 0)
            {
                throw new ArgumentException("Invasion spawn '" + spawnPartyTemplateId +
                                            "' cannot use OverridenClanSpawns: an invasion invades for one clan, " +
                                            "and an overriden clan is drawn at random for every spawn.");
            }

            if (inheritClanFromSettlement)
            {
                throw new ArgumentException("Invasion spawn '" + spawnPartyTemplateId +
                                            "' cannot use InheritClanFromSettlement: the invading faction would be " +
                                            "whoever owns the settlement it spawns at.");
            }
        }

        /**
         * One faction, one invasion, across the whole configuration. The clan that is invaded for holds the
         * towns the invasion takes, fields its parties and carries the kingdom it leaves behind, so two
         * invasion spawns sharing a clan would keep two sets of books -- footholds, party budget, strength
         * ceiling -- over one faction's single set of towns, parties and troops. The later spawn is refused
         * rather than merged into the first: a invasion reads one spawn template's configuration, so merging
         * would silently discard the other's.
         *
         * Returns the party template ids to refuse, in configuration order.
         */
        public static IReadOnlyList<string> FindInvasionsSharingAClan(
            IEnumerable<KeyValuePair<string, string>> invadedClanByPartyTemplateId)
        {
            var invadedClans = new HashSet<string>(StringComparer.Ordinal);
            var refused = new List<string>();

            foreach (KeyValuePair<string, string> spawn in invadedClanByPartyTemplateId)
            {
                if (!invadedClans.Add(spawn.Value))
                {
                    refused.Add(spawn.Key);
                }
            }

            return refused;
        }

        public static void ValidateEffectiveSiegeTemplate(string spawnPartyTemplateId, string? templateId,
            bool templateExists, IReadOnlyList<PriorityPartyBehaviourData> behaviours)
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                throw new ArgumentException("Invasion spawn '" + spawnPartyTemplateId + "' requires PriorityPartyBehaviourTemplateId.");
            }
            if (!templateExists)
            {
                throw new ArgumentException("Invasion spawn '" + spawnPartyTemplateId + "' references unknown priority party behaviour template '" +
                                            templateId + "'.");
            }
            if (!behaviours.OfType<BesiegeClosestSettlementData>().Any(behaviour =>
                    behaviour.TargetTypes.Contains(SpawnSettlementType.Town)))
            {
                throw new ArgumentException("Invasion spawn '" + spawnPartyTemplateId + "' priority party behaviour template '" +
                                            templateId + "' must contain BesiegeClosestSettlement targeting Town.");
            }
        }
    }

}
