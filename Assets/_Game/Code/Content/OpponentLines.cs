using System;
using Game.Battle;
using UnityEngine;

namespace Game.Content
{
    [Serializable]
    public sealed class OpponentLines
    {
        [SerializeField] private string intro;
        [SerializeField] private string victory;
        [SerializeField] private string defeat;

        public OpponentLines()
        {
        }

        public OpponentLines(string introDialogue, string victoryDialogue, string defeatDialogue)
        {
            intro = introDialogue;
            victory = victoryDialogue;
            defeat = defeatDialogue;
        }

        public string Intro => intro;

        public string Victory => victory;

        public string Defeat => defeat;

        public bool IsDefined =>
            !string.IsNullOrEmpty(intro) || !string.IsNullOrEmpty(victory) || !string.IsNullOrEmpty(defeat);

        public string DialogueFor(BattleStatus status) => status switch
        {
            BattleStatus.PlayerWon => victory,
            BattleStatus.PlayerLost => defeat,
            _ => null
        };
    }
}
