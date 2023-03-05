using System.Collections.Generic;

namespace CustomSpawns.Data.Model.Dialogue
{
    public class DefaultDialogue : List<Dialogue>
    {
        // There is currently no use case requiring the handling of settlement dialogues for custom spawn troops / parties.
        // Custom spawn parties without a leader can not be talked to via the settlement interface (e1.8.0)
        // Even if a custom spawn lord was to be in a settlement, it would trigger by default the vanilla's lord dialogue.
        // If CaW or submods require settlement dialogues, the mod should add their own custom dialogue instead of relying
        // on this default implementation.
        public DefaultDialogue()
        {
            Capacity = 3;
            Add(HostileAttackDialogue());
            Add(HostileDefendDialogue());
            Add(FriendlyDialogue());
        }

        private static string BaseDefaultCondition = "!IsPlayerEncounterInsideSettlement " +
                                                     "AND !IsBanditParty " +
                                                     "AND !IsLordParty " +
                                                     "AND IsCustomSpawnParty";

        private Dialogue HostileAttackDialogue()
        {
            Dialogue dialogue = new();
            dialogue.IsPlayerDialogue = false;
            dialogue.Text = "That's a nice head on your shoulders!";
            dialogue.Consequence = "Battle";
            dialogue.Condition = BaseDefaultCondition + " AND IsHostile AND IsAttacking";
            dialogue.Options = new();
            return dialogue;
        }
        
        private Dialogue HostileDefendDialogue()
        {
            Dialogue dialogue = new();
            dialogue.IsPlayerDialogue = false;
            dialogue.Text = "I will drink from your skull!";
            dialogue.Consequence = "Battle";
            dialogue.Condition = BaseDefaultCondition + " AND IsHostile AND IsDefending";
            dialogue.Options = new();
            return dialogue;
        }
        
        private Dialogue FriendlyDialogue()
        {
            Dialogue dialogue = new();
            dialogue.IsPlayerDialogue = false;
            dialogue.Text = "It's almost harvesting season!";
            dialogue.Condition = BaseDefaultCondition + " AND IsFriendly";
            dialogue.Options = new();
            dialogue.Options.Add(AttackFriendlyDialogue());
            dialogue.Options.Add(LeaveFriendlyDialogue());
            return dialogue;
        }
        
        private Dialogue LeaveFriendlyDialogue()
        {
            Dialogue dialogue = new();
            dialogue.IsPlayerDialogue = true;
            dialogue.Text = "Away with you vile beggar!";
            dialogue.Consequence = "Leave";
            dialogue.Options = new();
            return dialogue;
        }
        
        private Dialogue AttackFriendlyDialogue()
        {
            Dialogue dialogue = new();
            dialogue.IsPlayerDialogue = true;
            dialogue.Text = "I will tear you limb from limb!";
            dialogue.Consequence = "War";
            dialogue.Options = new();
            return dialogue;
        }
    }
}