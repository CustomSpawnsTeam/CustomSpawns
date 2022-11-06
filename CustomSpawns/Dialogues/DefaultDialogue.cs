using System.Collections.Generic;
using CustomSpawns.Data.Model;
using CustomSpawns.Dialogues.DialogueAlgebra;

namespace CustomSpawns.Dialogues
{
    public class DefaultDialogue
    {
        private readonly DialogueConditionInterpretor _dialogueConditionInterpretor;
        private readonly DialogueConsequenceInterpretor _dialogueConsequenceInterpretor;

        // There is currently no use case requiring the handling of settlement dialogues for custom spawn troops / parties.
        // Custom spawn parties without a leader can not be talked to via the settlement interface (e1.8.0)
        // Even if a custom spawn lord was to be in a settlement, it would trigger by default the vanilla's lord dialogue.
        // If CaW or submods require settlement dialogues, the mod should add their own custom dialogue instead of relying
        // on this default implementation.
        public DefaultDialogue(DialogueConditionInterpretor dialogueConditionInterpretor, DialogueConsequenceInterpretor dialogueConsequenceInterpretor)
        {
            _dialogueConditionInterpretor = dialogueConditionInterpretor;
            _dialogueConsequenceInterpretor = dialogueConsequenceInterpretor;
            DefaultDialogueData = new();
            DefaultDialogueData.Add(HostileAttackDialogue());
            DefaultDialogueData.Add(HostileDefendDialogue());
            DefaultDialogueData.Add(FriendlyDialogue());
        }
        
        public List<Dialogue> DefaultDialogueData { get; }

        private Dialogue HostileAttackDialogue()
        {
            DialogueCondition dialogueCondition =
                _dialogueConditionInterpretor.ParseCondition("!IsPlayerEncounterInsideSettlement " +
                                                             "AND IsCustomSpawnParty AND IsHostile AND IsAttacking");
            DialogueConsequence consequence = _dialogueConsequenceInterpretor.ParseConsequence("Battle");
            Dialogue dialogue = new();
            dialogue.Dialogue_ID = "CS_Default_Dialogue_1";
            dialogue.InjectedToTaleworlds = false;
            dialogue.IsPlayerDialogue = false;
            dialogue.DialogueText = "That's a nice head on your shoulders!";
            dialogue.Consequence = consequence;
            dialogue.Condition = dialogueCondition;
            dialogue.Children = new();
            return dialogue;
        }
        
        private Dialogue HostileDefendDialogue()
        {
            DialogueCondition dialogueCondition =
                _dialogueConditionInterpretor.ParseCondition("!IsPlayerEncounterInsideSettlement " +
                                                             "AND IsCustomSpawnParty AND IsHostile AND IsDefending");
            DialogueConsequence consequence = _dialogueConsequenceInterpretor.ParseConsequence("Battle");
            Dialogue dialogue = new();
            dialogue.Dialogue_ID = "CS_Default_Dialogue_2";
            dialogue.InjectedToTaleworlds = false;
            dialogue.IsPlayerDialogue = false;
            dialogue.DialogueText = "I will drink from your skull!";
            dialogue.Consequence = consequence;
            dialogue.Condition = dialogueCondition;
            dialogue.Children = new();
            return dialogue;
        }
        
        private Dialogue FriendlyDialogue()
        {
            DialogueCondition dialogueCondition =
                _dialogueConditionInterpretor.ParseCondition("!IsPlayerEncounterInsideSettlement " +
                                                             "AND IsCustomSpawnParty AND IsFriendly");
            Dialogue dialogue = new();
            dialogue.Dialogue_ID = "CS_Default_Dialogue_3";
            dialogue.InjectedToTaleworlds = false;
            dialogue.IsPlayerDialogue = false;
            dialogue.DialogueText = "It's almost harvesting season!";
            dialogue.Condition = dialogueCondition;
            dialogue.Children = new();
            dialogue.Children.Add(AttackFriendlyDialogue());
            dialogue.Children.Add(LeaveFriendlyDialogue());
            return dialogue;
        }
        
        private Dialogue LeaveFriendlyDialogue()
        {
            DialogueConsequence consequence = _dialogueConsequenceInterpretor.ParseConsequence("Leave");
            Dialogue dialogue = new();
            dialogue.Dialogue_ID = "CS_Default_Dialogue_4";
            dialogue.InjectedToTaleworlds = false;
            dialogue.IsPlayerDialogue = true;
            dialogue.DialogueText = "Away with you vile beggar!";
            dialogue.Consequence = consequence;
            dialogue.Children = new();
            return dialogue;
        }
        
        private Dialogue AttackFriendlyDialogue()
        {
            DialogueConsequence consequence = _dialogueConsequenceInterpretor.ParseConsequence("Battle");
            Dialogue dialogue = new();
            dialogue.Dialogue_ID = "CS_Default_Dialogue_5";
            dialogue.InjectedToTaleworlds = false;
            dialogue.IsPlayerDialogue = true;
            dialogue.DialogueText = "I will tear you limb from limb!";
            dialogue.Consequence = consequence;
            dialogue.Children = new();
            return dialogue;
        }
    }
}