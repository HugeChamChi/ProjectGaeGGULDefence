using UnityEngine;

/// <summary>
/// 보스 HP 바 임시 데미지 테스터.
/// Play 모드에서 화면 좌상단 버튼으로 데미지/힐/리셋을 주면
/// UI_BossHpBar 가 갱신되고, 10줄 경계를 넘으면 BossHpBarShake 흔들림도 재생된다.
/// (DOTween 연출과 이벤트 구독은 Play 모드에서만 동작하므로 반드시 Play 후 사용)
/// </summary>
public class BossHpDamageTester : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UI_BossHpBar _hpBar;

    [Header("HP")]
    [SerializeField] private float _maxHp = 10000f;
    [SerializeField] private float _currentHp = 10000f;

    [Header("Damage Amounts")]
    [Tooltip("고정 데미지 버튼 값")]
    [SerializeField] private float _flatDamage = 500f;
    [Tooltip("퍼센트 데미지 버튼 값 (전체 HP 대비 %)")]
    [SerializeField] private float _percentDamage = 5f;

    [Header("Debug UI")]
    [SerializeField] private bool _showOnGuiButtons = true;
    [SerializeField] private float _guiScale = 1.5f;

    private void Start() => Apply();

    /// <summary>현재 값으로 HP 바 갱신.</summary>
    public void Apply()
    {
        if (_hpBar != null)
            _hpBar.SetHp(_currentHp, _maxHp);
    }

    /// <summary>amount 만큼 피해 (음수면 힐).</summary>
    public void DealDamage(float amount)
    {
        _currentHp = Mathf.Clamp(_currentHp - amount, 0f, _maxHp);
        Apply();
    }

    public void DealFlatDamage() => DealDamage(_flatDamage);

    public void DealPercentDamage() => DealDamage(_maxHp * _percentDamage / 100f);

    /// <summary>n 줄만큼 피해 (줄 수는 HP 바의 LineCount 기준).</summary>
    public void DealLines(int lines)
    {
        int lineCount = _hpBar != null ? Mathf.Max(1, _hpBar.LineCount) : 1;
        DealDamage(_maxHp / lineCount * lines);
    }

    public void ResetFull()
    {
        _currentHp = _maxHp;
        Apply();
    }

    public float MaxHp => _maxHp;
    public float CurrentHp => _currentHp;

    /// <summary>디버그 창 등에서 HP 값을 직접 설정하고 즉시 반영.</summary>
    public void SetHpValues(float current, float max)
    {
        _maxHp = Mathf.Max(1f, max);
        _currentHp = Mathf.Clamp(current, 0f, _maxHp);
        Apply();
    }

    private void OnGUI()
    {
        if (!_showOnGuiButtons || _hpBar == null)
            return;

        var prev = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
            new Vector3(_guiScale, _guiScale, 1f));

        // 좌측 하단 배치 (GUI.matrix 스케일 보정)
        const float w = 240f, h = 320f, margin = 10f;
        float y = Screen.height / _guiScale - h - margin;
        GUILayout.BeginArea(new Rect(margin, y, w, h), GUI.skin.box);
        GUILayout.Label($"HP  {_currentHp:0} / {_maxHp:0}");
        GUILayout.Label($"줄  {_hpBar.CurrentLine} / {_hpBar.LineCount}");

        if (GUILayout.Button($"- {_flatDamage:0} 데미지"))
            DealFlatDamage();

        if (GUILayout.Button($"- {_percentDamage:0}% 데미지"))
            DealPercentDamage();

        if (GUILayout.Button("- 1줄"))
            DealLines(1);

        if (GUILayout.Button("- 10줄 (흔들림 확인)"))
            DealLines(10);

        if (GUILayout.Button("풀피 리셋"))
            ResetFull();

        GUILayout.EndArea();

        GUI.matrix = prev;
    }
}
