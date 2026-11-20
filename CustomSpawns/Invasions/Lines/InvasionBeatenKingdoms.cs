using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions.Lines
{
    /**
     * Which invasion lines beat each fallen kingdom: every line conquering it as its target when it fell. A fallen
     * kingdom is owed to those lines alone, so one line's conquest never sends another line's next realm, and a
     * kingdom that fell while no invasion targeted it is owed to no line, even when only one line is fighting.
     */
    public sealed class InvasionBeatenKingdoms
    {
        private const char ListSeparator = ',';
        private readonly Dictionary<string, HashSet<string>> _lineTemplateIdsByKingdomId = new(StringComparer.Ordinal);

        // A line that saw its target fall beat it; seeing it again on a later day changes nothing.
        public void ObserveFall(string lineTemplateId, string kingdomId)
        {
            if (!_lineTemplateIdsByKingdomId.TryGetValue(kingdomId, out HashSet<string> lineTemplateIds))
            {
                lineTemplateIds = new HashSet<string>(StringComparer.Ordinal);
                _lineTemplateIdsByKingdomId.Add(kingdomId, lineTemplateIds);
            }

            lineTemplateIds.Add(lineTemplateId);
        }

        public bool WasBeatenBy(string lineTemplateId, string kingdomId) =>
            _lineTemplateIdsByKingdomId.TryGetValue(kingdomId, out HashSet<string> lineTemplateIds) &&
            lineTemplateIds.Contains(lineTemplateId);

        // How many of the fallen kingdoms the line beat.
        public int CountBeatenBy(string lineTemplateId, IEnumerable<string> fallenKingdomIds) =>
            fallenKingdomIds.Count(kingdomId => WasBeatenBy(lineTemplateId, kingdomId));

        // One entry per kingdom, its lines joined by commas.
        public Dictionary<string, string> Export() => _lineTemplateIdsByKingdomId.ToDictionary(
            entry => entry.Key, entry => string.Join(ListSeparator.ToString(), entry.Value));

        public void Restore(Dictionary<string, string>? saved)
        {
            _lineTemplateIdsByKingdomId.Clear();

            foreach (KeyValuePair<string, string> entry in saved ?? new Dictionary<string, string>())
            {
                foreach (string lineTemplateId in entry.Value.Split(ListSeparator).Where(id => id.Length > 0))
                {
                    ObserveFall(lineTemplateId, entry.Key);
                }
            }
        }
    }
}
