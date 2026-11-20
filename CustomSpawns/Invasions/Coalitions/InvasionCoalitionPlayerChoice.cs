using System;

namespace CustomSpawns.Invasions.Coalitions
{
    public enum InvasionCoalitionAnswer
    {
        Unasked,
        Joined,
        Declined
    }

    /**
     * Whether the player's kingdom stands in the coalition. It never does unless the player joins when asked. Each
     * coalition asks once: the answer holds until the coalition breaks up, and is then forgotten, so the next
     * coalition asks again.
     */
    public sealed class InvasionCoalitionPlayerChoice
    {
        // An open question is not saved: a game saved before the player answered asks again once loaded.
        private bool _isAsking;

        public InvasionCoalitionAnswer Answer { get; private set; }

        public bool ShouldAsk => Answer == InvasionCoalitionAnswer.Unasked && !_isAsking;

        // Every kingdom but the player's may stand in a coalition; the player's only once the player joins.
        public bool Includes(bool isPlayerKingdom) => !isPlayerKingdom || Answer == InvasionCoalitionAnswer.Joined;

        public void Ask() => _isAsking = true;

        public void Join() => SetAnswer(InvasionCoalitionAnswer.Joined);

        public void Decline() => SetAnswer(InvasionCoalitionAnswer.Declined);

        public void Forget() => SetAnswer(InvasionCoalitionAnswer.Unasked);

        public string Export() => Answer.ToString();

        public void Restore(string? saved) =>
            SetAnswer(Enum.TryParse(saved, out InvasionCoalitionAnswer answer) ? answer : InvasionCoalitionAnswer.Unasked);

        private void SetAnswer(InvasionCoalitionAnswer answer)
        {
            Answer = answer;
            _isAsking = false;
        }
    }
}
