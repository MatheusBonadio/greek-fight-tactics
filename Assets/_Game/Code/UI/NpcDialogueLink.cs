using System;
using UnityEngine;

namespace Game.UI
{
    [Serializable]
    public sealed class NpcDialogueLink
    {
        [SerializeField] private ActorView actor;
        [SerializeField] private string dialogue;

        public NpcDialogueLink()
        {
        }

        public NpcDialogueLink(ActorView npc, string dialogueId)
        {
            actor = npc;
            dialogue = dialogueId;
        }

        public ActorView Actor => actor;

        public string Dialogue => dialogue;
    }
}
