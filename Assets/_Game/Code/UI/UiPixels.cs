using UnityEngine;

namespace Game.UI
{
    public static class UiPixels
    {
        private const float SnapTolerance = 0.001f;

        public static float Snap(float ratio, int pixels) =>
            pixels <= 0 ? 0f : Mathf.Clamp01(Mathf.Floor(ratio * pixels + SnapTolerance) / pixels);
    }
}
