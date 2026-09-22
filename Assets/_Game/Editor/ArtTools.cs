using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Game.EditorTools
{
    public static class ArtTools
    {
        public const string SourceFolder = "art/placeholders";
        public const string PlaceholderFolder = "Assets/_Game/Art/Placeholder";
        public const string CharacterFolder = PlaceholderFolder + "/characters";
        public const string TileFolder = PlaceholderFolder + "/tiles";
        public const string PortraitFolder = PlaceholderFolder + "/portraits";
        public const string PortraitSuffix = "_portrait.png";
        public const int PortraitSize = 32;
        public const string AnimationFolder = PlaceholderFolder + "/Animations";
        public const int CellWidth = 24;
        public const int CellHeight = 32;
        public const int SheetColumns = 4;
        public const int SheetRows = 4;
        public const string DirectionXParameter = "dirX";
        public const string DirectionYParameter = "dirY";
        public const string IdleState = "idle";
        public const string WalkState = "walk";
        public const string ActionState = "action";
        public const float WalkSecondsPerFrame = 0.15f;
        public const float IdleSecondsPerFrame = 0.6f;
        public const float ActionSecondsPerFrame = 0.3f;

        public static readonly Vector2 CharacterPivot = new(0.5f, 0.0625f);
        public static readonly string[] Characters = { "aristeu", "batilo", "citizen", "damaris", "eudoro", "kleia", "kritolaos", "menexeno", "oron", "phaidra", "pythia", "simo", "thrasybios" };
        public static readonly string[] StateNames = { IdleState, WalkState, ActionState };

        private const string SpriteProperty = "m_Sprite";
        private const int IdleColumn = 0;
        private const int FirstStepColumn = 1;
        private const int SecondStepColumn = 2;
        private const int ActionColumn = 3;

        private static readonly SheetDirection[] Directions =
        {
            new("se", 0, new Vector2(1f, -1f)),
            new("sw", 1, new Vector2(-1f, -1f)),
            new("ne", 2, new Vector2(1f, 1f)),
            new("nw", 3, new Vector2(-1f, 1f))
        };

        public static void SliceSheets() => BatchRunner.Run(RegenerateAll);

        public static string SheetPathFor(string character) => $"{CharacterFolder}/{character}_sheet.png";

        public static string PortraitPathFor(string character) => $"{PortraitFolder}/{character}{PortraitSuffix}";

        public static string ControllerPathFor(string character) => $"{AnimationFolder}/{character}/{character}.controller";

        public static void RegenerateAll()
        {
            var seams = FloorTileSeams.ApplyToSourceFloors();
            if (seams > 0)
            {
                Debug.Log($"[ArtTools] removed the bottom outline from {seams} floor tiles");
            }

            var blockSeams = BlockTileSeams.ApplyToSourceBlocks();
            if (blockSeams > 0)
            {
                Debug.Log($"[ArtTools] removed the right outline from {blockSeams} block tiles");
            }

            var copied = CopyPlaceholders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(PlaceholderFolder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            foreach (var character in Characters)
            {
                SliceSheet(character);
            }

            foreach (var character in Characters)
            {
                BuildAnimations(character);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ArtTools] copied {copied} placeholders, sliced and animated {Characters.Length} characters");
        }

        private static int CopyPlaceholders()
        {
            var copied = 0;
            foreach (var source in Directory.GetFiles(SourceFolder, "*.png", SearchOption.AllDirectories))
            {
                var relative = source.Substring(SourceFolder.Length).TrimStart('\\', '/');
                var destination = Path.Combine(PlaceholderFolder, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? PlaceholderFolder);

                if (File.Exists(destination) && File.ReadAllBytes(destination).SequenceEqual(File.ReadAllBytes(source)))
                {
                    continue;
                }

                File.Copy(source, destination, true);
                copied++;
            }

            return copied;
        }

        private static void SliceSheet(string character)
        {
            var path = SheetPathFor(character);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter ?? throw new InvalidOperationException($"missing sheet {path}");

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var existingIds = provider.GetSpriteRects().ToDictionary(rect => rect.name, rect => rect.spriteID);
            var rects = new List<SpriteRect>();
            foreach (var direction in Directions)
            {
                for (var column = 0; column < SheetColumns; column++)
                {
                    var name = SpriteName(character, direction, column);
                    rects.Add(new SpriteRect
                    {
                        name = name,
                        rect = new Rect(column * CellWidth, (SheetRows - 1 - direction.Row) * CellHeight, CellWidth, CellHeight),
                        alignment = SpriteAlignment.Custom,
                        pivot = CharacterPivot,
                        spriteID = existingIds.TryGetValue(name, out var id) ? id : GUID.Generate()
                    });
                }
            }

            provider.SetSpriteRects(rects.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
                ?.SetNameFileIdPairs(rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
        }

        private static void BuildAnimations(string character)
        {
            var folder = $"{AnimationFolder}/{character}";
            AssetFolders.Ensure(folder);

            var sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPathFor(character))
                .OfType<Sprite>()
                .ToDictionary(sprite => sprite.name);

            var motions = new Dictionary<string, List<(AnimationClip Clip, Vector2 Position)>>();
            foreach (var state in StateNames)
            {
                motions[state] = Directions
                    .Select(direction => (BuildClip(folder, character, state, direction, sprites), direction.BlendPosition))
                    .ToList();
            }

            var controllerPath = ControllerPathFor(character);
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            {
                return;
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter(DirectionXParameter, AnimatorControllerParameterType.Float);
            controller.AddParameter(DirectionYParameter, AnimatorControllerParameterType.Float);

            foreach (var state in StateNames)
            {
                controller.CreateBlendTreeInController(state, out var tree, 0);
                tree.blendType = BlendTreeType.SimpleDirectional2D;
                tree.blendParameter = DirectionXParameter;
                tree.blendParameterY = DirectionYParameter;
                foreach (var (clip, position) in motions[state])
                {
                    tree.AddChild(clip, position);
                }
            }

            EditorUtility.SetDirty(controller);
        }

        private static AnimationClip BuildClip(string folder, string character, string state, SheetDirection direction, IReadOnlyDictionary<string, Sprite> sprites)
        {
            var (columns, secondsPerFrame, loop) = state switch
            {
                WalkState => (new[] { FirstStepColumn, IdleColumn, SecondStepColumn, IdleColumn }, WalkSecondsPerFrame, true),
                ActionState => (new[] { ActionColumn }, ActionSecondsPerFrame, false),
                _ => (new[] { IdleColumn }, IdleSecondsPerFrame, true)
            };

            var frames = columns.Select(column => sprites[SpriteName(character, direction, column)]).ToArray();
            var path = $"{folder}/{character}_{state}_{direction.Suffix}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.ClearCurves();
            clip.frameRate = 1f / WalkSecondsPerFrame * 4f;

            var keyframes = frames
                .Select((sprite, index) => new ObjectReferenceKeyframe { time = index * secondsPerFrame, value = sprite })
                .Append(new ObjectReferenceKeyframe { time = frames.Length * secondsPerFrame, value = frames[^1] })
                .ToArray();
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), SpriteProperty), keyframes);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static string SpriteName(string character, SheetDirection direction, int column) =>
            $"{character}_{direction.Suffix}_{column}";

        private readonly struct SheetDirection
        {
            public readonly string Suffix;
            public readonly int Row;
            public readonly Vector2 BlendPosition;

            public SheetDirection(string suffix, int row, Vector2 blendPosition)
            {
                Suffix = suffix;
                Row = row;
                BlendPosition = blendPosition;
            }
        }
    }
}
