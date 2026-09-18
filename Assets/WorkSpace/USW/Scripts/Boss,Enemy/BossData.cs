using System;
using UnityEngine;

/// <summary>보스별 전투 수치와 체력줄 표시 설정. 연결된 보스는 시트보다 이 데이터를 우선한다.</summary>
[CreateAssetMenu(fileName = "BossData", menuName = "Game/Boss/Boss Data")]
public sealed class BossData : ScriptableObject
{
    [SerializeField] private string _displayName;
    [SerializeField] private GameObject _prefab;
    [SerializeField] private Sprite _icon;
    [SerializeField, Min(1)] private long _maxHp = 3000;
    [SerializeField, Min(0)] private double _defense;
    [SerializeField, Min(1)] private int _hpLineCount = 100;
    [SerializeField, Min(0)] private float _expReward = 100f;

    /// <summary>표시 이름. 미입력 시 에셋 이름.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
    /// <summary>BossBase가 붙은 보스 프리팹. 패턴은 해당 프리팹 설정을 사용한다.</summary>
    public GameObject Prefab => _prefab;
    /// <summary>보스 등장 연출/아이콘.</summary>
    public Sprite Icon => _icon;
    /// <summary>고정소수 전투 시스템의 범위 내 최대 체력.</summary>
    public decimal MaxHp => Math.Max(1m, Math.Min(_maxHp, CombatHealth.MaximumHp));
    /// <summary>보스 기본 방어력.</summary>
    public double Defense => double.IsNaN(_defense) || double.IsInfinity(_defense) ? 0d : Math.Max(0d, _defense);
    /// <summary>체력 비율을 나누어 표시할 전체 줄 수.</summary>
    public int HpLineCount => Mathf.Max(1, _hpLineCount);
    /// <summary>이 보스를 100% 처치했을 때 지급할 총 경험치.</summary>
    public float ExpReward => Mathf.Max(0f, _expReward);
    /// <summary>데미지 1당 지급할 경험치 배율(ExpManager.CalculateExpFromDamage에서 사용).</summary>
    public float ExpMultiplier => MaxHp > 0 ? ExpReward / (float)MaxHp : 0f;

    private void OnValidate()
    {
        _maxHp = (long)MaxHp;
        _defense = Defense;
        _hpLineCount = HpLineCount;
        _expReward = ExpReward;
    }
}
