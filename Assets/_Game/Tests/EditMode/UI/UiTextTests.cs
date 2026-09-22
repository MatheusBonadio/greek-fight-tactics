using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Battle;
using Game.Content;
using Game.Core;
using Game.EditorTools;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.UI
{
    public sealed class UiTextTests
    {
        [Test]
        public void GeneratedUiText_CoversEveryPlayerActionRejectionAndMenuTitle()
        {
            var uiText = AssetDatabase.LoadAssetAtPath<UiTextSO>(DataTools.UiTextPath);
            Assert.IsNotNull(uiText, $"run DataTools.Regenerate to create {DataTools.UiTextPath}");

            foreach (var action in ActionMenuModel.PlayerActions)
            {
                Assert.IsTrue(uiText.Contains(UiTextKeys.For(action)), UiTextKeys.For(action));
            }

            foreach (RejectionReason reason in Enum.GetValues(typeof(RejectionReason)))
            {
                if (reason != RejectionReason.None)
                {
                    Assert.IsTrue(uiText.Contains(UiTextKeys.For(reason)), UiTextKeys.For(reason));
                }
            }

            Assert.IsTrue(uiText.Contains(UiTextKeys.ChooseFallacy));
            Assert.IsTrue(uiText.Contains(UiTextKeys.ChoosePremise));
            Assert.AreEqual("MAIÊUTICA", uiText.Get(UiTextKeys.For(ActionId.Maieutics)));
        }

        [Test]
        public void BattleNarration_NamesTheDebaterEachLineIsAbout()
        {
            const string speaker = "Kleia";
            const string listener = "Trasíbulo";
            var uiText = AssetDatabase.LoadAssetAtPath<UiTextSO>(DataTools.UiTextPath);
            Assert.IsNotNull(uiText, $"run DataTools.Regenerate to create {DataTools.UiTextPath}");

            foreach (var key in new[] { BattleText.RefuteHitKey, BattleText.RefuteMissKey, BattleText.MaieuticsHitKey })
            {
                var line = string.Format(uiText.Get(key), speaker, listener);
                StringAssert.Contains(listener, line, $"{key} tells what happened to the debater who was answered");
                StringAssert.DoesNotContain(speaker, line, $"{key} must not credit the wrong side");
            }

            foreach (var key in new[] { BattleText.ConcedeKey, BattleText.ConcedeCounterKey, BattleText.QuoteKey, BattleText.DeflectedKey })
            {
                var line = string.Format(uiText.Get(key), speaker, listener);
                StringAssert.Contains(speaker, line, $"{key} tells what the debater of the turn did");
                StringAssert.DoesNotContain(listener, line, $"{key} must not name the other side");
            }
        }

        [Test]
        public void GeneratedUiText_HasALineForEverythingTheBattleNarrates()
        {
            var uiText = AssetDatabase.LoadAssetAtPath<UiTextSO>(DataTools.UiTextPath);
            Assert.IsNotNull(uiText, $"run DataTools.Regenerate to create {DataTools.UiTextPath}");

            var keys = typeof(BattleText)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue())
                .Concat(Enum.GetValues(typeof(BattleStatus)).Cast<BattleStatus>().Select(BattleText.StatusKey))
                .ToList();

            Assert.IsNotEmpty(keys);
            foreach (var key in keys)
            {
                Assert.IsTrue(uiText.Contains(key), key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(uiText.Get(key)), key);
            }
        }

        [Test]
        public void MissingText_IsVisibleInsteadOfBlank()
        {
            var uiText = ScriptableObject.CreateInstance<UiTextSO>();
            try
            {
                uiText.SetEntries(new Dictionary<string, string> { ["menu.title"] = "Menu", ["menu.empty"] = string.Empty });

                Assert.AreEqual("Menu", uiText.Get("menu.title"));
                Assert.AreEqual("#menu.empty", uiText.Get("menu.empty"));
                Assert.AreEqual("#menu.missing", uiText.Get("menu.missing"));
            }
            finally
            {
                Object.DestroyImmediate(uiText);
            }
        }

        [Test]
        public void Keys_AreSnakeCaseEnumNames()
        {
            Assert.AreEqual("rejection.argument_not_refutable", UiTextKeys.For(RejectionReason.ArgumentNotRefutable));
            Assert.AreEqual("action.maieutics", UiTextKeys.For(ActionId.Maieutics));
        }
    }
}
