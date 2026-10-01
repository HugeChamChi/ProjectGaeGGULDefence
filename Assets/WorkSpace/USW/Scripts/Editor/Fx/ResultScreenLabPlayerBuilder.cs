using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>FxLab_Result만 포함한 비교용 Android APK를 만든다.</summary>
public static class ResultScreenLabPlayerBuilder
{
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_Result.unity";
    private const string OutputDirectory = "outputs/result-ui-20261001";
    private const string FileName = "GGD-ResultLab.apk";
    private const string TestIdentifier = "com.gaeggul.resultlab";
    private static bool _queued;

    /// <summary>다음 Editor 갱신에서 결과 비교 APK를 만든다. 기존 앱/빌드 설정은 완료 후 복원한다.</summary>
    [MenuItem("Tools/USW/Fx/Build Result Comparison APK")]
    public static void QueueApkBuild()
    {
        if (_queued || BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("A build is already running.");
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before building.");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            throw new InvalidOperationException("Activate the Android target before building the comparison APK.");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            throw new InvalidOperationException("Android build support is not installed.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            throw new InvalidOperationException("Build the Result Screen Lab scene first.");
        _queued = true;
        WriteStatus(new BuildStatus { Status = "queued", Result = "Pending", OutputPath = ApkPath });
        EditorApplication.delayCall += BuildApk;
    }

    private static string DirectoryPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputDirectory));
    private static string ApkPath => Path.Combine(DirectoryPath, FileName);

    private static void BuildApk()
    {
        var status = new BuildStatus { Status = "building", Result = "Pending", OutputPath = ApkPath };
        WriteStatus(status);
        string originalName = PlayerSettings.productName;
        string originalIdentifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        bool originalBundle = EditorUserBuildSettings.buildAppBundle;
        bool originalKeystore = PlayerSettings.Android.useCustomKeystore;
        try
        {
            PlayerSettings.productName = "GGD Result Lab";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, TestIdentifier);
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.Android.useCustomKeystore = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.DetailedBuildReport,
            });
            status.Result = report.summary.result.ToString();
            status.TotalBytes = report.summary.totalSize;
            status.TotalErrors = report.summary.totalErrors;
            status.TotalWarnings = report.summary.totalWarnings;
            status.DurationSeconds = report.summary.totalTime.TotalSeconds;
            status.Errors = report.steps.SelectMany(step => step.messages)
                .Where(message => message.type == LogType.Error || message.type == LogType.Exception)
                .Select(message => message.content).Take(12).ToArray();
            Debug.Log($"[ResultScreenLab APK] {status.Result}: {ApkPath}");
        }
        catch (Exception exception)
        {
            status.Result = "Failed";
            status.Errors = new[] { exception.ToString() };
            Debug.LogException(exception);
        }
        finally
        {
            PlayerSettings.productName = originalName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, originalIdentifier);
            EditorUserBuildSettings.buildAppBundle = originalBundle;
            PlayerSettings.Android.useCustomKeystore = originalKeystore;
            _queued = false;
            status.Status = "completed";
            status.SettingsRestored = true;
            WriteStatus(status);
        }
    }

    private static void WriteStatus(BuildStatus status)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(Path.Combine(DirectoryPath, "apk-build-status.json"), JsonUtility.ToJson(status, true));
    }

    [Serializable]
    private sealed class BuildStatus
    {
        public string Status;
        public string Result;
        public string OutputPath;
        public string Scene = ScenePath;
        public string ApplicationIdentifier = TestIdentifier;
        public ulong TotalBytes;
        public int TotalErrors;
        public int TotalWarnings;
        public double DurationSeconds;
        public bool SettingsRestored;
        public string[] Errors = Array.Empty<string>();
    }
}
