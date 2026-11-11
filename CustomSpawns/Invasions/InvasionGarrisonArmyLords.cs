using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CustomSpawns.Invasions
{
    /**
     * The one-time lord each invasion's garrison army is led by, by the invasion's founding clan. One army at a time,
     * so one lord at most. Saved, because the hero vanilla creates carries an id that says nothing of what he is for,
     * and a lord forgotten across a load would linger as a lord of the realm once his army ended.
     */
    public sealed class InvasionGarrisonArmyLords : IInvasionGarrisonArmyLeaders
    {
        private const char Separator = '|';
        private readonly Dictionary<string, InvasionGarrisonArmyLord> _lords = new(StringComparer.Ordinal);

        public IReadOnlyList<string> FindClanIds() => _lords.Keys.ToList();

        public bool IsArmyOut(string clanId) => _lords.ContainsKey(clanId);

        public InvasionGarrisonArmyLord? Find(string clanId) =>
            _lords.TryGetValue(clanId, out InvasionGarrisonArmyLord lord) ? lord : null;

        public void Record(string clanId, string heroId, int partySizeLimit) =>
            _lords[clanId] = new InvasionGarrisonArmyLord(heroId, partySizeLimit, false);

        public void MarkGathered(string clanId)
        {
            if (_lords.TryGetValue(clanId, out InvasionGarrisonArmyLord lord))
            {
                _lords[clanId] = new InvasionGarrisonArmyLord(lord.HeroId, lord.PartySizeLimit, true);
            }
        }

        public void Forget(string clanId) => _lords.Remove(clanId);

        public bool LeadsGarrisonArmy(string? heroId) => FindPartySizeLimit(heroId) != null;

        public int? FindPartySizeLimit(string? heroId) => heroId == null
            ? null
            : _lords.Values.Where(lord => lord.HeroId == heroId).Select(lord => (int?)lord.PartySizeLimit)
                .FirstOrDefault();

        public Dictionary<string, string> Export() => _lords.ToDictionary(entry => entry.Key,
            entry => entry.Value.HeroId + Separator +
                     entry.Value.PartySizeLimit.ToString(CultureInfo.InvariantCulture) + Separator +
                     entry.Value.HasGathered);

        // An entry the save cannot read is dropped; its lord then stays an ordinary lord of his clan.
        public void Restore(Dictionary<string, string>? saved)
        {
            _lords.Clear();

            foreach (KeyValuePair<string, string> entry in saved ?? new Dictionary<string, string>())
            {
                string[] parts = entry.Value.Split(Separator);

                if (parts.Length == 3 && parts[0].Length > 0 &&
                    int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int limit) &&
                    bool.TryParse(parts[2], out bool hasGathered))
                {
                    _lords[entry.Key] = new InvasionGarrisonArmyLord(parts[0], limit, hasGathered);
                }
            }
        }
    }

    public readonly struct InvasionGarrisonArmyLord
    {
        public InvasionGarrisonArmyLord(string heroId, int partySizeLimit, bool hasGathered)
        {
            HeroId = heroId;
            PartySizeLimit = partySizeLimit;
            HasGathered = hasGathered;
        }

        public string HeroId { get; }

        public int PartySizeLimit { get; }

        // Whether the lords called to his army have finished gathering, which is logged once.
        public bool HasGathered { get; }
    }
}
