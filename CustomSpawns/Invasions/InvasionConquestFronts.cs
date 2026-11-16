using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    // When a conquering invasion starts a new war of its own, and how much its neighbours fear it.
    public sealed class InvasionConquestFronts
    {
        /**
         * An invader goes for whatever lies closest. When the nearest fief belongs to a kingdom it is at peace
         * with, it declares war on that kingdom rather than march past it, but only once its last new front
         * has had time to settle.
         */
        public string? FindFrontToOpen(InvasionConquest conquest, InvasionConquestRules rules,
            IEnumerable<InvasionPrey> prey, double today)
        {
            if (rules.OpenFrontIntervalInDays <= 0f || today - conquest.LastFrontDay < rules.OpenFrontIntervalInDays)
            {
                return null;
            }

            InvasionPrey? nearest = prey.OrderBy(candidate => candidate.Distance)
                .Select(candidate => (InvasionPrey?)candidate).FirstOrDefault();

            return nearest == null || nearest.Value.IsAtWar ? null : nearest.Value.KingdomId;
        }

        /**
         * How much more a kingdom wants war with a conquering invader. It grows with every fief the invader
         * holds, faster than the invader's worth does, so a foothold is shrugged off and a realm is feared.
         */
        public float FindThreat(InvasionConquestRules rules, int fiefsHeld, float invaderWorth) =>
            fiefsHeld <= 0 || invaderWorth <= 0f ? 0f : invaderWorth * rules.ThreatPerFiefHeld * fiefsHeld;

        /**
         * The share of the threat a kingdom still heeds while it fights other wars: its strength over its own and
         * its other enemies' together, as vanilla weighs the risk of a war. A kingdom stretched thin opens no second
         * front, however much it fears the invader.
         */
        public float FindStretchedThreat(float threat, float strength, float otherEnemiesStrength) =>
            strength <= 0f ? 0f : threat * strength / (strength + Math.Max(0f, otherEnemiesStrength));

        /**
         * Whether a line holds so much of the land that every other kingdom bands together against it: at least the
         * configured share of all the fiefs there are.
         */
        public bool IsCoalitionFormed(InvasionConquestRules rules, int lineFiefs, int allFiefs) =>
            rules.CoalitionFiefShare > 0f && allFiefs > 0 && lineFiefs >= rules.CoalitionFiefShare * allFiefs;

        // Whether a line is so beaten that the coalition against it breaks up: none of its capitals held, and less
        // than the configured share of all the fiefs there are.
        public bool IsCoalitionSpent(InvasionConquestRules rules, int lineFiefs, int allFiefs, bool holdsACapital) =>
            rules.CoalitionDisbandFiefShare > 0f && !holdsACapital &&
            lineFiefs < rules.CoalitionDisbandFiefShare * allFiefs;
    }
}
