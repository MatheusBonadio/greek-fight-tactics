using System;
using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Core;
using UnityEngine;

namespace Game.Content
{
    [Serializable]
    public sealed class OpponentPhase
    {
        [SerializeField] private string id;
        [SerializeField] private ActionId counter = ActionId.Refute;
        [SerializeField] private int untilCoherence;
        [SerializeField] private string dialogue;
        [SerializeField] private string music;
        [SerializeField] private Posture posture = Posture.Idle;
        [SerializeField] private Color tint = Color.white;
        [SerializeField] private List<ArgumentEntry> arguments = new();

        public OpponentPhase()
        {
        }

        public OpponentPhase(string phaseId, ActionId counterAction, int floor, string dialogueId, string musicId, Posture phasePosture, Color phaseTint, IEnumerable<Argument> phaseArguments)
        {
            id = phaseId;
            counter = counterAction;
            untilCoherence = floor;
            dialogue = dialogueId;
            music = musicId;
            posture = phasePosture;
            tint = phaseTint;
            arguments = (phaseArguments ?? Enumerable.Empty<Argument>()).Select(argument => ArgumentEntry.From(argument)).ToList();
        }

        public string Id => id;

        public ActionId Counter => counter;

        public int UntilCoherence => untilCoherence;

        public string Dialogue => dialogue;

        public string Music => music;

        public Posture Posture => posture;

        public Color Tint => tint;

        public IReadOnlyList<Argument> Arguments => (arguments ?? new List<ArgumentEntry>()).Select(entry => entry.ToArgument()).ToList();

        public DuelPhase ToPhase() => new(id, counter, untilCoherence, Arguments);
    }

    [Serializable]
    public sealed class OpponentPhases
    {
        [SerializeField] private OpponentLines lines = new();
        [SerializeField] private List<OpponentPhase> phases = new();

        public OpponentPhases()
        {
        }

        public OpponentPhases(string introDialogue, string victoryDialogue, string defeatDialogue, IEnumerable<OpponentPhase> duelPhases)
        {
            lines = new OpponentLines(introDialogue, victoryDialogue, defeatDialogue);
            phases = (duelPhases ?? Enumerable.Empty<OpponentPhase>()).ToList();
        }

        public OpponentLines Lines => lines ?? new OpponentLines();

        public string Intro => Lines.Intro;

        public string Victory => Lines.Victory;

        public string Defeat => Lines.Defeat;

        public IReadOnlyList<OpponentPhase> Phases => phases ?? new List<OpponentPhase>();

        public bool IsDefined => phases != null && phases.Count > 0;

        public string DialogueFor(BattleStatus status) => Lines.DialogueFor(status);

        public PhasedDuel ToDuel() => IsDefined ? new PhasedDuel(phases.Select(phase => phase.ToPhase())) : null;
    }
}
