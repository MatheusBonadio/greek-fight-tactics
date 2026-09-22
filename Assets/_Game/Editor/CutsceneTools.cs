using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Game.EditorTools
{
    public static class CutsceneTools
    {
        public const string CutsceneFolder = "Assets/_Game/Data/Cutscenes";
        public const double TailSeconds = 0.5d;

        public static string TimelinePathFor(string mapId, string cutsceneId) => $"{CutsceneFolder}/{mapId}/{cutsceneId}.playable";

        public static CutsceneDirector CreateDirector(GridSandboxController controller, ExplorationDialogueDirector dialogues, MapLayout map, IReadOnlyList<ActorView> actors)
        {
            if (map.Cutscenes.Count == 0)
            {
                return null;
            }

            var timelines = map.Cutscenes.ToDictionary(cutscene => cutscene.Id, cutscene => BuildTimeline(TimelinePathFor(map.Id, cutscene.Id), cutscene));
            var cast = map.Cutscenes
                .SelectMany(cutscene => cutscene.ActorIds)
                .Distinct()
                .ToDictionary(id => id, id => actors[map.Actors.FindIndex(actor => actor.Character == id)]);

            var directorObject = new GameObject(nameof(CutsceneDirector));
            var playable = directorObject.AddComponent<PlayableDirector>();
            playable.playOnAwake = false;
            playable.extrapolationMode = DirectorWrapMode.None;
            playable.timeUpdateMode = DirectorUpdateMode.GameTime;

            var director = directorObject.AddComponent<CutsceneDirector>();
            director.Configure(controller, dialogues, playable, timelines, cast);
            dialogues.ConfigureCutscenes(director);
            return director;
        }

        public static TimelineAsset BuildTimeline(string path, MapCutscene cutscene)
        {
            AssetFolders.Ensure(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            if (!timeline)
            {
                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                AssetDatabase.CreateAsset(timeline, path);
            }

            timeline.CreateMarkerTrack();
            var track = timeline.markerTrack;
            var markers = track.GetMarkers().OfType<CutsceneMarker>().OrderBy(marker => marker.time).ToList();
            if (markers.Count != cutscene.Steps.Count || track.GetMarkerCount() != markers.Count)
            {
                foreach (var stale in track.GetMarkers().ToList())
                {
                    track.DeleteMarker(stale);
                }

                markers = cutscene.Steps.Select(step => track.CreateMarker<CutsceneMarker>(step.At)).ToList();
            }

            for (var index = 0; index < markers.Count; index++)
            {
                var step = cutscene.Steps[index];
                var marker = markers[index];
                marker.time = step.At;
                marker.name = $"{index:00}_{step.Action}";
                marker.Configure(step.Action, step.Actor, step.Toward, step.Cell, step.Dialogue);
                EditorUtility.SetDirty(marker);
            }

            timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            timeline.fixedDuration = cutscene.LastStepAt + TailSeconds;
            EditorUtility.SetDirty(track);
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
            return timeline;
        }
    }
}
