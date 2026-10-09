using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 중앙 알림 실험실 씬(FxLab_CenterToast)과 CenterToast 설정 에셋을 만든다 (사용자 요청 2026-10-02, 참고 design/중앙팝업1.gif).
/// 전투판 대역(타일 6×4) 위에 재사용 컴포넌트 CenterToast를 얹고, 연출 방식 4종·문구·연타 버튼을 단다.
/// 설정 에셋은 없을 때만 만든다 (인게임과 같은 에셋을 쓰므로 튜닝 값 보존). 씬은 매번 덮어쓴다.
/// 겹쳐 올라가기 실험실(CenterToastRiseLabBuilder)도 BuildScene을 같이 쓴다.
/// </summary>
public static class CenterToastLabBuilder
{
    private const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_CenterToast.unity";
    private const string SettingsFolderParent = "Assets/WorkSpace/USW/Data/UI";
    private const string SettingsFolderName = "CenterToast";
    private const string SettingsPath = SettingsFolderParent + "/" + SettingsFolderName + "/CenterToastSettings.asset";
    private static readonly int[] AllStyles = { 0, 1, 2, 3, 4, 5 };
    private const string FontGuid = "8863727b6c787ba4a910762f78e8fd55"; // KCC-Ganpan SDF (게임 UI 폰트)
    private const string ModernFontGuid = "34571da45f3f3d8419493b418172203f"; // NotoSansKR-Bold SDF (모던 A·B·C)
    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
    private static readonly string[] StyleButtons = { "A 스택", "B 라인", "C 글래스", "D 펀치", "E B+A", "F B+C" };
    // 전투판 대역 (알림이 실제 화면 위에서 읽히는지 보기 위한 배경)
    private const int BoardColumns = 4;
    private const int BoardRows = 6;
    private const float TileSize = 170f;
    private const float TileGap = 14f;
    private static readonly Vector2 BoardCenter = new Vector2(0f, -260f);
    private const float CaptureDuration = 9f;

    [MenuItem("Tools/USW/Fx/Build Center Toast Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[CenterToastLab] 플레이 중에는 실행하지 않는다"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureSettings();
        BuildScene(ScenePath, SettingsPath, StyleButtons, AllStyles, "Temp/FxCapture/CenterToast");
        Debug.Log($"[CenterToastLab] 완료: {ScenePath} (설정 {SettingsPath})");
    }

    private static void EnsureSettings()
    {
        if (AssetDatabase.LoadAssetAtPath<CenterToastSettings>(SettingsPath) != null) return;
        if (!AssetDatabase.IsValidFolder(SettingsFolderParent + "/" + SettingsFolderName))
            AssetDatabase.CreateFolder(SettingsFolderParent, SettingsFolderName);
        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<CenterToastSettings>(), SettingsPath);
        AssetDatabase.SaveAssets();
    }

    /// <summary>실험실 씬 하나를 새로 만들어 저장한다. labels[i] 버튼은 styles[i] 방식(CenterToastStyle 정수)을 띄운다.</summary>
    public static void BuildScene(string scenePath, string settingsPath, string[] labels, int[] styles, string captureFolder)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // 새 씬을 연 뒤에 에셋을 불러온다 (먼저 불러 두면 저장 시 참조가 fileID 0으로 풀림 — PenaltyFxLabBuilder 참고)
        var settings = AssetDatabase.LoadAssetAtPath<CenterToastSettings>(settingsPath);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));
        var modernFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(ModernFontGuid));
        if (modernFont == null) Debug.LogWarning("[CenterToastLab] NotoSansKR-Bold SDF 없음 — 모던 방식도 게임 글꼴로 대체");

        var cam = new GameObject("LabCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = true;
        cam.orthographicSize = 9.6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.13f, 0.19f, 1f); // 전투 필드 대역색
        cam.transform.position = new Vector3(0f, 0f, -10f);
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var canvasGo = new GameObject("LabCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 5f;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0f;
        var canvasRt = (RectTransform)canvasGo.transform;

        BuildBoard(canvasRt);

        // 재사용 컴포넌트: 화면 전체 크기, 가운데 기준
        var toastRt = TimerBonusFxLabBuilder.NewRect("CenterToast", canvasRt, Vector2.zero);
        TimerBonusFxLabBuilder.Stretch(toastRt);
        var toast = toastRt.gameObject.AddComponent<CenterToast>();
        var toastSo = new SerializedObject(toast);
        toastSo.FindProperty("_settings").objectReferenceValue = settings;
        toastSo.FindProperty("_font").objectReferenceValue = font;
        toastSo.FindProperty("_modernFont").objectReferenceValue = modernFont;
        toastSo.ApplyModifiedPropertiesWithoutUndo();

        var status = TimerBonusFxLabBuilder.NewText("Status", canvasRt, font, 26f, Color.white, Vector2.zero);
        status.alignment = TextAlignmentOptions.BottomLeft;
        status.textWrappingMode = TextWrappingModes.Normal;
        status.rectTransform.anchorMin = new Vector2(0f, 0f);
        status.rectTransform.anchorMax = new Vector2(1f, 0f);
        status.rectTransform.pivot = new Vector2(0.5f, 0f);
        status.rectTransform.sizeDelta = new Vector2(-40f, 100f);
        status.rectTransform.anchoredPosition = new Vector2(0f, 16f);

        var lab = new GameObject("CenterToastLab", typeof(CenterToastLab)).GetComponent<CenterToastLab>();
        float step = TimerBonusFxLabBuilder.RowStep;
        var rowStyles = TimerBonusFxLabBuilder.NewButtonRow(canvasRt, "Styles", -16f);
        var styleImages = new Image[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            var b = TimerBonusFxLabBuilder.NewButton(rowStyles, labels[i], font);
            UnityEventTools.AddIntPersistentListener(b.onClick, lab.SelectStyle, styles[i]);
            styleImages[i] = b.GetComponent<Image>();
        }
        var rowMessages = TimerBonusFxLabBuilder.NewButtonRow(canvasRt, "Messages", -16f - step);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowMessages, "식량 부족", font).onClick, lab.ShowFood);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowMessages, "자리 없음", font).onClick, lab.ShowSeat);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowMessages, "합성 불가", font).onClick, lab.ShowMergeFail);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowMessages, "긴 문장", font).onClick, lab.ShowLong);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowMessages, "안내", font).onClick, lab.ShowInfo);
        var rowControls = TimerBonusFxLabBuilder.NewButtonRow(canvasRt, "Controls", -16f - step * 2f);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "같은 문구 연타", font).onClick, lab.TapSpam);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "섞어서 연타", font).onClick, lab.MixedSpam);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "속도", font).onClick, lab.NextSpeed);
        UnityEventTools.AddVoidPersistentListener(TimerBonusFxLabBuilder.NewButton(rowControls, "자동 데모", font).onClick, lab.ToggleAuto);

        var so = new SerializedObject(lab);
        so.FindProperty("_toast").objectReferenceValue = toast;
        so.FindProperty("_settings").objectReferenceValue = settings;
        so.FindProperty("_status").objectReferenceValue = status;
        var buttons = so.FindProperty("_styleButtons");
        buttons.arraySize = styleImages.Length;
        for (int i = 0; i < styleImages.Length; i++) buttons.GetArrayElementAtIndex(i).objectReferenceValue = styleImages[i];
        var order = so.FindProperty("_styleOrder");
        order.arraySize = styles.Length;
        for (int i = 0; i < styles.Length; i++) order.GetArrayElementAtIndex(i).intValue = styles[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        TimerBonusFxLabBuilder.AddCapture(cam, lab, CaptureDuration, captureFolder);
        EditorSceneManager.SaveScene(scene, scenePath);
    }

    // 전투판 대역: 반투명 타일 4×6
    private static void BuildBoard(RectTransform canvasRt)
    {
        var board = TimerBonusFxLabBuilder.NewRect("BoardStandIn", canvasRt, Vector2.zero);
        board.anchoredPosition = BoardCenter;
        float w = BoardColumns * TileSize + (BoardColumns - 1) * TileGap;
        float h = BoardRows * TileSize + (BoardRows - 1) * TileGap;
        for (int r = 0; r < BoardRows; r++)
        for (int c = 0; c < BoardColumns; c++)
        {
            var tile = TimerBonusFxLabBuilder.NewRect($"Tile{r}_{c}", board, Vector2.one * TileSize);
            tile.anchoredPosition = new Vector2(-w * 0.5f + TileSize * 0.5f + c * (TileSize + TileGap), h * 0.5f - TileSize * 0.5f - r * (TileSize + TileGap));
            var img = tile.gameObject.AddComponent<Image>();
            img.color = (r + c) % 3 == 0 ? new Color(0.55f, 0.35f, 0.55f, 0.55f) : new Color(1f, 1f, 1f, 0.08f);
            img.raycastTarget = false;
        }
    }
}
