using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VContainer.Unity;

/// <summary>
/// 데미지 표시 비교 테스트(DamageStyleLab) 세팅 도구.
/// - Setup: 폰트별 외곽선 머티리얼과 설정 SO를 만들고, TotemSelectTest 씬의 테스트 캔버스를 데미지 테스트 패널로 바꾼다.
/// - 플레이 중 도구: 샘플 재생, 방식/폰트 넘기기, 게임 화면 캡처 (검증용).
/// </summary>
public static class DamageStyleLabSetup
{
    private const string DataDir = "Assets/WorkSpace/USW/Data/UI/DamageStyleLab";
    private const string SettingsPath = DataDir + "/DamageStyleLabSettings.asset";
    private const string ScenePath = "Assets/WorkSpace/USW/TotemSelectTest.unity";
    private const string OldCanvasName = "TotemSelectTestCanvas";
    private const string CanvasName = "DamageStyleTestCanvas";
    private const string PanelFontPath = "Assets/Imports/Font/KCC-Ganpan SDF.asset";
    private const string EvidenceDir = "production/qa/evidence";

    private static readonly (string Name, string Path, string MaterialName)[] Fonts =
    {
        ("Lilita One", "Assets/Imports/Font/LilitaOne-Regular SDF.asset", "DamageLab_LilitaOne"),
        ("Bangers", "Assets/Imports/Font/Bangers-Regular SDF.asset", "DamageLab_Bangers"),
        ("Black Han Sans", "Assets/Imports/Font/BlackHanSans-Regular SDF.asset", "DamageLab_BlackHanSans"),
    };

    [MenuItem("Tools/USW/Damage Style Lab/Setup TotemSelectTest")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[DamageStyleLabSetup] 플레이 중에는 실행하지 않는다"); return; }
        Directory.CreateDirectory(DataDir);

        var entries = new DamageStyleLabSettings.FontEntry[Fonts.Length];
        for (int i = 0; i < Fonts.Length; i++)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonts[i].Path);
            if (font == null) { Debug.LogError($"[DamageStyleLabSetup] 폰트 없음: {Fonts[i].Path}"); return; }
            entries[i] = new DamageStyleLabSettings.FontEntry
            {
                DisplayName = Fonts[i].Name,
                Font = font,
                Material = CreateOrUpdateMaterial(font, $"{DataDir}/{Fonts[i].MaterialName}.mat"),
            };
        }

        var settings = AssetDatabase.LoadAssetAtPath<DamageStyleLabSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<DamageStyleLabSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        settings.Fonts = entries;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);

        var go = GameObject.Find(CanvasName) ?? GameObject.Find(OldCanvasName);
        if (go == null) { Debug.LogError("[DamageStyleLabSetup] 테스트 캔버스를 찾지 못함"); return; }
        go.name = CanvasName;
        foreach (var mb in go.GetComponents<MonoBehaviour>())
            if (mb != null && mb.GetType().Name == "TotemSelectTestPanel") Object.DestroyImmediate(mb);

        var lab = go.GetComponent<DamageStyleLab>() ?? go.AddComponent<DamageStyleLab>();
        var labSo = new SerializedObject(lab);
        labSo.FindProperty("_settings").objectReferenceValue = settings;
        labSo.ApplyModifiedPropertiesWithoutUndo();

        var panel = go.GetComponent<DamageStyleLabPanel>() ?? go.AddComponent<DamageStyleLabPanel>();
        var panelSo = new SerializedObject(panel);
        panelSo.FindProperty("_lab").objectReferenceValue = lab;
        panelSo.FindProperty("_font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PanelFontPath);
        panelSo.ApplyModifiedPropertiesWithoutUndo();

        var scope = Object.FindAnyObjectByType<LifetimeScope>();
        if (scope == null) { Debug.LogError("[DamageStyleLabSetup] LifetimeScope 없음"); return; }
        var scopeSo = new SerializedObject(scope);
        var list = scopeSo.FindProperty("autoInjectGameObjects");
        bool found = false;
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == go) found = true;
        if (!found)
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = go;
        }
        scopeSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[DamageStyleLabSetup] 완료: 머티리얼 3개, 설정 SO, 씬 캔버스 교체, 자동 주입 등록");
    }

    // 폰트 기본 머티리얼을 복사해 두꺼운 외곽선 + 아래쪽 그림자를 넣는다.
    // UNDERLAY_ON은 shader_feature라 에셋으로 저장해야 빌드에 변형이 포함된다.
    private static Material CreateOrUpdateMaterial(TMP_FontAsset font, string path)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(font.material);
            AssetDatabase.CreateAsset(mat, path);
        }
        else mat.CopyPropertiesFromMaterial(font.material);

        mat.SetFloat("_FaceDilate", 0.12f);
        mat.SetFloat("_OutlineWidth", 0.24f);
        mat.SetColor("_OutlineColor", new Color(0.18f, 0.06f, 0.03f, 1f));
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.55f));
        mat.SetFloat("_UnderlayOffsetX", 0.55f);
        mat.SetFloat("_UnderlayOffsetY", -0.65f);
        mat.SetFloat("_UnderlayDilate", 0.25f);
        mat.SetFloat("_UnderlaySoftness", 0.15f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    [MenuItem("Tools/USW/Damage Style Lab/Play Sample")]
    public static void PlaySample() => WithLab(lab => lab.PlaySample());

    [MenuItem("Tools/USW/Damage Style Lab/Next Style")]
    public static void NextStyle() => WithLab(lab => { lab.NextStyle(); Debug.Log($"[DamageStyleLab] 방식: {lab.StyleName}"); });

    [MenuItem("Tools/USW/Damage Style Lab/Next Font")]
    public static void NextFont() => WithLab(lab => { lab.NextFont(); Debug.Log($"[DamageStyleLab] 폰트: {lab.FontName}"); });

    [MenuItem("Tools/USW/Damage Style Lab/Capture Game View")]
    public static void Capture() => WithLab(lab =>
    {
        Directory.CreateDirectory(EvidenceDir);
        string file = $"{EvidenceDir}/damage-style-{lab.CurrentStyle}-{lab.FontName.Replace(" ", "")}-{System.DateTime.Now:HHmmss}.png";
        ScreenCapture.CaptureScreenshot(file);
        Debug.Log($"[DamageStyleLab] 캡처: {file}");
    });

    private const double SampleCaptureDelay = 1.0;

    /// <summary>검증용: 글자가 "시작"인 버튼을 누른다 (보스 등장 확인).</summary>
    [MenuItem("Tools/USW/Damage Style Lab/Press Start Button")]
    public static void PressStart() => WithLab(_ =>
    {
        foreach (var button in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label == null || label.text.Trim() != "시작") continue;
            button.onClick.Invoke();
            Debug.Log("[DamageStyleLab] 시작 버튼 누름");
            return;
        }
        Debug.LogWarning("[DamageStyleLab] 시작 버튼 없음");
    });

    /// <summary>검증용: SampleCaptureDelay초 뒤 캡처만 한다 (실제 전투 화면).</summary>
    [MenuItem("Tools/USW/Damage Style Lab/Capture Later")]
    public static void CaptureLater() => WithLab(_ =>
    {
        double at = EditorApplication.timeSinceStartup + SampleCaptureDelay;
        void Tick()
        {
            if (EditorApplication.timeSinceStartup < at) return;
            EditorApplication.update -= Tick;
            if (EditorApplication.isPlaying) Capture();
        }
        EditorApplication.update += Tick;
    });

    /// <summary>샘플을 재생하고 SampleCaptureDelay초 뒤에 캡처한다 (명령 사이 지연으로 타이밍을 놓치지 않게).</summary>
    [MenuItem("Tools/USW/Damage Style Lab/Sample And Capture")]
    public static void SampleAndCapture() => WithLab(lab =>
    {
        lab.PlaySample();
        double at = EditorApplication.timeSinceStartup + SampleCaptureDelay;
        void Tick()
        {
            if (EditorApplication.timeSinceStartup < at) return;
            EditorApplication.update -= Tick;
            if (!EditorApplication.isPlaying) return;
            int visible = 0;
            foreach (var t in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
                if (t.transform.parent != null && t.transform.parent.parent != null && t.transform.parent.parent.name == "DamageStyleLab") visible++;
            Debug.Log($"[DamageStyleLab] 떠 있는 숫자 {visible}개");
            Capture();
        }
        EditorApplication.update += Tick;
    });

    /// <summary>
    /// 검증용: 플레이 중 설정값을 바꿔도 바로 반영되는지 본다.
    /// 현재 값으로 캡처 → MobiOffset.y +300, 글자 크기 1.5배로 바꿔 캡처 → 원래 값으로 되돌린다.
    /// </summary>
    [MenuItem("Tools/USW/Damage Style Lab/Verify Live Tuning")]
    public static void VerifyLiveTuning() => WithLab(lab =>
    {
        var s = AssetDatabase.LoadAssetAtPath<DamageStyleLabSettings>(SettingsPath);
        var offset = s.MobiOffset;
        float mobiSize = s.MobiFontSize, cookieSize = s.CookieFontSize;
        lab.PlaySample();
        double t0 = EditorApplication.timeSinceStartup;
        int step = 0;
        void Tick()
        {
            double t = EditorApplication.timeSinceStartup - t0;
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; Restore(); return; }
            if (step == 0 && t > 1.0) { Capture(); step++; }
            else if (step == 1 && t > 1.2)
            {
                s.MobiOffset = offset + new Vector2(0f, 300f);
                s.MobiFontSize = mobiSize * 1.5f;
                s.CookieFontSize = cookieSize * 1.5f;
                step++;
            }
            else if (step == 2 && t > 2.2) { Capture(); step++; }
            else if (step == 3 && t > 2.4) { EditorApplication.update -= Tick; Restore(); }
        }
        void Restore()
        {
            s.MobiOffset = offset;
            s.MobiFontSize = mobiSize;
            s.CookieFontSize = cookieSize;
            Debug.Log("[DamageStyleLab] 검증 끝, 설정값 원래대로");
        }
        EditorApplication.update += Tick;
    });

    private static void WithLab(System.Action<DamageStyleLab> action)
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[DamageStyleLab] 플레이 중에만 쓸 수 있다"); return; }
        var lab = Object.FindAnyObjectByType<DamageStyleLab>();
        if (lab == null) { Debug.LogError("[DamageStyleLab] 씬에 DamageStyleLab 없음"); return; }
        action(lab);
    }
}
