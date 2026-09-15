using System;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.EditorTools
{
    public static class BuildTools
    {
        public const string OutputArgument = "-buildOutput";
        public const string WindowsDevelopmentDefaultOutput = "Build/PlanB/WindowsDevelopment/GFT.exe";
        public const string WindowsReleaseDefaultOutput = "Build/PlanB/WindowsRelease/GFT.exe";
        public const string WebDefaultOutput = "Build/PlanB/Web";

        public static void BuildWindowsDevelopment() =>
            BatchRunner.Run(() => Build(BuildProfileTools.WindowsDevelopmentName, WindowsDevelopmentDefaultOutput));

        public static void BuildWindowsRelease() =>
            BatchRunner.Run(() => Build(BuildProfileTools.WindowsReleaseName, WindowsReleaseDefaultOutput));

        public static void BuildWeb() =>
            BatchRunner.Run(() => Build(BuildProfileTools.WebName, WebDefaultOutput));

        public static BuildSummary Build(string profileName, string defaultOutput)
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(BuildProfileTools.PathFor(profileName));
            if (profile == null)
            {
                throw new InvalidOperationException($"missing build profile {BuildProfileTools.PathFor(profileName)}");
            }

            var output = ReadArgument(OutputArgument) ?? defaultOutput;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
            {
                buildProfile = profile,
                locationPathName = output,
                options = BuildOptions.None
            });

            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"build '{profileName}' finished with {summary.result} and {summary.totalErrors} error(s)");
            }

            Debug.Log($"[BuildTools] built '{profileName}' to {output} in {summary.totalTime.TotalSeconds:F0}s ({summary.totalSize} bytes)");
            return summary;
        }

        private static string ReadArgument(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (arguments[index] == name)
                {
                    return arguments[index + 1];
                }
            }

            return null;
        }
    }
}
