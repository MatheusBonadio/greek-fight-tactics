using System;
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    [Serializable]
    public sealed class DialogueTriggerLink
    {
        [SerializeField] private Vector2Int cell;
        [SerializeField] private string dialogue;
        [SerializeField] private string flag;

        public DialogueTriggerLink()
        {
        }

        public DialogueTriggerLink(Coord triggerCell, string dialogueId, string onceFlag)
        {
            cell = new Vector2Int(triggerCell.X, triggerCell.Y);
            dialogue = dialogueId;
            flag = onceFlag;
        }

        public Coord Cell => new(cell.x, cell.y);

        public string Dialogue => dialogue;

        public string Flag => flag;
    }
}
