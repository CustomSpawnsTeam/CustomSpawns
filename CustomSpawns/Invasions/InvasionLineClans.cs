using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    /**
     * The clans an invasion line founds to carry on its conquest once its configured followers are all in the
     * field. Each copy fights under its line's first invasion's spawn template, so the template no longer tells
     * one invasion of the line from another: this remembers, for every copy, the invasion it was launched for
     * and the configured clan it was modelled on.
     */
    public sealed class InvasionLineClans
    {
        private const char Separator = '|';
        private const string CloneSuffix = "_line_";
        private readonly Dictionary<string, (string LauncherClanId, string ModelClanId)> _clones =
            new(StringComparer.Ordinal);

        // Every copy with the configured clan it was modelled on.
        public IEnumerable<(string CloneClanId, string ModelClanId)> All =>
            _clones.Select(entry => (entry.Key, entry.Value.ModelClanId));

        public void Register(string cloneClanId, string launcherClanId, string modelClanId)
        {
            _clones[cloneClanId] = (launcherClanId, modelClanId);
        }

        public bool IsClone(string? clanId) => clanId != null && _clones.ContainsKey(clanId);

        public string? FindLauncherClanId(string? clanId) =>
            clanId != null && _clones.TryGetValue(clanId, out var clone) ? clone.LauncherClanId : null;

        // The id the next copy of a configured clan asks for; vanilla still makes it unique if it is taken.
        public string NextCloneClanId(string modelClanId) =>
            modelClanId + CloneSuffix + (_clones.Values.Count(clone => clone.ModelClanId == modelClanId) + 1);

        /**
         * Whether a conquest of candidateClanId leads the invasion of followerClanId. A copy follows the invasion it
         * was launched for and no other; a configured follower follows its configured leader, never a copy that
         * shares that leader's spawn template.
         */
        public bool CanLead(string followerClanId, string candidateClanId, bool candidateRunsLeaderTemplate)
        {
            string? launcherClanId = FindLauncherClanId(followerClanId);

            if (launcherClanId != null)
            {
                return candidateClanId == launcherClanId;
            }

            return candidateRunsLeaderTemplate && !IsClone(candidateClanId);
        }

        public Dictionary<string, string> Export() => _clones.ToDictionary(entry => entry.Key,
            entry => entry.Value.LauncherClanId + Separator + entry.Value.ModelClanId);

        public void Restore(Dictionary<string, string>? saved)
        {
            _clones.Clear();

            foreach (KeyValuePair<string, string> entry in saved ?? new Dictionary<string, string>())
            {
                string[] fields = entry.Value.Split(Separator);

                if (fields.Length == 2 && fields[0].Length > 0 && fields[1].Length > 0)
                {
                    Register(entry.Key, fields[0], fields[1]);
                }
            }
        }
    }
}
