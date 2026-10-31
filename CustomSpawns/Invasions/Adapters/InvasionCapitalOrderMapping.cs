using System;
using CustomSpawns.Invasions.CapitalObjectives;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CustomSpawns.Invasions.Adapters
{
    public readonly struct InvasionCapitalOrderIntent
    {
        public InvasionCapitalOrderIntent(AiBehavior behavior, bool gathers = false)
        {
            Behavior = behavior;
            Gathers = gathers;
        }

        public AiBehavior Behavior { get; }
        public bool Gathers { get; }

        public void PrepareMovement(PartyThinkParams thinkParams, Action moveToStaging)
        {
            if (Gathers)
            {
                moveToStaging();
                thinkParams.DoNotChangeBehavior = true;
            }
        }
    }

    /**
     * Preserves gathering movement; advancing objectives receive their native target order.
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

            if (phase == InvasionCapitalObjectivePhase.Gathering)
            {
                // Native army thinking requires a military candidate even when movement is preserved.
                return new InvasionCapitalOrderIntent(AiBehavior.DefendSettlement, true);
            }

            return null;
        }
    }
}
