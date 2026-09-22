using System;
using System.IO;
using System.Linq;
using Game.Content;
using Game.Core;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using static Game.EditorTools.UiBuilder;

namespace Game.EditorTools
{
    public static class SystemMenuTools
    {
        public const string MainMenuScenePath = "Assets/_Game/Scenes/MainMenu.unity";
        public const string PauseMenuPrefabPath = "Assets/_Game/Prefabs/UI/PauseMenu.prefab";
        public const string TitleBackgroundSprite = "bg_title";
        public const string GameTitleKey = "system.game_title";
        public const int TitleBackgroundWidth = 384;
        public const int TitleBackgroundHeight = 216;
        public const int TitleScale = 2;
        public const int TitleTop = 18;
        public const int TitleWidth = 320;
        public const int MainMenuRows = 5;
        public const int MainMenuWidth = 104;
        public const int MainMenuInset = 24;
        public const int PauseRows = 5;
        public const int PauseWidth = 112;
        public const int OptionsWidth = 208;
        public const int OptionsLabelWidth = 110;
        public const int OptionsValueWidth = 70;
        public const int CreditsWidth = 400;
        public const int CreditsHeight = 244;
        public const int SlotsWidth = 260;
        public const int SlotsHeight = 80;
        public const int ConfirmHeight = 30;
        public const int ConfirmChoiceWidth = 56;
        public const int BoxSidePadding = 8;

        private static readonly Vector2[] OutlineOffsets =
        {
            new(-1f, -1f), new(0f, -1f), new(1f, -1f),
            new(-1f, 0f), new(1f, 0f),
            new(-1f, 1f), new(0f, 1f), new(1f, 1f)
        };

        public static void BuildMenus() => BatchRunner.Run(BuildAll);

        public static void BuildAll()
        {
            SavePrefab(PauseMenuPrefabPath, root => BuildPauseRoot(root, Load<UiTextSO>(DataTools.UiTextPath), Load<FallacyCatalogSO>(DataTools.CatalogPath), Load<InputActionAsset>(SandboxTools.ControlsPath)));
            BuildMainMenuScene();
            ApplyBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"[SystemMenuTools] built {MainMenuScenePath} and {PauseMenuPrefabPath}");
        }

        public static void ApplyBuildScenes()
        {
            EditorBuildSettings.scenes = new[] { MainMenuScenePath, SandboxTools.ScenePath, SandboxTools.KerameikosScenePath, SandboxTools.SampleScenePath }
                .Where(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                .Select(path => new EditorBuildSettingsScene(path, true))
                .ToArray();
        }

        public static EventSystem CreateEventSystem()
        {
            var eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            return eventSystem;
        }

        public static PauseMenuController InstantiatePauseMenu()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PauseMenuPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[SystemMenuTools] {PauseMenuPrefabPath} is missing; run SystemMenuTools.BuildMenus first");
                return null;
            }

            return ((GameObject)PrefabUtility.InstantiatePrefab(prefab)).GetComponent<PauseMenuController>();
        }

        private static void BuildMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var uiText = Load<UiTextSO>(DataTools.UiTextPath);
            var controls = Load<InputActionAsset>(SandboxTools.ControlsPath);

            PrefabUtility.InstantiatePrefab(Load<GameObject>(RenderSetupTools.CameraRigPrefabPath));
            CreateEventSystem();
            var services = SandboxTools.CreateGameServices();

            var root = new GameObject("MainMenu", typeof(RectTransform));
            ConfigureCanvas(root);
            CreateTitleBackground(root.transform);
            CreateOutlinedLabel(root.transform, "Title", new Vector2(0f, -TitleTop), new Vector2(TitleWidth, TitleScale * LineHeight), TitleScale, AccentColor, uiText, GameTitleKey);
            CreateOutlinedLabel(root.transform, "Subtitle", new Vector2(0f, -TitleTop - TitleScale * LineHeight - 2), new Vector2(TitleWidth, LineHeight), 1, MarkerColor, uiText, UiTextKeys.GameSubtitle);

            var mainMenu = CreateListPanel(root.transform, "Menu", BottomRight, new Vector2(-MainMenuInset, MainMenuInset), MainMenuRows, MainMenuWidth, uiText, null);
            var options = CreateOptionsPanel(root.transform, uiText);
            var credits = CreateCreditsPanel(root.transform, uiText);
            var slots = CreateSlotsPanel(root.transform, uiText);

            var input = root.AddComponent<SystemMenuInput>();
            input.Configure(controls);
            root.AddComponent<MainMenuController>().Configure(services, input, mainMenu, options, credits, slots, MainMenuController.DefaultGameplayScene);

            AssetFolders.Ensure(Path.GetDirectoryName(MainMenuScenePath)?.Replace('\\', '/'));
            if (!EditorSceneManager.SaveScene(scene, MainMenuScenePath))
            {
                throw new InvalidOperationException($"could not save {MainMenuScenePath}");
            }
        }

        private static void CreateTitleBackground(Transform parent)
        {
            var background = Fill(parent, "Background");
            Paint(background.gameObject, UiSprite(TitleBackgroundSprite), Color.white);
            var fitter = background.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = (float)TitleBackgroundWidth / TitleBackgroundHeight;
        }

        private static void CreateOutlinedLabel(Transform parent, string name, Vector2 position, Vector2 size, int scale, Color color, UiTextSO uiText, string key)
        {
            var group = CreateRect(parent, name, TopCenter, position, size);
            foreach (var offset in OutlineOffsets)
            {
                AddScaledLabel(group, $"Outline{offset.x}_{offset.y}", offset * scale, size, scale, InkColor, uiText, key);
            }

            AddScaledLabel(group, "Face", Vector2.zero, size, scale, color, uiText, key);
        }

        private static void AddScaledLabel(Transform parent, string name, Vector2 offset, Vector2 size, int scale, Color color, UiTextSO uiText, string key)
        {
            var label = CreateLabel(parent, name, Center, offset, size, TextAlignmentOptions.Center);
            label.fontSize = FontSize * scale;
            label.overflowMode = TextOverflowModes.Overflow;
            label.color = color;
            label.gameObject.AddComponent<UiTextLabel>().Configure(label, uiText, key);
        }

        private static void BuildPauseRoot(GameObject root, UiTextSO uiText, FallacyCatalogSO catalog, InputActionAsset controls)
        {
            ConfigureCanvas(root).sortingOrder = 10;
            var panel = Fill(root.transform, "Panel");
            Paint(panel.gameObject, null, DimColor);

            var menu = CreateListPanel(panel, "PauseMenu", Center, Vector2.zero, PauseRows, PauseWidth, uiText, SystemMenus.PauseTitleKey);
            var options = CreateOptionsPanel(panel, uiText);
            var slots = CreateSlotsPanel(panel, uiText);
            var compendium = CreateCompendiumPanel(panel, uiText, catalog);

            var input = root.AddComponent<SystemMenuInput>();
            input.Configure(controls);
            root.AddComponent<PauseMenuController>().Configure(null, input, panel.gameObject, menu, options, slots, compendium, PauseMenuController.DefaultMainMenuScene);
            panel.gameObject.SetActive(false);
        }

        private static CompendiumView CreateCompendiumPanel(Transform parent, UiTextSO uiText, FallacyCatalogSO catalog)
        {
            var host = Fill(parent, "Compendium").gameObject;
            var view = BattleHudTools.CreateCompendiumView(host, uiText, catalog);
            host.SetActive(false);
            return view;
        }

        private static MenuListView CreateListPanel(Transform parent, string name, Vector2 anchor, Vector2 position, int rowCount, int width, UiTextSO uiText, string titleKey)
        {
            var box = CreateBox(parent, name, anchor, position, new Vector2(width, 2 * BoxTopPadding + rowCount * RowHeight));
            var title = titleKey == null ? null : CreateWindowTitle(box.transform);
            var view = box.gameObject.AddComponent<MenuListView>();
            var (rows, cursor) = CreatePanelRows(box.transform, rowCount, new Vector2(RowInset, -BoxTopPadding), width - RowInset - BoxSidePadding, view);
            view.Configure(title, titleKey, rows, cursor, uiText);
            return view;
        }

        private static TMP_Text CreateWindowTitle(Transform box)
        {
            var title = CreateTab(box, "Title", TopLeft, TabInset, AccentColor, TrackColor);
            title.fontStyle = FontStyles.UpperCase;
            return title;
        }

        private static OptionsView CreateOptionsPanel(Transform parent, UiTextSO uiText)
        {
            var panel = CreateBox(parent, "Options", Center, Vector2.zero, new Vector2(OptionsWidth, 2 * BoxTopPadding + OptionsModel.RowCount * RowHeight));
            var view = panel.gameObject.AddComponent<OptionsView>();
            var title = CreateWindowTitle(panel.transform);

            var (labels, cursor) = CreatePanelRows(panel.transform, OptionsModel.RowCount, new Vector2(RowInset, -BoxTopPadding), OptionsLabelWidth, view);
            var valueLeft = OptionsWidth - BoxSidePadding - OptionsValueWidth;
            var values = Enumerable.Range(0, OptionsModel.RowCount)
                .Select(index => CreateLabel(panel.transform, $"Value{index}", TopLeft, new Vector2(valueLeft, -BoxTopPadding - index * RowHeight), new Vector2(OptionsValueWidth, RowHeight), TextAlignmentOptions.Right))
                .ToArray();

            view.Configure(title, labels, values, cursor, uiText);
            panel.gameObject.SetActive(false);
            return view;
        }

        private static CreditsView CreateCreditsPanel(Transform parent, UiTextSO uiText)
        {
            var panel = CreateBox(parent, "Credits", Center, Vector2.zero, new Vector2(CreditsWidth, CreditsHeight));
            var title = CreateWindowTitle(panel.transform);
            var body = CreateLabel(panel.transform, "Body", TopLeft, new Vector2(BoxSidePadding, -BoxTopPadding - 2f), new Vector2(CreditsWidth - 2 * BoxSidePadding, CreditsHeight - 2 * BoxTopPadding - 4f), TextAlignmentOptions.TopLeft, true);
            body.overflowMode = TextOverflowModes.Page;

            var view = panel.gameObject.AddComponent<CreditsView>();
            view.Configure(title, body, uiText);
            panel.gameObject.SetActive(false);
            return view;
        }

        private static SaveSlotsView CreateSlotsPanel(Transform parent, UiTextSO uiText)
        {
            var panel = CreateBox(parent, "SaveSlots", Center, Vector2.zero, new Vector2(SlotsWidth, SlotsHeight));
            var view = panel.gameObject.AddComponent<SaveSlotsView>();
            var title = CreateWindowTitle(panel.transform);
            var (rows, cursor) = CreatePanelRows(panel.transform, SaveSlots.Count, new Vector2(RowInset, -BoxTopPadding), SlotsWidth - RowInset - BoxSidePadding, view);

            var confirmWidth = SlotsWidth - 2 * BoxSidePadding;
            var confirm = CreateBox(panel.transform, "Confirm", BottomCenter, new Vector2(0f, 3f), new Vector2(confirmWidth, ConfirmHeight));
            var confirmLabel = CreateLabel(confirm.transform, "Question", TopLeft, new Vector2(BoxSidePadding, -3f), new Vector2(confirmWidth - ConfirmChoiceWidth - RowInset - 2 * BoxSidePadding, RowHeight), TextAlignmentOptions.Left);
            confirmLabel.color = AccentColor;
            var (confirmRows, confirmCursor) = CreatePanelRows(confirm.transform, 2, new Vector2(confirmWidth - ConfirmChoiceWidth, -3f), ConfirmChoiceWidth - BoxSidePadding, view);
            confirm.gameObject.SetActive(false);

            view.Configure(title, rows, cursor, confirm.gameObject, confirmLabel, confirmRows, confirmCursor, uiText);
            panel.gameObject.SetActive(false);
            return view;
        }
    }
}
