using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 강화 화면 테스트용 APK를 따로 빌드한다 (본 빌드와 섞이지 않게).
/// - OutgameUpgrade 씬 하나만 담는다. 빌드 목록(EditorBuildSettings)은 건드리지 않는다.
/// - 패키지 이름·앱 이름을 빌드하는 동안만 테스트용으로 바꿔서, 폰에 원래 게임과 따로 설치되게 한다 (저장 데이터도 따로).
///   끝나면 성공·실패와 상관없이 원래 값으로 되돌린다. AAB 설정이 켜져 있어도 이번 빌드만 APK로 만든다.
/// - 결과물: Builds/UpgradeTest.apk (Builds/ 는 git 제외).
/// </summary>
public static class UpgradeTestBuild
{
    private const string ScenePath = "Assets/WorkSpace/USW/Data/OutgameUpgrade.unity";
    private const string OutputPath = "Builds/UpgradeTest.apk";
    private const string TestPackageName = "com.GaeGGUL.upgradetest";
    private const string TestProductName = "개꿀 강화테스트";

    [MenuItem("Tools/USW/Build/강화 테스트 APK")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[UpgradeTestBuild] 플레이 중에는 빌드하지 않는다"); return; }
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            EditorUtility.DisplayDialog("강화 테스트 APK", "지금 플랫폼이 Android가 아니에요. Build Profiles에서 Android로 바꾼 뒤 다시 눌러 주세요.", "확인");
            return;
        }
        if (!File.Exists(ScenePath))
        {
            EditorUtility.DisplayDialog("강화 테스트 APK", $"씬이 없어요: {ScenePath}", "확인");
            return;
        }

        var android = NamedBuildTarget.Android;
        string originalPackage = PlayerSettings.GetApplicationIdentifier(android);
        string originalProduct = PlayerSettings.productName;
        bool originalAppBundle = EditorUserBuildSettings.buildAppBundle;

        BuildReport report = null;
        try
        {
            PlayerSettings.SetApplicationIdentifier(android, TestPackageName);
            PlayerSettings.productName = TestProductName;
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
        }
        finally
        {
            PlayerSettings.SetApplicationIdentifier(android, originalPackage);
            PlayerSettings.productName = originalProduct;
            EditorUserBuildSettings.buildAppBundle = originalAppBundle;
            AssetDatabase.SaveAssets();
        }

        var summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[UpgradeTestBuild] 완료: {OutputPath} ({summary.totalSize / (1024 * 1024)}MB, {summary.totalTime.TotalSeconds:0}초). 패키지 {TestPackageName}, 설정은 {originalPackage} 로 되돌림");
            EditorUtility.RevealInFinder(OutputPath);
        }
        else
        {
            Debug.LogError($"[UpgradeTestBuild] 실패: {summary.result}, 에러 {summary.totalErrors}개. 콘솔 로그를 확인. 설정은 {originalPackage} 로 되돌림");
            EditorUtility.DisplayDialog("강화 테스트 APK", $"빌드 실패 ({summary.result}). 콘솔을 확인해 주세요.\n패키지 이름 등 설정은 원래대로 되돌렸어요.", "확인");
        }
    }
}
