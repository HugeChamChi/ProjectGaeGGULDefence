using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GaeGGUL.Build.Editor
{
    public static class AndroidBuildPipeline
    {
        private const string KeystorePasswordEnvVar = "GAEGGUL_KEYSTORE_PASS";
        private const string OutputDirectory = "Builds/Android";

        [MenuItem("GaeGGUL/Build/Android AAB (Signed)")]
        public static void BuildAab() => Build(buildAppBundle: true, outputPath: $"{OutputDirectory}/GaeGGUL.aab");

        [MenuItem("GaeGGUL/Build/Android APK (Signed)")]
        public static void BuildApk() => Build(buildAppBundle: false, outputPath: $"{OutputDirectory}/GaeGGUL.apk");

        [MenuItem("GaeGGUL/Build/Android AAB + APK (Signed)")]
        public static void BuildBoth()
        {
            BuildAab();
            BuildApk();
        }

        private static void Build(bool buildAppBundle, string outputPath)
        {
            string password = Environment.GetEnvironmentVariable(KeystorePasswordEnvVar);
            if (string.IsNullOrEmpty(password))
            {
                Debug.LogError($"[AndroidBuildPipeline] 환경변수 '{KeystorePasswordEnvVar}'가 설정되어 있지 않아 빌드를 중단합니다.");
                return;
            }

            PlayerSettings.Android.keystorePass = password;
            PlayerSettings.Android.keyaliasPass = password;
            EditorUserBuildSettings.buildAppBundle = buildAppBundle;

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            });

            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[AndroidBuildPipeline] 빌드 성공: {outputPath} ({summary.totalSize / (1024 * 1024)}MB, 경고 {summary.totalWarnings}개)");
            }
            else
            {
                Debug.LogError($"[AndroidBuildPipeline] 빌드 실패: {outputPath} (에러 {summary.totalErrors}개, 경고 {summary.totalWarnings}개)");
            }
        }
    }
}
