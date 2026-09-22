using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class JurorMeterView : MonoBehaviour
    {
        [SerializeField] private RawImage image;
        [SerializeField] private Color trackColor = new Color32(0, 0, 0, 255);
        [SerializeField] private Color acquittalColor = new Color32(72, 200, 224, 255);
        [SerializeField] private Color condemnationColor = new Color32(192, 56, 40, 255);
        [SerializeField] private Color menexenoColor = new Color32(248, 216, 48, 255);

        private Texture2D texture;
        private int painted;
        private int acquittalsPainted;
        private int condemnationsPainted;

        public JurorCount Count { get; private set; }

        public Texture2D Texture => texture;

        public Vector2Int? MenexenoPixel { get; private set; }

        public Color TrackColor => trackColor;

        public Color AcquittalColor => acquittalColor;

        public Color CondemnationColor => condemnationColor;

        public Color MenexenoColor => menexenoColor;

        public void Configure(RawImage target)
        {
            image = target;
        }

        public void Bind(JurorCount count)
        {
            Count = count;
            EnsureTexture();
            Clear();
            painted = 0;
            acquittalsPainted = 0;
            condemnationsPainted = 0;
            MenexenoPixel = null;
            PaintRevealed();
        }

        public void Advance(float deltaSeconds)
        {
            if (Count != null && Count.Tick(deltaSeconds) > 0)
            {
                PaintRevealed();
            }
        }

        public void SkipToEnd()
        {
            if (Count == null)
            {
                return;
            }

            Count.SkipToEnd();
            PaintRevealed();
        }

        public Color PixelAt(int x, int y) => texture.GetPixel(x, y);

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            if (!texture)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(texture);
            }
            else
            {
                DestroyImmediate(texture);
            }
        }

        private void PaintRevealed()
        {
            if (Count == null)
            {
                return;
            }

            while (painted < Count.Revealed)
            {
                var acquits = Count.VoteAt(painted);
                var pixel = acquits ? JurorLayout.AcquittalPixel(acquittalsPainted++) : JurorLayout.CondemnationPixel(condemnationsPainted++);
                var isMenexeno = painted == JurorCount.MenexenoIndex;
                texture.SetPixel(pixel.x, pixel.y, isMenexeno ? menexenoColor : acquits ? acquittalColor : condemnationColor);
                if (isMenexeno)
                {
                    MenexenoPixel = pixel;
                    PaintMarker(pixel.x);
                }

                painted++;
            }

            texture.Apply(false);
        }

        private void PaintMarker(int x)
        {
            for (var offset = 0; offset < JurorLayout.MarkerLength; offset++)
            {
                texture.SetPixel(x, JurorLayout.MarkerLowY + offset, menexenoColor);
                texture.SetPixel(x, JurorLayout.MarkerHighY - offset, menexenoColor);
            }
        }

        private void EnsureTexture()
        {
            if (!texture)
            {
                texture = new Texture2D(JurorLayout.Width, JurorLayout.Height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
            }

            if (image)
            {
                image.texture = texture;
            }
        }

        private void Clear()
        {
            var pixels = new Color32[JurorLayout.Width * JurorLayout.Height];
            Color32 track = trackColor;
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = track;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
        }
    }
}
