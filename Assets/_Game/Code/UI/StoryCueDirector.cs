using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    public sealed class StoryCueDirector : MonoBehaviour
    {
        [SerializeField] private GridSandboxController sandbox;
        [SerializeField] private ExplorationDialogueDirector dialogues;
        [SerializeField] private List<StoryCueLink> cues = new();

        private StoryCueLink pendingFog;
        private bool walking;

        public IReadOnlyList<StoryCueLink> Cues => cues;

        public string LastPlayedFlag { get; private set; }

        public void Configure(GridSandboxController controller, ExplorationDialogueDirector director, List<StoryCueLink> links)
        {
            sandbox = controller;
            dialogues = director;
            cues = links ?? new List<StoryCueLink>();
        }

        private void Start()
        {
            sandbox.FogRevealed += OnFogRevealed;
        }

        private void OnDestroy()
        {
            if (sandbox)
            {
                sandbox.FogRevealed -= OnFogRevealed;
            }
        }

        private void OnFogRevealed(int revealed)
        {
            if (pendingFog != null || !IsQuiet())
            {
                return;
            }

            pendingFog = Due(ExplorationCue.FogRevealed);
            if (pendingFog != null)
            {
                sandbox.Player.Walker.Cancel();
            }
        }

        private void Update()
        {
            var walker = sandbox.Player?.Walker;
            if (walker == null || !IsQuiet())
            {
                walking = false;
                return;
            }

            if (walker.IsMoving)
            {
                walking = true;
                return;
            }

            if (pendingFog != null)
            {
                var fog = pendingFog;
                pendingFog = null;
                walking = false;
                Play(fog);
                return;
            }

            if (walking)
            {
                walking = false;
                if (TryPlay(ExplorationCue.WalkFinished))
                {
                    return;
                }
            }

            if (!sandbox.ArrivedThroughExit)
            {
                TryPlay(ExplorationCue.MapStart);
            }
        }

        private bool IsQuiet() =>
            dialogues && dialogues.IsStoryLoaded && !dialogues.IsTalking && !dialogues.IsApproaching && !dialogues.HasPendingTrigger
            && sandbox.ProgressLoaded && sandbox.ExplorationInputEnabled && sandbox.StateMachine.Mode == GameMode.Exploration;

        private StoryCueLink Due(ExplorationCue cue) => cues.Find(link => link.IsDue(cue, dialogues.Story));

        private bool TryPlay(ExplorationCue cue)
        {
            var link = Due(cue);
            return link != null && Play(link);
        }

        private bool Play(StoryCueLink link)
        {
            if (!link.IsDue(link.On, dialogues.Story) || !dialogues.TryPlayOnce(link.Dialogue, link.Flag))
            {
                return false;
            }

            LastPlayedFlag = link.Flag;
            return true;
        }
    }
}
