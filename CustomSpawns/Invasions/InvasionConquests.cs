using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    /**
     * What an invasion's war with another faction should be. Policy leaves it to the diplomacy configuration;
     * War keeps it at war for good; Open leaves it to the kingdoms themselves, as any two realms of Calradia.
     */
    public enum InvasionWarStance
    {
        Policy,
        War,
        Open
    }

    // The realm an invasion is conquering: the kingdom it first took a fief from, then each neighbour in turn.
    public sealed class InvasionConquest
    {
        public InvasionConquest(string invasionClanId, string targetKingdomId, double startedDay,
            double lastFrontDay)
        {
            InvasionClanId = invasionClanId;
            TargetKingdomId = targetKingdomId;
            StartedDay = startedDay;
            LastFrontDay = lastFrontDay;
        }

        public string InvasionClanId { get; }
        public string TargetKingdomId { get; private set; }

        // When the invasion began conquering its current target.
        public double StartedDay { get; private set; }

        // When the invasion last went to war on a kingdom of its own accord; its first conquest counts.
        public double LastFrontDay { get; private set; }

        public void OpenFront(double today) => LastFrontDay = today;

        // The target has fallen, and the invasion turns on the next.
        public void Retarget(string targetKingdomId, double today)
        {
            TargetKingdomId = targetKingdomId;
            StartedDay = today;
            LastFrontDay = today;
        }
    }

    // A kingdom bordering the invasion, and how strong it is.
    public readonly struct InvasionNeighbour
    {
        public InvasionNeighbour(string kingdomId, float strength)
        {
            KingdomId = kingdomId;
            Strength = strength;
        }

        public string KingdomId { get; }
        public float Strength { get; }
    }

    // A fief the invasion could take next, and how far it lies from the nearest fief the invasion holds.
    public readonly struct InvasionPrey
    {
        public InvasionPrey(string kingdomId, float distance, bool isAtWar)
        {
            KingdomId = kingdomId;
            Distance = distance;
            IsAtWar = isAtWar;
        }

        public string KingdomId { get; }
        public float Distance { get; }
        public bool IsAtWar { get; }
    }

    public readonly struct InvasionConquestRules
    {
        public InvasionConquestRules(float threatPerFiefHeld, float openFrontIntervalInDays,
            float targetSiegePreference = 0f)
        {
            ThreatPerFiefHeld = threatPerFiefHeld;
            OpenFrontIntervalInDays = openFrontIntervalInDays;
            TargetSiegePreference = targetSiegePreference;
        }

        // The share of the invader's own worth every fief it holds adds to a kingdom's will to fight it.
        public float ThreatPerFiefHeld { get; }

        // The fewest days between two wars the invasion starts itself. Zero never starts one.
        public float OpenFrontIntervalInDays { get; }

        // How many times more the invasion's lords want a siege of the conquered kingdom's fiefs. At most one
        // leaves their sieges to vanilla.
        public float TargetSiegePreference { get; }
    }

    public sealed class InvasionConquests
    {
        // The lowest distance score vanilla still weighs a siege at, given to a fief it does not weigh at all.
        private const float UnborderedDistanceScore = 0.1f;
        private const float LeadOverOtherSieges = 1.1f;
        private readonly Dictionary<string, InvasionConquest> _conquestsByClanId = new();

        public IReadOnlyCollection<InvasionConquest> All => _conquestsByClanId.Values;

        public InvasionConquest? Find(string? invasionClanId) =>
            invasionClanId != null && _conquestsByClanId.TryGetValue(invasionClanId, out InvasionConquest conquest)
                ? conquest
                : null;

        /**
         * The first fief an invasion takes from a kingdom names the realm it conquers; later conquests,
         * whoever they are taken from, leave that choice alone until that realm falls. A fief taken from a clan outside any kingdom
         * names nothing.
         */
        public InvasionConquest? Begin(string invasionClanId, string? previousOwnerKingdomId, double today)
        {
            if (previousOwnerKingdomId == null || _conquestsByClanId.ContainsKey(invasionClanId))
            {
                return null;
            }

            var conquest = new InvasionConquest(invasionClanId, previousOwnerKingdomId, today, today);
            _conquestsByClanId.Add(invasionClanId, conquest);
            return conquest;
        }

        public void Restore(InvasionConquest conquest) => _conquestsByClanId[conquest.InvasionClanId] = conquest;

        public void Forget(string invasionClanId) => _conquestsByClanId.Remove(invasionClanId);

        /**
         * Once its target falls, an invasion turns on the weakest kingdom on its borders: an invader that
         * picks a fight with a stronger neighbour is beaten back before it can grow. With no kingdom on its
         * borders it has nothing to turn on.
         */
        public string? FindNextTarget(IEnumerable<InvasionNeighbour> neighbours) =>
            neighbours.OrderBy(neighbour => neighbour.Strength)
                .Select(neighbour => neighbour.KingdomId)
                .FirstOrDefault();

        /**
         * A realm that conquers stays at war with its target and leaves every other kingdom to choose: they
         * fight it, or not, as they would any neighbour. Minor factions and bandits keep their configuration.
         */
        public InvasionWarStance FindStance(string? invasionClanId, string otherFactionId, bool otherIsKingdom)
        {
            InvasionConquest? conquest = Find(invasionClanId);

            if (conquest == null)
            {
                return InvasionWarStance.Policy;
            }

            if (otherFactionId == conquest.TargetKingdomId)
            {
                return InvasionWarStance.War;
            }

            return otherIsKingdom ? InvasionWarStance.Open : InvasionWarStance.Policy;
        }

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
         * A lord's wish to besiege a fief of the kingdom being conquered, from vanilla's. Any such siege vanilla
         * judges can be won is wanted more than every other siege, so the invasion finishes that kingdom before it
         * spreads into the others it fights; among them, the preference multiplies vanilla's wish.
         */
        public float FindTargetSiegeScore(InvasionConquestRules rules, float score, float bestOtherSiegeScore) =>
            rules.TargetSiegePreference <= 1f || score <= 0f
                ? score
                : System.Math.Max(score * rules.TargetSiegePreference,
                    bestOtherSiegeScore * LeadOverOtherSieges);

        /**
         * A lord's wish to besiege a fief of the kingdom being conquered that vanilla does not weigh, because the
         * invasion holds no fortification near it. Only while no fief of that kingdom is worth a siege does the lord
         * want it, and then more than any other siege, so the invasion marches on to finish the kingdom instead of
         * turning on its neighbours. A lord already besieging it keeps wanting it, so it does not leave the siege for
         * a nearer one. A fief it cannot take, as vanilla judges it, stays unwanted.
         */
        public float FindUnborderedTargetSiegeScore(InvasionConquestRules rules, float targetScore,
            float bestTargetSiegeScore, float bestOtherSiegeScore, bool isBesiegingIt) =>
            rules.TargetSiegePreference <= 1f || targetScore <= 0f || (bestTargetSiegeScore > 0f && !isBesiegingIt)
                ? 0f
                : System.Math.Max(targetScore * UnborderedDistanceScore * rules.TargetSiegePreference,
                    bestOtherSiegeScore * LeadOverOtherSieges);
    }
}
