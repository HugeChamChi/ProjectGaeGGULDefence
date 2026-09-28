using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>보스 엑셀 등록, 변경 검증과 적용 창.</summary>
public sealed class BossBalanceWindow : EditorWindow
{
    [SerializeField] private string _path = BossBalanceImporter.DefaultWorkbook;
    private BossBalanceRegistry _registry;
    private BossBalanceImporter.Plan _preview;
    private string _message = "Boss_스탯만 적용합니다. Stage/무한/패널티/패턴 탭은 적용하지 않습니다.";
    private Vector2 _scroll;

    /// <summary>Import 창을 연다. 씬/Play 상태는 변경하지 않는다.</summary>
    [MenuItem("Tools/USW/Balance/Boss Excel Import")]
    public static void Open() => GetWindow<BossBalanceWindow>("보스 Excel Import");

    private void OnEnable()
    {
        minSize = new Vector2(620, 420);
        _registry = AssetDatabase.LoadAssetAtPath<BossBalanceRegistry>(BossBalanceRegistry.AssetPath);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("엑셀 → BossData", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("엑셀 저장 → 검증/미리보기 → SO 적용. 현재 씬을 열거나 교체하지 않습니다.\n보스 키와 프리팹 키는 등록표에서 관리합니다. 이름을 바꿔도 기존 키를 유지하세요.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _path = EditorGUILayout.TextField("엑셀 경로", _path);
            if (EditorGUI.EndChangeCheck()) _preview = null;
            if (GUILayout.Button("찾기", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFilePanel("GameBalance.xlsx 선택", Directory.GetCurrentDirectory(), "xlsx");
                if (!string.IsNullOrEmpty(path)) { _path = path; _preview = null; }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.ObjectField("등록표", _registry, typeof(BossBalanceRegistry), false);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("기존/신규 에셋 등록")) Run(() =>
            {
                _registry = BossBalanceRegistry.GetOrCreate();
                _registry.RegisterExistingAssets();
                _preview = null;
                _message = $"등록됨: 보스 SO {_registry.Bosses.Count}개 / 프리팹 {_registry.Prefabs.Count}개. 기존 키는 유지합니다.";
            });
            if (GUILayout.Button("등록표 선택") && _registry != null) Selection.activeObject = _registry;
            if (GUILayout.Button("엑셀 열기") && File.Exists(_path)) EditorUtility.OpenWithDefaultApp(Path.GetFullPath(_path));
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("1. 검증 / 변경 내역 보기")) Run(() =>
            {
                _preview = null;
                _preview = BossBalanceImporter.Prepare(_path, _registry);
                _message = _preview.Summary;
            });
            using (new EditorGUI.DisabledScope(_preview == null || _preview.Changes.Count == 0))
                if (GUILayout.Button("2. 검증한 변경을 SO에 적용")) Run(() =>
                {
                    string backup = BossBalanceImporter.Apply(_preview, _registry);
                    _message = $"적용 완료. 백업: {backup}\n다음 보스 생성부터 새 값을 사용합니다. 실행 전 적용을 권장합니다.";
                    _preview = null;
                });
        }
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.SelectableLabel(_message, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(250), GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception error) { _preview = null; _message = error.Message; Debug.LogWarning("[Boss Balance] " + error.Message); }
    }
}
