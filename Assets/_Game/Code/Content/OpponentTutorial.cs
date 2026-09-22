using System;
using System.Collections.Generic;
using Game.Battle;
using Game.Core;
using UnityEngine;

namespace Game.Content
{
    [Serializable]
    public sealed class OpponentTutorial
    {
        [SerializeField] private string fallacyId;
        [SerializeField] private string doneFlag;
        [SerializeField] private Dictionary<TutorialCue, string> dialogues = new();

        public OpponentTutorial()
        {
        }

        public OpponentTutorial(string fallacy, string flag, IReadOnlyDictionary<TutorialCue, string> cueDialogues)
        {
            fallacyId = fallacy;
            doneFlag = flag;
            dialogues = cueDialogues == null ? new Dictionary<TutorialCue, string>() : new Dictionary<TutorialCue, string>(cueDialogues);
        }

        public string FallacyId => fallacyId;

        public string DoneFlag => doneFlag;

        public IReadOnlyDictionary<TutorialCue, string> Dialogues => dialogues;

        public bool IsDefined => !string.IsNullOrEmpty(fallacyId) && SaveFlags.IsCustomName(doneFlag);

        public bool IsPending(SaveData story) => IsDefined && story != null && !StoryCondition.Has(doneFlag).IsMet(story);

        public string DialogueFor(TutorialCue cue) => dialogues != null && dialogues.TryGetValue(cue, out var id) ? id : null;
    }
}
