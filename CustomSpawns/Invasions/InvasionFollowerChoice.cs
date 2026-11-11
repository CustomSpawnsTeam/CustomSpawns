using System.Collections.Generic;
using System.Linq;

namespace CustomSpawns.Invasions
{
    // A follower configured for an invasion line, and whether it could still be launched.
    public readonly struct InvasionFollowerCandidate
    {
        public string TemplateId { get; }
        public bool IsInField { get; }
        public bool IsClanEliminated { get; }

        public InvasionFollowerCandidate(string templateId, bool isInField, bool isClanEliminated)
        {
            TemplateId = templateId;
            IsInField = isInField;
            IsClanEliminated = isClanEliminated;
        }
    }

    // Which invasion a line launches as one of its kingdoms falls.
    public sealed class InvasionFollowerChoice
    {
        /**
         * The first configured follower that is neither in the field nor of a clan gone from the world, in the
         * order it was configured; null when the line has none left, so it launches a copy of its first invasion
         * instead. A follower whose clan is gone could only fail to land.
         */
        public string? FindConfiguredFollower(IEnumerable<InvasionFollowerCandidate> followers) => followers
            .Where(follower => !follower.IsInField && !follower.IsClanEliminated)
            .Select(follower => follower.TemplateId)
            .FirstOrDefault();
    }
}
