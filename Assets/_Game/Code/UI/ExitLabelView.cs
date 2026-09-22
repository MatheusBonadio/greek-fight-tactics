using Game.Grid;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public sealed class ExitLabelView : MonoBehaviour
    {
        public const float LiftAboveSurface = 1.25f;
        public const float Padding = 3f;

        [SerializeField] private GridSandboxController sandbox;
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text label;

        public bool IsShown => panel && panel.gameObject.activeSelf;

        public string Text => label ? label.text : null;

        public MapExitLink Shown { get; private set; }

        public RectTransform Panel => panel;

        public void Configure(GridSandboxController controller, RectTransform canvas, RectTransform labelPanel, TMP_Text labelText)
        {
            sandbox = controller;
            canvasRect = canvas;
            panel = labelPanel;
            label = labelText;
            if (panel)
            {
                panel.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            var exit = sandbox ? sandbox.HoveredExit : null;
            var camera = Camera.main;
            Shown = exit != null && !string.IsNullOrEmpty(exit.Label) && camera && panel && label ? exit : null;
            if (panel)
            {
                panel.gameObject.SetActive(Shown != null);
            }

            if (Shown == null)
            {
                return;
            }

            label.text = Shown.Label;
            var size = label.GetPreferredValues(Shown.Label);
            panel.sizeDelta = new Vector2(Mathf.Ceil(size.x) + 2f * Padding, Mathf.Ceil(size.y) + 2f * Padding);

            var surface = IsoMath.GridToSurface(Shown.Cell, sandbox.Grid[Shown.Cell].Level) + new Vector2(0f, LiftAboveSurface);
            var screen = camera.WorldToScreenPoint(surface);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local))
            {
                panel.anchoredPosition = new Vector2(Mathf.Round(local.x), Mathf.Round(local.y));
            }
        }
    }
}
