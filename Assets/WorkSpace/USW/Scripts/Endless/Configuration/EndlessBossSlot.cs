using System;
using UnityEngine;

/// <summary>1부터 시작하는 순환 위치와 보스 SO. 키는 Import 시 직접 참조로 변환한다.</summary>
[Serializable]
public sealed class EndlessBossSlot
{
    [SerializeField] private int _position;
    [SerializeField] private EndlessBossRole _role;
    [SerializeField] private string _bossKey;
    [SerializeField] private BossData _boss;

    /// <summary>순환 안에서의 1 기반 위치.</summary>
    public int Position => _position;
    /// <summary>위치의 난도 역할.</summary>
    public EndlessBossRole Role => _role;
    /// <summary>Import 당시의 보스 키. 진단용이며 런타임 검색에는 Boss 참조를 사용한다.</summary>
    public string BossKey => _bossKey;
    /// <summary>기본 보스 SO. 런 성장값으로 덮어쓰지 않는다.</summary>
    public BossData Boss => _boss;
}
