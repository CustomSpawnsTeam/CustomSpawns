using CustomSpawns.Invasions.CapitalObjectives;
using CustomSpawns.AI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Invasions.Adapters
{
    public readonly struct InvasionCapitalOrderIntent
    {
        public InvasionCapitalOrderIntent(AiBehavior behavior)
        {
            Behavior = behavior;
        }

        public AiBehavior Behavior { get; }
    }

    /**
     * Maps the advancing domain phase to the Bannerlord target order the native party thinker consumes.
     * Every other phase deliberately maps to no order so native army gathering and siege AI retain control.
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

            if (phase == InvasionCapitalObjectivePhase.Advancing)
            {
                return new InvasionCapitalOrderIntent(behavior);
            }

            return null;
        }
    }
}
