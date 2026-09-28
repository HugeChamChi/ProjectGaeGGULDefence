using System;
using System.Collections.Generic;

/// <summary>v1: 트리 키와 노드 Id/레벨만 저장한다. 밸런스 값은 SO에서 읽는다.</summary>
[Serializable]
public sealed class ResearchSaveData
{
    /// <summary>현재 지원하는 저장 구조 버전.</summary>
    public const int CurrentVersion = 1;
    /// <summary>구조 버전. 밸런스 수정 횟수가 아니다.</summary>
    public int SchemaVersion = CurrentVersion;
    /// <summary>고정 트리 키.</summary>
    public string TreeKey;
    /// <summary>노드별 진행. 미지의 노드도 향후 복원을 위해 보존한다.</summary>
    public List<Entry> Entries = new();
    /// <summary>JSON에 직렬화되는 노드 진행 한 개.</summary>
    [Serializable]
    public sealed class Entry
    {
        /// <summary>트리 내부 고정 Id.</summary>
        public string Id;
        /// <summary>획득한 레벨. 최대 레벨 변경 시에도 저장 원값을 보존한다.</summary>
        public int Level;
    }
    /// <summary>변경 가능한 진행 사전과 분리된 저장 스냅샷을 만든다.</summary>
    public static ResearchSaveData Capture(string treeKey, IReadOnlyDictionary<string, int> levels)
    {
        var data = new ResearchSaveData { TreeKey = treeKey };
        foreach (var pair in levels) data.Entries.Add(new Entry { Id = pair.Key, Level = pair.Value });
        data.Entries.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        return data;
    }
    /// <summary>저장 형식/트리/중복/범위를 확인하고 독립 사전으로 반환한다.</summary>
    public Dictionary<string, int> GetLevels(string treeKey)
    {
        if (SchemaVersion != CurrentVersion || TreeKey != treeKey || Entries == null)
            throw new System.IO.InvalidDataException("지원하지 않는 강화 저장 버전 또는 트리입니다.");
        var levels = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entry in Entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || entry.Level < 0 || levels.ContainsKey(entry.Id))
                throw new System.IO.InvalidDataException("강화 저장에 잘못된 노드 진행이 있습니다.");
            levels.Add(entry.Id, entry.Level);
        }
        return levels;
    }
}
