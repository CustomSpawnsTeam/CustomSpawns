using System;
using System.Collections.Generic;
using System.Linq;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Dto;
using CustomSpawns.Data.Model.Dialogue;
using CustomSpawns.Data.Reader.Impl;
using CustomSpawns.Utils;

namespace CustomSpawns.Data.Dao
{
    public class DialogueDao
    {
        private readonly DialogueDataReader _dialogueDataReader;
        private readonly DialogueDtoAdapter _dialogueDtoAdapter;
        private readonly MessageBoxService _messageBoxService;
        private List<DialogueDto>? _dialogue;

        public DialogueDao(DialogueDataReader dialogueDataReader, DialogueDtoAdapter dialogueDtoAdapter, MessageBoxService messageBoxService)
        {
            _dialogueDataReader = dialogueDataReader;
            _dialogueDtoAdapter = dialogueDtoAdapter;
            _messageBoxService = messageBoxService;
        }

        private List<DialogueDto> Dialogues()
        {
            if (_dialogue == null)
            {
                List<Dialogue> dialogues = new();
                dialogues.AddRange(_dialogueDataReader.Data);
                dialogues.AddRange(new DefaultDialogue());
                _dialogue = dialogues
                    .Select(dialogue => 
                    {
                        try
                        {
                            return _dialogueDtoAdapter.Adapt(dialogue);
                        }
                        catch (ArgumentException e)
                        {
                            _messageBoxService.ShowCustomSpawnsErrorMessage(e, "reading dialogue data");
                            return null;
                        }
                    })
                    .Where(dialogue => dialogue != null)
                    .Aggregate((allDialoguesDtos, currentDialogueDtos) =>
                    {
                        allDialoguesDtos!.AddRange(currentDialogueDtos!);
                        return allDialoguesDtos;
                    })!
                    .ToList();
            }
            return _dialogue;
        }

        public IList<DialogueDto> FindAll()
        {
            return Dialogues();
        }
    }
}