using System;
using UnityEngine;

/// <summary>족장이 명시적으로 사용하는 카드 목록. 효과 실행 및 런 상태를 보관하지 않는다.</summary>
[CreateAssetMenu(fileName = "LevelUpPoolData", menuName = "Game/LevelUpPoolData")]
public class LevelUpPoolData : ScriptableObject
{
    /// <summary>이름이나 배열 순서와 독립적인 풀 식별자.</summary>
    public int PoolId;

    /// <summary>여러 풀에서 같은 카드 SO를 공유할 수 있다. 중복 등록은 가중치가 아니다.</summary>
    public LevelUpData[] Cards = Array.Empty<LevelUpData>();
}
