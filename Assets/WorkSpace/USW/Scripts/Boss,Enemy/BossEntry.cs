using System;
using UnityEngine;

/// <summary>웨이브의 보스 한 자리. BossData 연결 시 공용 데이터를 사용하고 기존 인라인 데이터도 지원한다.</summary>
[Serializable]
public class BossEntry
{
    /// <summary>보스 전용 SO. 지정하면 HP/방어력은 시트보다 이 SO를 우선한다.</summary>
    public BossData Data;
    /// <summary>BossData 미연결 웨이브의 기존 아이콘.</summary>
    public Sprite bossIcon;
    /// <summary>BossData 미연결 웨이브의 기존 프리팹.</summary>
    public GameObject prefab;
    /// <summary>BossData 미연결 및 시트 미로드 시 사용하는 기존 HP.</summary>
    public int hp = 500;
    /// <summary>BossData 미연결 및 시트 미로드 시 사용하는 기존 방어력.</summary>
    [Min(0)] public double Defense;
    /// <summary>BossData 미연결 및 시트 미로드 시 사용하는 기존 경험치 지급량(100% 처치 기준).</summary>
    [Min(0)] public float ExpReward = 100f;

    /// <summary>실제로 소환할 프리팹.</summary>
    public GameObject Prefab => Data != null ? Data.Prefab : prefab;
    /// <summary>실제로 표시할 아이콘.</summary>
    public Sprite Icon => Data != null ? Data.Icon : bossIcon;
    /// <summary>기본 최대 HP. BossData 미연결 시에만 소환기가 시트로 덮어쓸 수 있다.</summary>
    public decimal MaxHp => Data != null ? Data.MaxHp : hp;
    /// <summary>기본 방어력.</summary>
    public double BaseDefense => Data != null ? Data.Defense : Defense;
    /// <summary>보스별 전체 줄 수. 0은 기존 HP 바의 설정 유지.</summary>
    public int HpLineCount => Data != null ? Data.HpLineCount : 0;
    /// <summary>데미지 1당 지급 경험치 배율.</summary>
    public float ExpMultiplier => Data != null ? Data.ExpMultiplier : (MaxHp > 0 ? ExpReward / (float)MaxHp : 0f);
}
