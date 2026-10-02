using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보스 처치 공지 실험실 씬(FxLab_BossClearNotice)과 설정 에셋을 만든다 (사용자 요청 2026-10-02).
/// 타이머 보너스 실험실 무대(TimerBonusFxLabBuilder.CreateTimerLab)를 그대로 깔고, 그 위에 공지 시안 4종을 얹는다:
/// A 밴드 슬라이스 / B 골드 배너 / C 젤리 필 / D 크로스 스트라이크. 참고: design/보스죽고나서연출.gif, 보스죽고나서연출2.gif.
/// 설정 에셋은 없을 때만 만든다 (튜닝 값 보존). 씬은 매번 덮어쓴다.
/// </summary>
public static class BossClearNoticeLabBuilder
{
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_BossClearNotice.unity";
    private const string SettingsFolderParent = "Assets/WorkSpace/USW/Data/UI";
    private const string SettingsFolderName = "BossClearNoticeLab";
    private const string SettingsPath = SettingsFolderParent + "/" + SettingsFolderName + "/BossClearNoticeSettings.asset";
    private static readonly string[] NoticeButtons = { "공지 A 밴드", "공지 B 골드", "공지 C 젤리", "공지 D 스트라이크" };
    private const float CaptureDuration = 3.8f;
    private const float LabelY = 130f;

    [MenuItem("Tools/USW/Fx/Build Boss Clear Notice Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[BossClearNoticeLab] 플레이 중에는 실행하지 않는다"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        TimerBonusFxLabBuilder.EnsureSettings();
        EnsureSettings();
        BuildScene();
        Debug.Log($"[BossClearNoticeLab] 완료: {ScenePath} (설정 {SettingsPath})");
    }

    private static void EnsureSettings()
    {
        if (AssetDatabase.LoadAssetAtPath<BossClearNoticeSettings>(SettingsPath) != null) return;
        if (!AssetDatabase.IsValidFolder(SettingsFolderParent + "/" + SettingsFolderName))
            AssetDatabase.CreateFolder(SettingsFolderParent, SettingsFolderName);
        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<BossClearNoticeSettings>(), SettingsPath);
        AssetDatabase.SaveAssets();
    }

    private static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var timerLab = TimerBonusFxLabBuilder.CreateTimerLab(out var canvasRt, out var font, out var cam);
        // 새 씬을 연 뒤에 불러온다 (TimerBonusFxLabBuilder와 같은 이유)
        var settings = AssetDatabase.LoadAssetAtPath<BossClearNoticeSettings>(SettingsPath);

        // 공지 루트: 화면 가운데, 플래시(Overlay) 바로 위 · 버튼/상태 줄 아래
        var root = TimerBonusFxLabBuilder.NewRect("NoticeRoot", canvasRt, Vector2.zero);
        var overlay = canvasRt.Find("Overlay");
        root.SetSiblingIndex(overlay != null ? overlay.GetSiblingIndex() + 1 : canvasRt.childCount - 1);

        var label = TimerBonusFxLabBuilder.NewText("NoticeLabel", canvasRt, font, 26f, new Color(1f, 0.85f, 0.5f), new Vector2(-40f, 40f));
        label.alignment = TextAlignmentOptions.BottomLeft;
        label.rectTransform.anchorMin = new Vector2(0f, 0f);
        label.rectTransform.anchorMax = new Vector2(1f, 0f);
        label.rectTransform.pivot = new Vector2(0.5f, 0f);
        label.rectTransform.anchoredPosition = new Vector2(0f, LabelY);

        var lab = new GameObject("BossClearNoticeLab", typeof(BossClearNoticeLab)).GetComponent<BossClearNoticeLab>();
        var row = TimerBonusFxLabBuilder.NewButtonRow(canvasRt, "Notices", -16f - TimerBonusFxLabBuilder.RowStep * 2f);
        var images = new Image[NoticeButtons.Length];
        for (int i = 0; i < NoticeButtons.Length; i++)
        {
            var b = TimerBonusFxLabBuilder.NewButton(row, NoticeButtons[i], font);
            UnityEventTools.AddIntPersistentListener(b.onClick, lab.SelectNotice, i);
            images[i] = b.GetComponent<Image>();
        }

        var so = new SerializedObject(lab);
        so.FindProperty("_timerLab").objectReferenceValue = timerLab;
        so.FindProperty("_settings").objectReferenceValue = settings;
        so.FindProperty("_font").objectReferenceValue = font;
        so.FindProperty("_root").objectReferenceValue = root;
        so.FindProperty("_label").objectReferenceValue = label;
        var buttons = so.FindProperty("_variantButtons");
        buttons.arraySize = images.Length;
        for (int i = 0; i < images.Length; i++) buttons.GetArrayElementAtIndex(i).objectReferenceValue = images[i];
        so.FindProperty("_variant").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();

        TimerBonusFxLabBuilder.AddCapture(cam, lab, CaptureDuration, "Temp/FxCapture/BossClearNotice");
        EditorSceneManager.SaveScene(scene, ScenePath);
    }
}
