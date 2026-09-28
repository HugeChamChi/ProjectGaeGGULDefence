using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>강화 수치의 검증/미리보기/백업/적용 창.</summary>
public sealed class ResearchBalanceWindow : EditorWindow
{
    [SerializeField] private string _path = ResearchBalanceImporter.DefaultWorkbook;
    private ResearchBalanceImporter.Plan _preview;
    private string _message = "엑셀을 저장한 뒤 검증하세요.";
    private Vector2 _scroll;
    /// <summary>강화 Import 도구를 연다.</summary>
    [MenuItem("Tools/USW/Balance/OutgameUpgrade Excel Import")]
    public static void Open() => GetWindow<ResearchBalanceWindow>("영구 강화 Excel Import");
    private void OnEnable() => minSize = new Vector2(720, 460);
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Account_영구강화 → 강화 트리", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("TreeKey + NodeId로 숫자만 적용합니다. Pct 2는 SO 0.02, 화면 +2%입니다.\n스탯·선행 노드·비용·최대 증가·주석은 Import하지 않습니다. 현재 SO의 모든 노드 행이 필요합니다.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUI.BeginChangeCheck();
            _path = EditorGUILayout.TextField("엑셀 경로", _path);
            if (EditorGUI.EndChangeCheck()) _preview = null;
            if (GUILayout.Button("엑셀 선택"))
            {
                string path = EditorUtility.OpenFilePanel("GameBalance.xlsx", Directory.GetCurrentDirectory(), "xlsx");
                if (!string.IsNullOrEmpty(path)) { _path = path; _preview = null; }
            }
            if (GUILayout.Button("1. 검증 / 변경 미리보기")) Run(() => { _preview = ResearchBalanceImporter.Prepare(_path); _message = _preview.Summary; });
            using (new EditorGUI.DisabledScope(_preview == null || _preview.ChangeCount == 0))
                if (GUILayout.Button("2. 백업 후 적용")) Run(() => { _message = "적용 완료: " + ResearchBalanceImporter.Apply(_preview); _preview = null; });
        }
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.SelectableLabel(_message, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(300), GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }
    private void Run(Action action)
    {
        try { action(); }
        catch (Exception error) { _preview = null; _message = error.Message; Debug.LogWarning("[Research Import] " + error.Message); }
    }
}
