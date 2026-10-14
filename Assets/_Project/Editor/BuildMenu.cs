using System.Collections.Generic;
using System.IO;
using Cinderbird.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Cinderbird.EditorTools
{
    /// <summary>
    /// One-click Windows builds into Builds/ (ignored by Git). The development build allows the Profiler to
    /// attach and reports garbage per frame; the release build is what players get (no debug keys or overlay); the
    /// performance-test build is a release build with the testing aids on (<see cref="DevTools"/>), for honest
    /// frame rates measured with the F3 overlay.
    /// </summary>
    internal static class BuildMenu
    {
        private const string BuildsFolder = "Builds";

        [MenuItem("Cinderbird/Build/Windows - Development")]
        private static void BuildDevelopment()
        {
            Build("Windows-Development", BuildOptions.Development | BuildOptions.ConnectWithProfiler);
        }

        [MenuItem("Cinderbird/Build/Windows - Release")]
        private static void BuildRelease()
        {
            Build("Windows-Release", BuildOptions.None);
        }

        [MenuItem("Cinderbird/Build/Windows - Performance Test")]
        private static void BuildPerformanceTest()
        {
            Build("Windows-PerfTest", BuildOptions.None, DevTools.PerfTestDefine);
        }

        private static void Build(string folderName, BuildOptions options, params string[] extraDefines)
        {
            List<string> scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    scenes.Add(scene.path);
                }
            }

            if (scenes.Count == 0)
            {
                Debug.LogError("No scenes in the Build Settings; add Main.unity first.");
                return;
            }

            string executable = Path.Combine(BuildsFolder, folderName, Application.productName + ".exe");
            BuildPlayerOptions build = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = executable,
                target = BuildTarget.StandaloneWindows64,
                options = options,
                extraScriptingDefines = extraDefines,
            };

            BuildReport report = BuildPipeline.BuildPlayer(build);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"Build {summary.result}: {summary.totalErrors} errors. See the Console above.");
                return;
            }

            Debug.Log($"Build done in {summary.totalTime.TotalSeconds:F0} s: {Path.GetFullPath(executable)}");
            EditorUtility.RevealInFinder(executable);
        }
    }
}
