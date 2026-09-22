using Game.Core;
using Game.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.UI
{
    public sealed class ArgumentWorkbenchTests
    {
        [Test]
        public void ArgumentWorkbench_DescribesReportsAndPreviewsDialogue()
        {
            var validator = new ArgumentValidator(new[] { "ad_hominem" });
            var report = Game.Net.ArgumentValidationReport.Validate("not json", validator);

            StringAssert.StartsWith("Invalid JSON", ArgumentWorkbenchWindow.DescribeReport(report));

            var argument = new Argument(new string('a', 30) + " " + new string('b', 30), "ad_hominem", new[] { "p" }, 0, ArgumentTones.Mocking);
            StringAssert.Contains("\n", ArgumentWorkbenchWindow.PreviewDialogue(argument));

            var window = ScriptableObject.CreateInstance<ArgumentWorkbenchWindow>();
            Assert.IsNotNull(window);
            Object.DestroyImmediate(window);
        }
    }
}
