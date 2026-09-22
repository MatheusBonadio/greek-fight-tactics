using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game.UI
{
    public sealed class ArenaVeilView : MonoBehaviour
    {
        public const float MaximumDensity = 0.5f;
        public const float DefaultFadeOutSeconds = 0.4f;
        public const string DensityProperty = "_Density";
        public const string OverlayProperty = "_Overlay";
        public const string PixelsPerUnitProperty = "_PixelsPerUnit";

        private const float OverlayMode = 1f;
        private const float SpriteMode = 0f;

        [SerializeField] private Tilemap floorVeil;
        [SerializeField] private Tilemap blocksVeil;
        [SerializeField] private Tilemap blocks;
        [SerializeField] private TileBase floorTile;
        [SerializeField] private Shader shader;
        [SerializeField] private float fadeOutSeconds = DefaultFadeOutSeconds;
        [SerializeField] private int pixelsPerUnit = PixelSnappedCamera.DefaultPixelsPerUnit;

        private readonly Dictionary<SpriteRenderer, Material> veiledActors = new();
        private Material overlayMaterial;
        private Material actorMaterial;
        private float fadeFrom;
        private float fadeTo;
        private float fadeSeconds;
        private float fadeElapsed;
        private bool fading;
        private bool hiding;

        public bool IsShown { get; private set; }

        public float Density { get; private set; }

        public int VeiledCellCount { get; private set; }

        public int VeiledBlockCount { get; private set; }

        public IReadOnlyCollection<SpriteRenderer> VeiledActors => veiledActors.Keys;

        public Tilemap FloorVeil => floorVeil;

        public Tilemap BlocksVeil => blocksVeil;

        public Material ActorMaterial => actorMaterial;

        public Material OverlayMaterial => overlayMaterial;

        public void Configure(Tilemap floorVeilTilemap, Tilemap blocksVeilTilemap, Tilemap blocksTilemap, TileBase floorVeilTile, Shader veilShader)
        {
            floorVeil = floorVeilTilemap;
            blocksVeil = blocksVeilTilemap;
            blocks = blocksTilemap;
            floorTile = floorVeilTile;
            shader = veilShader;
        }

        public void Show(int width, int height, Func<Coord, bool> isLit, IEnumerable<SpriteRenderer> outsideActors, float fadeInSeconds)
        {
            if (isLit == null)
            {
                throw new ArgumentNullException(nameof(isLit));
            }

            EnsureMaterials();
            Clear();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (!isLit(new Coord(x, y)))
                    {
                        VeilCell(new Vector3Int(x, y, 0));
                    }
                }
            }

            foreach (var actor in outsideActors ?? Array.Empty<SpriteRenderer>())
            {
                if (actor && !veiledActors.ContainsKey(actor))
                {
                    veiledActors[actor] = actor.sharedMaterial;
                    actor.sharedMaterial = actorMaterial;
                }
            }

            IsShown = true;
            FadeTo(MaximumDensity, fadeInSeconds);
        }

        public void Hide()
        {
            if (!IsShown)
            {
                return;
            }

            hiding = true;
            FadeTo(0f, fadeOutSeconds);
        }

        public void Clear()
        {
            if (floorVeil)
            {
                floorVeil.ClearAllTiles();
            }

            if (blocksVeil)
            {
                blocksVeil.ClearAllTiles();
            }

            foreach (var (actor, material) in veiledActors)
            {
                if (actor)
                {
                    actor.sharedMaterial = material;
                }
            }

            veiledActors.Clear();
            VeiledCellCount = 0;
            VeiledBlockCount = 0;
            IsShown = false;
            fading = false;
            hiding = false;
            ApplyDensity(0f);
        }

        private void Awake() => EnsureMaterials();

        private void OnDestroy()
        {
            Clear();
            Destroy(overlayMaterial);
            Destroy(actorMaterial);
        }

        private void Update() => StepFade(Time.deltaTime);

        private void StepFade(float deltaSeconds)
        {
            if (!fading)
            {
                return;
            }

            fadeElapsed += deltaSeconds;
            var progress = fadeSeconds <= 0f ? 1f : Mathf.Clamp01(fadeElapsed / fadeSeconds);
            ApplyDensity(Mathf.Lerp(fadeFrom, fadeTo, progress));
            if (progress < 1f)
            {
                return;
            }

            fading = false;
            if (hiding)
            {
                Clear();
            }
        }

        private void VeilCell(Vector3Int position)
        {
            if (floorVeil && floorTile)
            {
                floorVeil.SetTile(position, floorTile);
                VeiledCellCount++;
            }

            var block = blocks ? blocks.GetTile(position) : null;
            if (blocksVeil && block)
            {
                blocksVeil.SetTile(position, block);
                VeiledBlockCount++;
            }
        }

        private void FadeTo(float density, float seconds)
        {
            fadeFrom = Density;
            fadeTo = Mathf.Clamp(density, 0f, MaximumDensity);
            fadeSeconds = Mathf.Max(0f, seconds);
            fadeElapsed = 0f;
            fading = true;
            if (fadeSeconds <= 0f)
            {
                StepFade(0f);
            }
        }

        private void ApplyDensity(float density)
        {
            Density = density;
            if (overlayMaterial)
            {
                overlayMaterial.SetFloat(DensityProperty, density);
            }

            if (actorMaterial)
            {
                actorMaterial.SetFloat(DensityProperty, density);
            }
        }

        private void EnsureMaterials()
        {
            if (!shader || overlayMaterial)
            {
                return;
            }

            overlayMaterial = CreateMaterial(OverlayMode);
            actorMaterial = CreateMaterial(SpriteMode);
            foreach (var tilemap in new[] { floorVeil, blocksVeil })
            {
                if (tilemap && tilemap.TryGetComponent<TilemapRenderer>(out var renderer))
                {
                    renderer.sharedMaterial = overlayMaterial;
                }
            }

            ApplyDensity(Density);
        }

        private Material CreateMaterial(float overlay)
        {
            var material = new Material(shader) { name = overlay > SpriteMode ? "ArenaVeilOverlay" : "ArenaVeilSprite" };
            material.SetFloat(OverlayProperty, overlay);
            material.SetFloat(PixelsPerUnitProperty, pixelsPerUnit);
            return material;
        }
    }
}
