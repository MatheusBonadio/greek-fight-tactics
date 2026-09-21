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
using static Game.EditorTools.UiBuilder;

namespace Game.EditorTools
{
    public static class SystemMenuTools
    {
        public const string MainMenuScenePath = "Assets/_Game/Scenes/MainMenu.unity";
        public const string PauseMenuPrefabPath = "Assets/_Game/Prefabs/UI/PauseMenu.prefab";
        public const string GameTitleKey = "system.game_title";
        public const int MainMenuRows = 5;
        public const int PauseRows = 5;
        public const int OptionsWidth = 176;
        public const int OptionsHeight = 72;
        public const int CreditsWidth = 208;
        public const int CreditsHeight = 176;
        public const int SlotsWidth = 208;
        public const int SlotsHeight = 80;
        public const int ConfirmHeight = 28;

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
            CreateImage(root.transform, "Background", null, Center, Vector2.zero, new Vector2(BattleHudTools.CanvasReference, BattleHudTools.CanvasReference), TrackColor);
            var title = CreateLabel(root.transform, "Title", TopCenter, new Vector2(0f, -40f), new Vector2(BattleHudTools.ReasonWidth, 16f), TextAlignmentOptions.Center);
            title.color = AccentColor;

            var mainMenu = CreateListPanel(root.transform, "Menu", BottomCenter, new Vector2(0f, 24f), MainMenuRows, uiText, title, GameTitleKey);
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

        private static void BuildPauseRoot(GameObject root, UiTextSO uiText, FallacyCatalogSO catalog, InputActionAsset controls)
        {
            ConfigureCanvas(root).sortingOrder = 10;
            var panel = CreateImage(root.transform, "Panel", null, Center, Vector2.zero, new Vector2(BattleHudTools.CanvasReference, BattleHudTools.CanvasReference), DimColor);
            var title = CreateLabel(panel.transform, "Title", Center, new Vector2(0f, 56f), new Vector2(BattleHudTools.MenuWidth, BattleHudTools.RowHeight), TextAlignmentOptions.Center);
            title.color = AccentColor;

            var menu = CreateListPanel(panel.transform, "PauseMenu", Center, Vector2.zero, PauseRows, uiText, title, SystemMenus.PauseTitleKey);
            var options = CreateOptionsPanel(panel.transform, uiText);
            var slots = CreateSlotsPanel(panel.transform, uiText);
            var compendium = CreateCompendiumPanel(panel.transform, uiText, catalog);

            var input = root.AddComponent<SystemMenuInput>();
            input.Configure(controls);
            root.AddComponent<PauseMenuController>().Configure(null, input, panel.gameObject, menu, options, slots, compendium, PauseMenuController.DefaultMainMenuScene);
            panel.gameObject.SetActive(false);
        }

        private static CompendiumView CreateCompendiumPanel(Transform parent, UiTextSO uiText, FallacyCatalogSO catalog)
        {
            var host = CreateRect(parent, "Compendium", Center, Vector2.zero, new Vector2(BattleHudTools.CanvasReference, BattleHudTools.CanvasReference)).gameObject;
            var view = BattleHudTools.CreateCompendiumView(host, uiText, catalog);
            host.SetActive(false);
            return view;
        }

        private static MenuListView CreateListPanel(Transform parent, string name, Vector2 anchor, Vector2 position, int rowCount, UiTextSO uiText, TMP_Text title, string titleKey)
        {
            var box = CreateImage(parent, name, UiSprite("box_menu"), anchor, position, new Vector2(BattleHudTools.MenuWidth, BattleHudTools.MenuHeight), Color.white);
            var view = box.gameObject.AddComponent<MenuListView>();
            var (rows, cursor) = CreatePanelRows(box.transform, rowCount, new Vector2(BattleHudTools.RowInset, -BattleHudTools.Margin), BattleHudTools.MenuWidth - BattleHudTools.RowInset - 4, view);
            view.Configure(title, titleKey, rows, cursor, uiText);
            return view;
        }

        private static OptionsView CreateOptionsPanel(Transform parent, UiTextSO uiText)
        {
            var panel = CreateImage(parent, "Options", null, Center, Vector2.zero, new Vector2(OptionsWidth, OptionsHeight), PanelColor);
            var view = panel.gameObject.AddComponent<OptionsView>();
            var title = CreateLabel(panel.transform, "Title", TopLeft, new Vector2(BattleHudTools.Margin, -2f), new Vector2(OptionsWidth - 2 * BattleHudTools.Margin, BattleHudTools.RowHeight), TextAlignmentOptions.MidlineLeft);
            title.color = AccentColor;

            var (labels, cursor) = CreatePanelRows(panel.transform, OptionsModel.RowCount, new Vector2(BattleHudTools.RowInset, -14f), 90f, view);
            var values = Enumerable.Range(0, OptionsModel.RowCount)
                .Select(index => CreateLabel(panel.transform, $"Value{index}", TopLeft, new Vector2(110f, -14f - index * BattleHudTools.RowHeight), new Vector2(58f, BattleHudTools.RowHeight), TextAlignmentOptions.MidlineRight))
                .ToArray();

            view.Configure(title, labels, values, cursor, uiText);
            panel.gameObject.SetActive(false);
            return view;
        }

        private static CreditsView CreateCreditsPanel(Transform parent, UiTextSO uiText)
        {
            var panel = CreateImage(parent, "Credits", null, Center, Vector2.zero, new Vector2(CreditsWidth, CreditsHeight), PanelColor);
            var title = CreateLabel(panel.transform, "Title", TopLeft, new Vector2(BattleHudTools.Margin, -4f), new Vector2(CreditsWidth - 2 * BattleHudTools.Margin, BattleHudTools.RowHeight), TextAlignmentOptions.MidlineLeft);
            title.color = AccentColor;
            var body = CreateLabel(panel.transform, "Body", TopLeft, new Vector2(BattleHudTools.Margin, -18f), new Vector2(CreditsWidth - 2 * BattleHudTools.Margin, CreditsHeight - 26f), TextAlignmentOptions.TopLeft, true);
            body.overflowMode = TextOverflowModes.Page;

            var view = panel.gameObject.AddComponent<CreditsView>();
            view.Configure(title, body, uiText);
            panel.gameObject.SetActive(false);
            return view;
        }

        private static SaveSlotsView CreateSlotsPanel(Transform parent, UiTextSO uiText)
        {
            var panel = CreateImage(parent, "SaveSlots", null, Center, Vector2.zero, new Vector2(SlotsWidth, SlotsHeight), PanelColor);
            var view = panel.gameObject.AddComponent<SaveSlotsView>();
            var title = CreateLabel(panel.transform, "Title", TopLeft, new Vector2(BattleHudTools.Margin, -2f), new Vector2(SlotsWidth - 2 * BattleHudTools.Margin, BattleHudTools.RowHeight), TextAlignmentOptions.MidlineLeft);
            title.color = AccentColor;
            var (rows, cursor) = CreatePanelRows(panel.transform, SaveSlots.Count, new Vector2(BattleHudTools.RowInset, -14f), SlotsWidth - BattleHudTools.RowInset - BattleHudTools.Margin, view);

            var confirm = CreateImage(panel.transform, "Confirm", null, BottomLeft, Vector2.zero, new Vector2(SlotsWidth, ConfirmHeight), DimColor);
            var confirmLabel = CreateLabel(confirm.transform, "Question", TopLeft, new Vector2(BattleHudTools.Margin, -2f), new Vector2(110f, BattleHudTools.RowHeight), TextAlignmentOptions.MidlineLeft);
            confirmLabel.color = AccentColor;
            var (confirmRows, confirmCursor) = CreatePanelRows(confirm.transform, 2, new Vector2(140f, -2f), 60f, view);
            confirm.gameObject.SetActive(false);

            view.Configure(title, rows, cursor, confirm.gameObject, confirmLabel, confirmRows, confirmCursor, uiText);
            panel.gameObject.SetActive(false);
            return view;
        }
    }
}
