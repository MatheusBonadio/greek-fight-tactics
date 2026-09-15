using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Content;
using Game.Core;
using Game.Net;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    public sealed class ArgumentWorkbenchWindow : EditorWindow
    {
        public const string MenuPath = "GFT/Argument Workbench";
        public const string WindowTitle = "Argument Workbench";

        private const float CatalogWidth = 240f;
        private const float PreviewBoxHeight = 96f;

        private Vector2 catalogScroll;
        private Vector2 argumentScroll;
        private string selectedEntryId;
        private string pastedJson = string.Empty;
        private ArgumentValidationReport lastReport;

        [MenuItem(MenuPath)]
        public static void Open() => GetWindow<ArgumentWorkbenchWindow>(WindowTitle);

        public static string DescribeReport(ArgumentValidationReport report)
        {
            if (report == null)
            {
                return string.Empty;
            }

            if (report.ParseError != null)
            {
                return $"Invalid JSON: {report.ParseError}";
            }

            var builder = new StringBuilder(report.IsValid ? "Valid" : "Invalid").AppendLine();
            foreach (var item in report.Items)
            {
                builder.AppendLine($"#{item.Index}: {item.Violation}");
            }

            return builder.ToString();
        }

        public static string PreviewDialogue(Argument argument) =>
            string.Join("\n\n", DialogueText.Paginate(argument.Speech).Select(page => string.Join("\n", page)));

        private void OnGUI()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);
            var library = AssetDatabase.LoadAssetAtPath<FallbackLibrarySO>(DataTools.FallbackLibraryPath);
            if (catalog == null || library == null)
            {
                EditorGUILayout.HelpBox("Run Game.EditorTools.DataTools.Regenerate to create the catalog and fallback library.", MessageType.Warning);
                return;
            }

            var validator = new ArgumentValidator(catalog.RefutableIds);
            EditorGUILayout.BeginHorizontal();
            DrawCatalog(catalog);
            EditorGUILayout.BeginVertical();
            DrawArguments(library, validator);
            DrawValidator(validator);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCatalog(FallacyCatalogSO catalog)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(CatalogWidth));
            EditorGUILayout.LabelField("Catalog", EditorStyles.boldLabel);
            catalogScroll = EditorGUILayout.BeginScrollView(catalogScroll);
            foreach (var fallacy in catalog.All.OrderBy(fallacy => fallacy.Category).ThenBy(fallacy => fallacy.Id))
            {
                var label = $"{fallacy.DisplayName} ({fallacy.Category})";
                if (GUILayout.Toggle(selectedEntryId == fallacy.Id, label, EditorStyles.miniButton))
                {
                    selectedEntryId = fallacy.Id;
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawArguments(FallbackLibrarySO library, ArgumentValidator validator)
        {
            EditorGUILayout.LabelField("Fallback arguments", EditorStyles.boldLabel);
            if (selectedEntryId == null || !library.TryGetJson(selectedEntryId, out var json))
            {
                EditorGUILayout.HelpBox("Select a catalog entry.", MessageType.Info);
                return;
            }

            var arguments = new List<Argument>();
            ArgumentJson.ParseValidList(json, validator, arguments);
            argumentScroll = EditorGUILayout.BeginScrollView(argumentScroll);
            foreach (var argument in arguments)
            {
                EditorGUILayout.LabelField($"tone: {argument.Tone}  weak premise: {argument.WeakPremise}", EditorStyles.miniLabel);
                EditorGUILayout.HelpBox(PreviewDialogue(argument), MessageType.None);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawValidator(ArgumentValidator validator)
        {
            EditorGUILayout.LabelField("Validate pasted JSON", EditorStyles.boldLabel);
            pastedJson = EditorGUILayout.TextArea(pastedJson, GUILayout.MinHeight(PreviewBoxHeight));
            if (GUILayout.Button("Validate"))
            {
                lastReport = ArgumentValidationReport.Validate(pastedJson, validator);
            }

            if (lastReport == null)
            {
                return;
            }

            EditorGUILayout.HelpBox(DescribeReport(lastReport), lastReport.IsValid ? MessageType.Info : MessageType.Error);
            foreach (var item in lastReport.Items.Where(item => item.Argument.HasValue))
            {
                EditorGUILayout.LabelField("Dialogue preview", EditorStyles.miniBoldLabel);
                EditorGUILayout.HelpBox(PreviewDialogue(item.Argument.Value), MessageType.None);
            }
        }
    }
}
