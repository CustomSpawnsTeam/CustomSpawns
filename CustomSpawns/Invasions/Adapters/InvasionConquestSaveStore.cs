using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CustomSpawns.Invasions.Adapters
{
    /**
     * Writes every conquest into the save as one line per invasion clan,
     * target|startedDay|lastFrontDay|leader|follower|lastReconsideredDay|warsLed, the kingdoms whose war it leads
     * joined by commas, and reads older saves that hold fewer fields.
     */
    public sealed class InvasionConquestSaveStore
    {
        private const char Separator = '|';
        private const char ListSeparator = ',';
        private readonly InvasionConquests _invasionConquests;

        public InvasionConquestSaveStore(InvasionConquests invasionConquests)
        {
            _invasionConquests = invasionConquests;
        }

        public Dictionary<string, string> Export() => _invasionConquests.All.ToDictionary(
            conquest => conquest.InvasionClanId,
            conquest => conquest.TargetKingdomId + Separator +
                        conquest.StartedDay.ToString("R", CultureInfo.InvariantCulture) + Separator +
                        conquest.LastFrontDay.ToString("R", CultureInfo.InvariantCulture) + Separator +
                        conquest.LeaderClanId + Separator + conquest.FollowerClanId + Separator +
                        conquest.LastReconsideredDay.ToString("R", CultureInfo.InvariantCulture) + Separator +
                        string.Join(ListSeparator.ToString(), conquest.WarsLed));

        /**
         * A save without a last front dates it to the conquest. A save that marked the conquest settled names a
         * fallen target, so the invasion turns on its next one on the first day.
         */
        public void Restore(Dictionary<string, string>? saved)
        {
            _invasionConquests.Clear();

            foreach (KeyValuePair<string, string> entry in saved ?? new Dictionary<string, string>())
            {
                string[] fields = entry.Value.Split(Separator);

                if (fields.Length >= 2 &&
                    double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double day))
                {
                    double lastFrontDay = fields.Length >= 3 && double.TryParse(fields[2], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double front)
                        ? front
                        : day;
                    // A save older than reconsidering dates the last one to the conquest.
                    double? lastReconsideredDay = fields.Length >= 6 && double.TryParse(fields[5],
                        NumberStyles.Float, CultureInfo.InvariantCulture, out double reconsidered)
                        ? reconsidered
                        : null;
                    _invasionConquests.Restore(new InvasionConquest(entry.Key, fields[0], day, lastFrontDay,
                        FindSavedClanId(fields, 3), FindSavedClanId(fields, 4), lastReconsideredDay,
                        FindSavedWarsLed(fields)));
                }
            }
        }

        // A save older than leaders and followers has neither.
        private static string? FindSavedClanId(string[] fields, int index) =>
            fields.Length > index && fields[index].Length > 0 ? fields[index] : null;

        // A save older than led wars leads none, so the line's leader leads each war it already fights.
        private static IEnumerable<string> FindSavedWarsLed(string[] fields) =>
            fields.Length > 6
                ? fields[6].Split(ListSeparator).Where(kingdomId => kingdomId.Length > 0)
                : Enumerable.Empty<string>();
    }
}
