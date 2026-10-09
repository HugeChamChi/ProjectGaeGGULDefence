using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 중앙 알림 "겹쳐 올라가기" 실험실 씬(FxLab_CenterToastRise)을 만든다 (사용자 요청 2026-10-09, 참고 design/중앙팝업1.gif).
/// 지금 인게임 방식 F(오른쪽에서 들어와 왼쪽으로 빠짐)를 비교용으로 두고,
/// 진입은 F 그대로 + 연타 시 이전 알림이 위로 겹쳐 올라가며 사라지는 G(글자만)·H(카드째)·I(G + 화면 전체 폭 띠)를 나란히 본다.
/// 인게임 설정 에셋을 건드리지 않도록 전용 설정 에셋을 쓴다 (없을 때만 만듦). 씬은 매번 덮어쓴다.
/// </summary>
public static class CenterToastRiseLabBuilder
{
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_CenterToastRise.unity";
    private const string SettingsFolder = "Assets/WorkSpace/USW/Data/UI/CenterToast";
    private const string SettingsPath = SettingsFolder + "/CenterToastRiseSettings.asset";
    private const int VisibleLines = 6;           // 참고 GIF처럼 이전 줄이 여러 겹 보이도록
    private static readonly string[] Labels = { "F 지금(비교)", "G 글자 겹침", "H 카드 겹침", "I 전체 띠" };
    private static readonly int[] Styles =
    {
        (int)CenterToastStyle.SlideLines,
        (int)CenterToastStyle.SlideRise,
        (int)CenterToastStyle.SlideCards,
        (int)CenterToastStyle.SlideBand,
    };

    [MenuItem("Tools/USW/Fx/Build Center Toast Rise Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[CenterToastRiseLab] 플레이 중에는 실행하지 않는다"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureSettings();
        CenterToastLabBuilder.BuildScene(ScenePath, SettingsPath, Labels, Styles, "Temp/FxCapture/CenterToastRise");
        Debug.Log($"[CenterToastRiseLab] 완료: {ScenePath} (설정 {SettingsPath})");
    }

    private static void EnsureSettings()
    {
        if (AssetDatabase.LoadAssetAtPath<CenterToastSettings>(SettingsPath) != null) return;
        if (!AssetDatabase.IsValidFolder(SettingsFolder))
        {
            Debug.LogError($"[CenterToastRiseLab] 폴더 없음: {SettingsFolder} — 먼저 Build Center Toast Lab 실행");
            return;
        }
        var settings = ScriptableObject.CreateInstance<CenterToastSettings>();
        settings.Style = CenterToastStyle.SlideRise;
        settings.MaxVisible = VisibleLines;
        AssetDatabase.CreateAsset(settings, SettingsPath);
        AssetDatabase.SaveAssets();
    }
}
