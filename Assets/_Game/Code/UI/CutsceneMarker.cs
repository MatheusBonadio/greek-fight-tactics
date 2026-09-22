using Game.Core;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Game.UI
{
    public sealed class CutsceneMarker : Marker, INotification, INotificationOptionProvider
    {
        [SerializeField] private CutsceneAction action;
        [SerializeField] private string actor;
        [SerializeField] private string toward;
        [SerializeField] private bool hasCell;
        [SerializeField] private Vector2Int cell;
        [SerializeField] private string dialogue;

        public PropertyName id => new(nameof(CutsceneMarker));

        public NotificationFlags flags => NotificationFlags.TriggerOnce;

        public CutsceneAction Action => action;

        public string Actor => actor;

        public string Toward => toward;

        public bool HasCell => hasCell;

        public Coord Cell => new(cell.x, cell.y);

        public string Dialogue => dialogue;

        public void Configure(CutsceneAction stepAction, string actorId, string towardActorId, Coord? targetCell, string dialogueId)
        {
            action = stepAction;
            actor = actorId ?? string.Empty;
            toward = towardActorId ?? string.Empty;
            hasCell = targetCell.HasValue;
            cell = targetCell.HasValue ? new Vector2Int(targetCell.Value.X, targetCell.Value.Y) : Vector2Int.zero;
            dialogue = dialogueId ?? string.Empty;
        }
    }
}
