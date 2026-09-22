using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    [RequireComponent(typeof(Image))]
    public sealed class SpriteFlipbook : MonoBehaviour
    {
        public const float DefaultFramesPerSecond = 6f;

        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
        [SerializeField] private float framesPerSecond = DefaultFramesPerSecond;

        private Image image;
        private float elapsed;

        public IReadOnlyList<Sprite> Frames => frames;

        public float FramesPerSecond => framesPerSecond;

        public int Index { get; private set; }

        public void Configure(Sprite[] played, float speed)
        {
            frames = played ?? Array.Empty<Sprite>();
            framesPerSecond = speed;
            Show(0);
        }

        public void Advance(float seconds)
        {
            if (frames.Length < 2 || framesPerSecond <= 0f)
            {
                return;
            }

            var step = 1f / framesPerSecond;
            elapsed += seconds;
            while (elapsed >= step)
            {
                elapsed -= step;
                Show((Index + 1) % frames.Length);
            }
        }

        private void Awake()
        {
            Show(Index);
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
        }

        private void Show(int index)
        {
            Index = index;
            if (index >= frames.Length || !frames[index])
            {
                return;
            }

            image = image ? image : GetComponent<Image>();
            if (image)
            {
                image.sprite = frames[index];
            }
        }
    }
}
