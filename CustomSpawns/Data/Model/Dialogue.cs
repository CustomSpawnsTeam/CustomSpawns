using System.Collections.Generic;
using CustomSpawns.Dialogues.DialogueAlgebra;

namespace CustomSpawns.Data.Model
{
    public class Dialogue
    {
        public string DialogueText { get; set; }
        public string Dialogue_ID { get; set; }
        public DialogueCondition Condition { get; set; }
        public DialogueConsequence Consequence { get; set; }
        public bool IsPlayerDialogue { get; set; }

        public List<Dialogue> Children { get; set; }

        public Dialogue Parent { get; set; }

        public bool InjectedToTaleworlds { get; set; }
    }
}