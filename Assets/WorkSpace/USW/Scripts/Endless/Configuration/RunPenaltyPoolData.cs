using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>키로 식별되는 런 패널티 추첨 후보 설정.</summary>
[CreateAssetMenu(fileName = "RunPenaltyPoolData", menuName = "Game/Endless/Run Penalty Pool")]
public sealed class RunPenaltyPoolData : ScriptableObject
{
    [SerializeField] private string _key;
    [SerializeField] private RunPenaltyPoolEntry[] _entries = Array.Empty<RunPenaltyPoolEntry>();

    /// <summary>추첨 풀 고정 키.</summary>
    public string Key => _key;
    /// <summary>참조용 후보 목록. 호출자는 배열/요소를 변경하지 않는다.</summary>
    public IReadOnlyList<RunPenaltyPoolEntry> Entries => _entries;
}
