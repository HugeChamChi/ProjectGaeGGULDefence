using UnityEngine;

/// <summary>
/// 보스 HP 바 설정용 디버그 창 (화면 우측 하단).
/// 최대 HP / 현재 HP / 줄 수를 입력해서 적용한다.
/// 좌측 하단 데미지 테스터(BossHpDamageTester)와는 별개 창.
/// </summary>
public class BossHpDebugWindow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UI_BossHpBar _hpBar;

    [Tooltip("연결하면 HP 값을 테스터와 동기화 (좌측 데미지 버튼과 값 공유)")]
    [SerializeField] private BossHpDamageTester _damageTester;

    [Header("Debug UI")]
    [SerializeField] private bool _show = true;
    [SerializeField] private float _guiScale = 1.5f;

    [Header("Defaults")]
    [SerializeField] private float _defaultMaxHp = 10000f;
    [SerializeField] private int _defaultLineCount = 100;

    private string _maxHpStr;
    private string _currentHpStr;
    private string _lineCountStr;
    private bool _inited;

    private void OnEnable()
    {
        // 현재 값으로 입력칸 초기화
        float max = _damageTester != null ? _damageTester.MaxHp : _defaultMaxHp;
        float cur = _damageTester != null ? _damageTester.CurrentHp : max;
        int lines = _hpBar != null ? _hpBar.LineCount : _defaultLineCount;

        _maxHpStr = max.ToString("0");
        _currentHpStr = cur.ToString("0");
        _lineCountStr = lines.ToString();
        _inited = true;
    }

    private void OnGUI()
    {
        if (!_show || _hpBar == null || !_inited)
            return;

        var prev = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
            new Vector3(_guiScale, _guiScale, 1f));

        // 우측 하단 배치 (GUI.matrix 스케일 보정)
        const float w = 240f, h = 260f, margin = 10f;
        float x = Screen.width / _guiScale - w - margin;
        float y = Screen.height / _guiScale - h - margin;

        GUILayout.BeginArea(new Rect(x, y, w, h), GUI.skin.box);
        GUILayout.Label("=== Boss HP 설정 ===");

        GUILayout.Label("최대 HP");
        _maxHpStr = GUILayout.TextField(_maxHpStr);

        GUILayout.Label("현재 HP");
        _currentHpStr = GUILayout.TextField(_currentHpStr);

        GUILayout.Label("줄 수 (Line Count)");
        _lineCountStr = GUILayout.TextField(_lineCountStr);

        GUILayout.Space(6f);

        if (GUILayout.Button("적용"))
            Apply();

        if (GUILayout.Button("현재 HP = 최대로"))
        {
            _currentHpStr = _maxHpStr;
            Apply();
        }

        GUILayout.EndArea();

        GUI.matrix = prev;
    }

    private void Apply()
    {
        float max = ParseFloat(_maxHpStr, _defaultMaxHp);
        float cur = ParseFloat(_currentHpStr, max);
        int lines = ParseInt(_lineCountStr, _defaultLineCount);

        max = Mathf.Max(1f, max);
        cur = Mathf.Clamp(cur, 0f, max);
        lines = Mathf.Max(1, lines);

        // 입력값 정규화해서 다시 표시
        _maxHpStr = max.ToString("0");
        _currentHpStr = cur.ToString("0");
        _lineCountStr = lines.ToString();

        // 줄 수 먼저 → HP 적용 (새 줄 수 기준으로 색/줄 재계산)
        _hpBar.SetLineCount(lines);

        if (_damageTester != null)
            _damageTester.SetHpValues(cur, max);   // 테스터와 값 동기화
        else
            _hpBar.SetHp(cur, max);
    }

    private static float ParseFloat(string s, float fallback)
        => float.TryParse(s, out float v) ? v : fallback;

    private static int ParseInt(string s, int fallback)
        => int.TryParse(s, out int v) ? v : fallback;
}
