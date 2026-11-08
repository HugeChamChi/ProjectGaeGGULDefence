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
        private static string InternalOutput => "Builds/InternalTest/GaeGGUL-ShopTest." + (_internalBundle ? "aab" : "apk");
        private const string InternalResult = "outputs/internal-release-prep/build-result.json";
        private static bool _internalQueued;
        private static bool _internalBundle;

        private static string SigningPassword() => Environment.GetEnvironmentVariable(KeystorePasswordEnvVar)
            ?? Environment.GetEnvironmentVariable(KeystorePasswordEnvVar, EnvironmentVariableTarget.User)
            ?? "123456";

        [MenuItem("GaeGGUL/Build/Android AAB (Internal Test)")]
        public static void BuildInternalAab() => QueueInternalAab();

        [MenuItem("GaeGGUL/Build/Android APK (Internal Test)")]
        public static void BuildInternalApk() => QueueInternalBuild(false);

        public static string QueueInternalAab() => QueueInternalBuild(true);

        public static string QueueInternalBuild(bool appBundle, string signingPassword = null)
        {
            if (_internalQueued || EditorApplication.isPlaying || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Stop Play and wait for compilation/build to finish.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Switch the active build target to Android first.");
            string password = signingPassword ?? SigningPassword();
            if (!string.IsNullOrEmpty(password))
            { PlayerSettings.Android.keystorePass = password; PlayerSettings.Android.keyaliasPass = password; }
            if (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) || string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
                throw new InvalidOperationException("Configure the upload key password locally or in Publishing Settings.");
            string keystore = PlayerSettings.Android.keystoreName;
            if (keystore.StartsWith("{inproject}:")) keystore = keystore.Substring("{inproject}:".Length).Trim();
            if (!File.Exists(keystore)) throw new FileNotFoundException("Upload keystore missing.", keystore);
            if (string.IsNullOrEmpty(PlayerSettings.Android.keyaliasName)) throw new InvalidOperationException("Configure the upload key alias in Publishing Settings.");
            _internalBundle = appBundle;
            Directory.CreateDirectory(Path.GetDirectoryName(InternalOutput));
            Directory.CreateDirectory(Path.GetDirectoryName(InternalResult));
            _internalQueued = true;
            EditorApplication.update += RunInternalAab;
            return "Queued signed shop test " + (appBundle ? "AAB" : "APK") + ": ShopTestTitle -> ShopTestLobby, non-debuggable, DEV charts.";
        }

        [Serializable]
        private sealed class InternalReport
        {
            public string result, output, environment;
            public ulong bytes;
            public int errors, warnings;
            public bool development;
            public string[] messages;
        }

        private static void RunInternalAab()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= RunInternalAab;
            _internalQueued = false;
            bool bundle = EditorUserBuildSettings.buildAppBundle, development = EditorUserBuildSettings.development,
                debugging = EditorUserBuildSettings.allowDebugging, customKey = PlayerSettings.Android.useCustomKeystore;
            try
            {
                EditorUserBuildSettings.buildAppBundle = _internalBundle;
                EditorUserBuildSettings.development = false;
                EditorUserBuildSettings.allowDebugging = false;
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.bundleVersionCode = Math.Max(2, PlayerSettings.Android.bundleVersionCode);
                AssetDatabase.SaveAssets();
                File.WriteAllText(InternalResult, "{\"result\":\"Building\"}");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/ShopTestTitle.unity" },
                    locationPathName = InternalOutput,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.DetailedBuildReport,
                    extraScriptingDefines = new[] { "INTERNAL_TEST_BUILD", "SHOP_TEST_BUILD" }
                });
                var summary = report.summary;
                File.WriteAllText(InternalResult, JsonUtility.ToJson(new InternalReport
                {
                    result = summary.result.ToString(), output = Path.GetFullPath(InternalOutput),
                    environment = "DEV via INTERNAL_TEST_BUILD", bytes = summary.totalSize,
                    errors = (int)summary.totalErrors, warnings = (int)summary.totalWarnings,
                    development = (summary.options & BuildOptions.Development) != 0,
                    messages = report.steps.SelectMany(s => s.messages)
                        .Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content).ToArray()
                }, true));
                if (summary.result == BuildResult.Succeeded)
                    Debug.Log("[AndroidBuildPipeline] Internal build succeeded: " + Path.GetFullPath(InternalOutput));
                else
                    Debug.LogError("[AndroidBuildPipeline] Internal build failed. See " + InternalResult);
            }
            catch (Exception error)
            {
                File.WriteAllText(InternalResult, JsonUtility.ToJson(new InternalReport { result = "Failed", messages = new[] { error.Message } }, true));
                Debug.LogException(error);
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = bundle;
                EditorUserBuildSettings.development = development;
                EditorUserBuildSettings.allowDebugging = debugging;
                PlayerSettings.Android.useCustomKeystore = customKey;
                AssetDatabase.SaveAssets();
            }
        }

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
            string password = SigningPassword();
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
