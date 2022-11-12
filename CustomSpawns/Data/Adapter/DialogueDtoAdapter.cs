using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Dto;
using CustomSpawns.Data.Model.Dialogue;
using CustomSpawns.Dialogues;

namespace CustomSpawns.Data.Adapter
{
    public class DialogueDtoAdapter
    {
        private readonly DialogueConsequenceInterpretor _consequenceInterpretor;
        private readonly DialogueConditionInterpretor _conditionInterpretor;
        private int _currentId = -1;

        public DialogueDtoAdapter(DialogueConsequenceInterpretor consequenceInterpretor,
            DialogueConditionInterpretor conditionInterpretor)
        {
            _consequenceInterpretor = consequenceInterpretor;
            _conditionInterpretor = conditionInterpretor;
        }

        public List<DialogueDto> Adapt(Dialogue dialogue)
        {
            List<DialogueDto> dialogueDtos = new();
            DialogueDto dialogueDto = new();
            dialogueDto.Type = dialogue.Type;

            if (dialogue.Condition != null)
            {
                try
                {
                    dialogueDto.Condition = _conditionInterpretor.ParseCondition(dialogue.Condition);
                }
                catch (ArgumentException e)
                {
                    throw new ArgumentException("The dialogue condition with text \""
                                                + dialogue.Text + "\" is not correct. Reason:", e.Message);
                }
            }

            if (dialogue.Consequence != null)
            {
                try
                {
                    dialogueDto.Consequence = _consequenceInterpretor.ParseConsequence(dialogue.Consequence);
                }
                catch (ArgumentException e)
                {
                    throw new ArgumentException("The dialogue consequence with text \""
                                                + dialogue.Text + "\" is not correct. Reason:", e.Message);
                }
            }

            dialogueDto.InjectedToTaleworlds = false;
            dialogueDto.Text = dialogue.Text;
            dialogueDto.Id = GenerateId();

            dialogueDto.IsPlayerDialogue = dialogue.IsPlayerDialogue;
            IList<Dialogue>? options = dialogue.Options;
            if (options != null && options.Count > 0)
            {
                List<DialogueDto> optionsDto = new(options.Count);
                foreach (Dialogue option in options)
                {
                    optionsDto.AddRange(Adapt(option));
                }

                dialogueDto.Options = optionsDto;
            }
            else
            {
                dialogueDto.Options = new List<DialogueDto>(0);
            }

            IList<Dialogue>? alternativesDialogues = dialogue.Parents;
            if (alternativesDialogues != null && alternativesDialogues.Count > 0)
            {
                List<DialogueDto> alternativeDialogueDtos = alternativesDialogues
                    .Select(Adapt)
                    .Aggregate((allAlternativesDialogues, currentAlternativesDialogue) =>
                    {
                        allAlternativesDialogues.AddRange(currentAlternativesDialogue);
                        return allAlternativesDialogues;
                    })
                    .Select(alternativesDialogue =>
                    {
                        alternativesDialogue.Options = dialogueDto.Options;
                        return alternativesDialogue;
                    })
                    .ToList();
                dialogueDtos.AddRange(alternativeDialogueDtos);
            }
            dialogueDtos.Add(dialogueDto);
            return dialogueDtos;
        }

        private string GenerateId()
        {
            _currentId++;
            return "CS_Dialogue_" + _currentId;
        }
    }
}