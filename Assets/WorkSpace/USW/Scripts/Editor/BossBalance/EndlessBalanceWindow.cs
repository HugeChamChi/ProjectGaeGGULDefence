using System;
using UnityEditor;
using UnityEngine;

/// <summary>무한모드 설정 엑셀을 검증하고 SO 묶음으로 적용하는 에디터 창.</summary>
public sealed class EndlessBalanceWindow : EditorWindow
{
    [SerializeField] private string _path = BossBalanceImporter.DefaultWorkbook;
    private EndlessBalanceImporter.Plan _plan;
    private string _message = "미정 설정은 비활성 초안으로 저장합니다. 활성 프로필은 모든 검증을 통과해야 합니다.";
    private Vector2 _scroll;

    /// <summary>씬 변경 없이 무한 설정 Import 창을 연다.</summary>
    [MenuItem("Tools/USW/Balance/Endless Excel Import")]
    public static void Open() => GetWindow<EndlessBalanceWindow>("무한 Excel Import");

    private void OnEnable() => minSize = new Vector2(720, 420);
    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Stage_난이도·무한·보스순환·보스풀·패널티·패널티풀을 함께 검증합니다.\n보스순환은 첫 고정 배치와 이후 분류 순서, 보스풀은 그 이후의 후보 목록입니다.\n적용 후 사용할 프로필을 인게임 진행 설정에 연결하세요.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUI.BeginChangeCheck();
            _path = EditorGUILayout.TextField("엑셀 경로", _path);
            if (EditorGUI.EndChangeCheck()) _plan = null;
            if (GUILayout.Button("1. 검증 / 변경 내역 보기")) Run(() =>
            {
                _plan = null;
                _plan = EndlessBalanceImporter.Prepare(_path, AssetDatabase.LoadAssetAtPath<BossBalanceRegistry>(BossBalanceRegistry.AssetPath));
                _message = _plan.Summary;
            });
            using (new EditorGUI.DisabledScope(_plan == null || _plan.Changes == 0))
                if (GUILayout.Button("2. 검증한 SO 묶음 적용")) Run(() =>
                {
                    _message = "저장 완료. 백업: " + EndlessBalanceImporter.Apply(_plan, AssetDatabase.LoadAssetAtPath<BossBalanceRegistry>(BossBalanceRegistry.AssetPath));
                    _plan = null;
                });
        }
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.SelectableLabel(_message, EditorStyles.wordWrappedLabel, GUILayout.Height(Mathf.Max(600, _message.Split('\n').Length * 20)));
        EditorGUILayout.EndScrollView();
    }
    private void Run(Action action)
    { try { action(); } catch (Exception error) { _plan = null; _message = error.Message; Debug.LogWarning("[Endless Import] " + error.Message); } }
}
