using UnityEngine;

/// <summary>재사용할 패턴 목록과 비교 씬 재생 설정.</summary>
[CreateAssetMenu(menuName = "Game/Boss/Pattern Catalog")]
public sealed class PatternLabCatalog : ScriptableObject
{
    /// <summary>수동 선택·자동 순회에 사용할 패턴 SO 목록.</summary>
    public PatternLabData[] Patterns;
    /// <summary>자동 순회에서 패턴 사이의 전투 시간 간격.</summary>
    [Min(.1f)] public float CycleGap = 1.1f;
    /// <summary>비교 중 체력바를 유지하는 조우 전용 체력.</summary>
    [Min(1)] public long PreviewHp = 1000000000;
}
