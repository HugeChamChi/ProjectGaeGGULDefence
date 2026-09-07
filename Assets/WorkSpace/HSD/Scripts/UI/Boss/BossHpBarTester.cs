using UnityEngine;

/// <summary>
/// UI_BossHpBar 테스트용. 인스펙터에서 값 변경 시 즉시 HP 바/패턴/텍스트/색상이 갱신된다.
/// </summary>
public class BossHpBarTester : MonoBehaviour
{
    [SerializeField] private UI_BossHpBar _hpBar;

    [Header("Test Values")]
    [SerializeField] private float _maxHp = 10000f;
    [SerializeField] private float _currentHp = 6461f;
    
    private void Start() => Apply();

    [ContextMenu("Apply HP")]
    private void Apply()
    {
        if (_hpBar != null) _hpBar.SetHp(_currentHp, _maxHp);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_hpBar != null) _hpBar.SetHp(_currentHp, _maxHp);
    }
#endif
}
