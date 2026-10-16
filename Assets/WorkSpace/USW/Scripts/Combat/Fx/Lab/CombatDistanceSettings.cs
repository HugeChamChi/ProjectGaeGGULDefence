using UnityEngine;

/// <summary>실제 전투 거리 비교 씬 전용 설정. 본게임 밸런스에는 사용하지 않는다.</summary>
[CreateAssetMenu(menuName = "USW/Fx/Combat Distance Settings")]
public sealed class CombatDistanceSettings : ScriptableObject
{
    /// <summary>비교할 기존 보스 데이터.</summary>
    public BossData Boss;
    /// <summary>같은 배치에서 반복 사용할 기존 유닛 데이터.</summary>
    public UnitData[] Units;
    /// <summary>고정 배치 셀 좌표.</summary>
    public Vector2Int[] Cells;
    /// <summary>모든 비교에 공통인 유닛 등급.</summary>
    public Tier SpawnTier = Tier.Legend;
    /// <summary>현재, 조금 가까움, 많이 가까움 순서의 보스 이동량.</summary>
    public float[] Approach = { 0f, .65f, 1.25f };
    /// <summary>배경에서 아래 그리드를 고정하고 위 발판을 당길 사이 구간의 월드 Y.</summary>
    public Vector2 BackgroundGap = new Vector2(1f, 2.5f);
    /// <summary>비교 중 보스 교체를 막는 테스트용 체력.</summary>
    public long PreviewHealth = 1000000000000;
    /// <summary>씬을 다시 실행할 때의 랜덤 초기값.</summary>
    public int Seed = 17414;
}
