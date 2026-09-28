using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>엑셀 CombatCommon 탭 검증과 GameConfig 적용 창.</summary>
public sealed class CombatCommonWindow : EditorWindow
{
    [SerializeField] private string _path = CombatCommonImporter.DefaultWorkbook;
    private CombatCommonImporter.Plan _preview;
    private string _message = "CombatCommon 탭의 연결된 키만 GameConfig에 적용합니다. (현재: baseCritChancePct)";
    private Vector2 _scroll;

    /// <summary>Import 창을 연다. 씬/Play 상태는 변경하지 않는다.</summary>
    [MenuItem("Tools/USW/Balance/CombatCommon Excel Import")]
    public static void Open() => GetWindow<CombatCommonWindow>("전투 공통 Excel Import");

    private void OnEnable() => minSize = new Vector2(560, 320);

    private void OnGUI()
    {
        EditorGUILayout.LabelField("엑셀 CombatCommon → GameConfig", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("엑셀 저장 → 검증/미리보기 → SO 적용. 대상: " + CombatCommonImporter.GameConfigPath, MessageType.Info);
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
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("엑셀 열기") && File.Exists(_path)) EditorUtility.OpenWithDefaultApp(Path.GetFullPath(_path));
            if (GUILayout.Button("GameConfig 선택")) Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameConfig>(CombatCommonImporter.GameConfigPath);
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("1. 검증 / 변경 내역 보기")) Run(() =>
            {
                _preview = null;
                _preview = CombatCommonImporter.Prepare(_path);
                _message = _preview.Summary;
            });
            using (new EditorGUI.DisabledScope(_preview == null || _preview.Changes.Count == 0))
                if (GUILayout.Button("2. 검증한 변경을 SO에 적용")) Run(() =>
                {
                    string backup = CombatCommonImporter.Apply(_preview);
                    _message = $"적용 완료. 백업: {backup}\n다음 Play부터 새 값을 사용합니다.";
                    _preview = null;
                });
        }
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.SelectableLabel(_message, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(180), GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception error) { _preview = null; _message = error.Message; Debug.LogWarning("[Combat Common] " + error.Message); }
    }
}
