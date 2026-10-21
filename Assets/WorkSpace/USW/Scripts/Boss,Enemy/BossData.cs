using System;
using UnityEngine;

/// <summary>보스 외형/표시와 고정 스테이지 수치. 난도 공통 엔드리스 HP/방어/줄 수는 별도 진행 설정이 소유한다.</summary>
[CreateAssetMenu(fileName = "BossData", menuName = "Game/Boss/Boss Data")]
public sealed class BossData : ScriptableObject
{
    [SerializeField] private string _displayName;
    [SerializeField] private GameObject _prefab;
    [SerializeField] private Sprite _icon;
    [Header("고정 스테이지/튜토리얼 수치 · 엔드리스에서는 기존 전투 EXP 비율 보존용")]
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
    /// <summary>기존 전투 EXP 총량(선택지 레벨업용). 계정 EXP 보상과 별개다.</summary>
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
