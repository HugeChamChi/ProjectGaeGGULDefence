using System;
using UnityEngine;

/// <summary>일반 공격에 반응하는 보조 투사체의 SO 설정. 런타임 상태를 보관하지 않는다.</summary>
[Serializable]
public sealed class TotemBonusProjectileSettings
{
    /// <summary>일반 공격 한 번당 발동 확률(0~1).</summary>
    [Range(0f, 1f)] public float Chance = 0.33f;
    /// <summary>공격자 기준 공격력에 곱할 계수.</summary>
    [Min(0f)] public float AttackCoefficient = 0.5f;
    /// <summary>보조 투사체의 외형/이동 구성. 비어 있으면 풀 기본값.</summary>
    public ProjectileData Projectile;
}
