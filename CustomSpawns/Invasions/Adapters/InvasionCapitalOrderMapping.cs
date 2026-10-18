using CustomSpawns.Invasions.CapitalObjectives;
using CustomSpawns.AI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Invasions.Adapters
{
    public readonly struct InvasionCapitalOrderIntent
    {
        public InvasionCapitalOrderIntent(AiBehavior behavior, bool willGatherArmy)
        {
            Behavior = behavior;
            WillGatherArmy = willGatherArmy;
        }

        public AiBehavior Behavior { get; }
        public bool WillGatherArmy { get; }
    }

    /**
     * Maps domain phases to the two Bannerlord knobs the native party thinker consumes.
     * Dormant and besieging objectives deliberately map to no order so vanilla regains control.
     */
    public sealed class InvasionCapitalOrderMapping
    {
        public InvasionCapitalOrderIntent? Find(InvasionCapitalObjectiveKind directiveKind,
            InvasionCapitalObjectivePhase phase, InvasionCapitalObjectiveKind expectedKind,
            AiBehavior behavior)
        {
            if (directiveKind != expectedKind)
            {
                return null;
            }

            if (phase == InvasionCapitalObjectivePhase.Gathering)
            {
                return new InvasionCapitalOrderIntent(behavior, true);
            }

            if (phase == InvasionCapitalObjectivePhase.Advancing)
            {
                return new InvasionCapitalOrderIntent(behavior, false);
            }

            return null;
        }
    }
}
