using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SpinningFeast.Editor
{
    /// <summary>
    /// CLI entry: -buildTarget Android -executeMethod SpinningFeast.Editor.ReceiptAndroidBuild.BuildDevelopmentApk
    /// Builds an unsigned-by-user (Unity debug-key signed) local development APK. Never deploys or publishes.
    /// Run in an isolated worktree; temporary build settings are restored even when validation/build throws.
    /// </summary>
    public static class ReceiptAndroidBuild
    {
        public static void BuildDevelopmentApk()
        {
            const string scene = "Assets/Scenes/Banquet.unity";
            const string output = "Builds/Android/ReceiptUI-dev.apk";
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Start Unity with -buildTarget Android before this executeMethod. The helper does not switch targets.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Android Build Support is unavailable for this Unity Editor.");
            if (!File.Exists(scene))
                throw new FileNotFoundException("Required game scene is missing; no scene or build-settings mutation was attempted.", scene);

            var target = NamedBuildTarget.Android;
            var oldBackend = PlayerSettings.GetScriptingBackend(target);
            var oldArchitectures = PlayerSettings.Android.targetArchitectures;
            var oldIdentifier = PlayerSettings.GetApplicationIdentifier(target);
            var oldBundle = EditorUserBuildSettings.buildAppBundle;
            var oldExport = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            var oldExpansion = PlayerSettings.Android.useAPKExpansionFiles;
            var oldCustomKeystore = PlayerSettings.Android.useCustomKeystore;
            try
            {
                ReceiptUITests.ValidateReceiptUI();
                MobileGeometryTests.Validate();
                ProjectTools.RunSimulationTests();
                ProjectTools.RunVisualDataTests();
                ProjectTools.RunAudioChecks();

                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                // Stable valid identifier for this local-only test APK; original restored below.
                PlayerSettings.SetApplicationIdentifier(target, "local.spinningfeast.receiptuitest");
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                PlayerSettings.Android.useAPKExpansionFiles = false;
                // Do not read, modify, or save any user keystore path/password; use Unity's debug key.
                PlayerSettings.Android.useCustomKeystore = false;
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { scene },
                    locationPathName = output,
                    target = BuildTarget.Android,
                    options = BuildOptions.Development
                });
                if (report == null || report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Android development APK build failed: " +
                        (report == null ? "no report" : report.summary.result.ToString()));
                if (!File.Exists(output))
                    throw new FileNotFoundException("Build reported success but APK is missing.", output);
                Debug.Log("RECEIPT_ANDROID_APK_PASS path=" + output + " bytes=" + report.summary.totalSize +
                    "; development/debug-key build only. Device, IME, touch and rotation are NOT validated by this build.");
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(target, oldBackend);
                PlayerSettings.Android.targetArchitectures = oldArchitectures;
                PlayerSettings.SetApplicationIdentifier(target, oldIdentifier);
                EditorUserBuildSettings.buildAppBundle = oldBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = oldExport;
                PlayerSettings.Android.useAPKExpansionFiles = oldExpansion;
                PlayerSettings.Android.useCustomKeystore = oldCustomKeystore;
            }
        }
    }
}
