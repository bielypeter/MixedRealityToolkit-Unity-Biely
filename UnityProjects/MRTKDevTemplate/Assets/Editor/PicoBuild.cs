using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;

/// <summary>
/// Builds the PICO 4 APK following the Pupil Labs MRTK3 template guide
/// (https://docs.pupil-labs.com/neon/neon-xr/MRTK3-template-project):
/// Android target, landscape, PICO as the only XR plug-in provider,
/// Addressables content build, then the player build.
///
/// Menu: Pupil Labs > Build > PICO APK
/// CLI:  Unity.exe -batchmode -quit -projectPath . -executeMethod PicoBuild.BuildFromCommandLine [-output Builds/x.apk]
/// </summary>
public static class PicoBuild
{
    const string PicoLoaderType = "Unity.XR.PXR.PXR_Loader";
    const string DefaultOutput = "Builds/wheelchair-pico.apk";

    [MenuItem("Pupil Labs/Build/PICO APK")]
    public static void BuildFromMenu() => Build(DefaultOutput);

    public static void BuildFromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-output");
        string output = i >= 0 && i + 1 < args.Length ? args[i + 1] : DefaultOutput;
        try
        {
            Build(output);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("[PicoBuild] FAILED: " + e);
            EditorApplication.Exit(1);
        }
    }

    static void Build(string output)
    {
        // 1. Same as the PXR SDK Settings "Apply" button.
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

        // 2. XR Plug-in Management (Android): PICO only.
        ConfigurePicoLoader();

        // 3. Addressables: New Build > Default Build Script.
        AddressableAssetSettings.CleanPlayerContent();
        AddressableAssetSettings.BuildPlayerContent(out var addrResult);
        if (!string.IsNullOrEmpty(addrResult.Error))
            throw new Exception("Addressables build failed: " + addrResult.Error);
        Debug.Log("[PicoBuild] Addressables content built.");

        // 4. Player build.
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        });

        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception($"Player build {report.summary.result} with {report.summary.totalErrors} error(s).");
        Debug.Log($"[PicoBuild] SUCCESS: {output} ({report.summary.totalSize / (1024 * 1024)} MB, scenes: {string.Join(", ", scenes)})");
    }

    static void ConfigurePicoLoader()
    {
        var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
        if (generalSettings == null || generalSettings.Manager == null)
            throw new Exception("No XR General Settings for Android. Open Project Settings > XR Plug-in Management once.");

        var manager = generalSettings.Manager;
        foreach (var loader in manager.activeLoaders.ToList())
        {
            string type = loader.GetType().FullName;
            if (type != PicoLoaderType)
                XRPackageMetadataStore.RemoveLoader(manager, type, BuildTargetGroup.Android);
        }
        if (!manager.activeLoaders.Any(l => l.GetType().FullName == PicoLoaderType)
            && !XRPackageMetadataStore.AssignLoader(manager, PicoLoaderType, BuildTargetGroup.Android))
            throw new Exception("Could not assign PICO XR loader (is the PICO Unity Integration SDK installed?).");

        generalSettings.InitManagerOnStart = true;
        EditorUtility.SetDirty(generalSettings);
        EditorUtility.SetDirty(manager);
        AssetDatabase.SaveAssets();
        Debug.Log("[PicoBuild] Android XR loaders: " + string.Join(", ", manager.activeLoaders.Select(l => l.GetType().Name)));
    }
}
