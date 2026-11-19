using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CustomSpawns.Invasions.GarrisonArmies
{
    /**
     * When each invasion last raised an army out of its capital's garrison, by its founding clan, and why it last
     * did not. Only the day is saved: the army itself is found again on the map, and a reload that forgot the day
     * would raise the next one at once.
     */
    public sealed class InvasionGarrisonArmyClock
    {
        private readonly Dictionary<string, double> _lastRaisedDays = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _lastSkipReasons = new(StringComparer.Ordinal);

        // Null for an invasion that has never raised one.
        public double? FindDaysSinceLastRaise(string clanId, double today) =>
            _lastRaisedDays.TryGetValue(clanId, out double day) ? today - day : null;

        public void MarkRaised(string clanId, double day)
        {
            _lastRaisedDays[clanId] = day;
            _lastSkipReasons.Remove(clanId);
        }

        // An army skipped for the same reason day after day is logged once, on the first.
        public bool ShouldLogSkip(string clanId, string reason)
        {
            if (_lastSkipReasons.TryGetValue(clanId, out string last) && last == reason)
            {
                return false;
            }

            _lastSkipReasons[clanId] = reason;
            return true;
        }

        public Dictionary<string, string> Export() => _lastRaisedDays.ToDictionary(entry => entry.Key,
            entry => entry.Value.ToString("R", CultureInfo.InvariantCulture));

        // A day the save cannot read is forgotten, and that invasion's next army is due at once.
        public void Restore(Dictionary<string, string>? saved)
        {
            _lastRaisedDays.Clear();
            _lastSkipReasons.Clear();

            foreach (KeyValuePair<string, string> entry in saved ?? new Dictionary<string, string>())
            {
                if (double.TryParse(entry.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double day))
                {
                    _lastRaisedDays[entry.Key] = day;
                }
            }
        }
    }
}
