using System;
using System.Collections.Generic;
using Game.Core;
using Game.Grid;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Game.UI
{
    [RequireComponent(typeof(PlayableDirector))]
    public sealed class CutsceneDirector : MonoBehaviour, INotificationReceiver
    {
        [SerializeField] private GridSandboxController sandbox;
        [SerializeField] private ExplorationDialogueDirector dialogues;
        [SerializeField] private PlayableDirector director;
        [SerializeField] private Dictionary<string, TimelineAsset> cutscenes = new();
        [SerializeField] private Dictionary<string, ActorView> cast = new();

        private readonly Queue<CutsceneMarker> pending = new();
        private readonly PathFinder pathFinder = new();
        private readonly List<Coord> path = new();
        private PauseMenuController pause;
        private ActorView walking;
        private bool talking;
        private bool timelineEnded;

        public event Action<string> Finished;

        public IReadOnlyDictionary<string, TimelineAsset> Cutscenes => cutscenes;

        public IReadOnlyDictionary<string, ActorView> Cast => cast;

        public PlayableDirector Director => director;

        public string ActiveCutscene { get; private set; }

        public bool IsPlaying => ActiveCutscene != null;

        public void Configure(
            GridSandboxController controller,
            ExplorationDialogueDirector dialogueDirector,
            PlayableDirector playableDirector,
            Dictionary<string, TimelineAsset> timelines,
            Dictionary<string, ActorView> actors)
        {
            sandbox = controller;
            dialogues = dialogueDirector;
            director = playableDirector;
            cutscenes = timelines ?? new Dictionary<string, TimelineAsset>();
            cast = actors ?? new Dictionary<string, ActorView>();
        }

        public bool Contains(string cutsceneId) =>
            !string.IsNullOrEmpty(cutsceneId) && cutscenes.TryGetValue(cutsceneId, out var timeline) && timeline;

        public bool TryPlay(string cutsceneId)
        {
            if (IsPlaying || !Contains(cutsceneId) || sandbox.StateMachine.Mode != GameMode.Exploration || !sandbox.StateMachine.TryOpenOverlay(GameMode.Cutscene))
            {
                return false;
            }

            ActiveCutscene = cutsceneId;
            pending.Clear();
            walking = null;
            talking = false;
            timelineEnded = false;
            sandbox.Player?.Walker?.Cancel();
            if (pause)
            {
                pause.Input.Panel = null;
            }

            director.playableAsset = cutscenes[cutsceneId];
            director.extrapolationMode = DirectorWrapMode.None;
            director.time = 0d;
            director.Play();
            return true;
        }

        public void OnNotify(Playable origin, INotification notification, object context)
        {
            if (IsPlaying && notification is CutsceneMarker marker)
            {
                pending.Enqueue(marker);
            }
        }

        private void Awake()
        {
            pause = FindAnyObjectByType<PauseMenuController>();
        }

        private void OnEnable()
        {
            if (director)
            {
                director.stopped += OnTimelineStopped;
            }
        }

        private void OnDisable()
        {
            if (director)
            {
                director.stopped -= OnTimelineStopped;
            }
        }

        private void OnTimelineStopped(PlayableDirector stopped)
        {
            if (IsPlaying)
            {
                timelineEnded = true;
            }
        }

        private void Update()
        {
            if (!IsPlaying || IsWaiting())
            {
                return;
            }

            while (pending.Count > 0)
            {
                if (Run(pending.Dequeue()))
                {
                    director.Pause();
                    return;
                }
            }

            if (timelineEnded)
            {
                Finish();
                return;
            }

            if (director.state != PlayState.Playing)
            {
                director.Resume();
            }
        }

        private bool IsWaiting()
        {
            if (talking)
            {
                if (dialogues.IsTalking)
                {
                    return true;
                }

                talking = false;
            }

            if (walking)
            {
                if (walking.Walker != null && walking.Walker.IsMoving)
                {
                    return true;
                }

                walking = null;
            }

            return false;
        }

        private bool Run(CutsceneMarker marker)
        {
            switch (marker.Action)
            {
                case CutsceneAction.Face:
                    if (TryFind(marker.Actor, out var facing) && TryTarget(marker, out var target))
                    {
                        FaceToward(facing.Walker, target);
                    }

                    return false;
                case CutsceneAction.Dialogue:
                    talking = dialogues && dialogues.TryStartDialogue(marker.Dialogue);
                    return talking;
                case CutsceneAction.Walk:
                    if (TryFind(marker.Actor, out var walker) && pathFinder.FindPath(sandbox.Grid, walker.Walker.Cell, marker.Cell, path) && path.Count > 0)
                    {
                        walker.Walker.Follow(path);
                        walking = walker;
                        return true;
                    }

                    return false;
                case CutsceneAction.Leave:
                    if (TryFind(marker.Actor, out var leaving))
                    {
                        sandbox.TryDismiss(leaving);
                    }

                    return false;
                default:
                    return false;
            }
        }

        private bool TryFind(string actorId, out ActorView actor) =>
            cast.TryGetValue(actorId ?? string.Empty, out actor) && actor && actor.Walker != null;

        private bool TryTarget(CutsceneMarker marker, out Coord target)
        {
            if (TryFind(marker.Toward, out var other))
            {
                target = other.Walker.Cell;
                return true;
            }

            target = marker.Cell;
            return marker.HasCell;
        }

        private static void FaceToward(GridWalker walker, Coord target)
        {
            var deltaX = target.X - walker.Cell.X;
            var deltaY = target.Y - walker.Cell.Y;
            if (deltaX == 0 && deltaY == 0)
            {
                return;
            }

            walker.FaceToward(Math.Abs(deltaX) >= Math.Abs(deltaY)
                ? new Coord(walker.Cell.X + Math.Sign(deltaX), walker.Cell.Y)
                : new Coord(walker.Cell.X, walker.Cell.Y + Math.Sign(deltaY)));
        }

        private void Finish()
        {
            var finished = ActiveCutscene;
            ActiveCutscene = null;
            pending.Clear();
            director.Stop();

            var from = sandbox.StateMachine.Mode;
            sandbox.StateMachine.TryCloseOverlay();
            if (pause)
            {
                pause.Input.Panel = pause;
            }

            if (dialogues)
            {
                dialogues.RefreshMap();
            }

            if (dialogues && AutosavePolicy.ShouldSave(from, sandbox.StateMachine.Mode))
            {
                dialogues.SaveProgress();
            }

            Finished?.Invoke(finished);
        }
    }
}
