using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>분류 Excel에서 명시적으로 가져온 풀별 ID→태그 목록. 효과나 추천 규칙은 보관하지 않는다.</summary>
public sealed class DroneChoiceConceptCatalog : ScriptableObject
{
    /// <summary>이 카탈로그가 지원하는 분류 스키마.</summary>
    public const int SupportedSchemaVersion = 1;

    /// <summary>고정 선택지 ID와 저작된 복수 태그.</summary>
    [Serializable]
    public struct Entry
    {
        public int ChooseId;
        public DroneChoiceConcept Tags;
    }

    [SerializeField, HideInInspector] private int _poolId;
    [SerializeField, HideInInspector] private int _schemaVersion;
    [SerializeField, HideInInspector] private string _dataVersion;
    [SerializeField, HideInInspector] private string _sourceWorkbookSha256;
    [SerializeField, HideInInspector] private string _contentHash;
    [SerializeField, HideInInspector] private Entry[] _entries = Array.Empty<Entry>();

    /// <summary>분류 대상 선택 풀 ID.</summary>
    public int PoolId => _poolId;
    /// <summary>가져온 스키마 버전.</summary>
    public int SchemaVersion => _schemaVersion;
    /// <summary>사람이 관리하는 데이터 버전.</summary>
    public string DataVersion => _dataVersion;
    /// <summary>가져온 Excel 원본 바이트의 SHA-256.</summary>
    public string SourceWorkbookSha256 => _sourceWorkbookSha256;
    /// <summary>풀 ID와 ID순/고정 태그순 분류 내용의 SHA-256.</summary>
    public string ContentHash => _contentHash;
    /// <summary>ID순으로 저장한 분류 행.</summary>
    public IReadOnlyList<Entry> Entries => _entries;

    /// <summary>ID로만 조회한다. 누락을 공용 태그로 추정하지 않는다.</summary>
    public bool TryGetTags(int chooseId, out DroneChoiceConcept tags)
    {
        foreach (var entry in _entries)
            if (entry.ChooseId == chooseId) { tags = entry.Tags; return true; }
        tags = DroneChoiceConcept.None;
        return false;
    }

#if UNITY_EDITOR
    /// <summary>검증된 Editor Importer 산출물을 복사한다. 플레이어에는 쓰기 API를 포함하지 않는다.</summary>
    public void SetImportedData(int poolId, int schemaVersion, string dataVersion, string sourceSha256,
        string contentHash, Entry[] entries)
    {
        _poolId = poolId; _schemaVersion = schemaVersion; _dataVersion = dataVersion;
        _sourceWorkbookSha256 = sourceSha256; _contentHash = contentHash;
        _entries = (Entry[])entries.Clone();
    }
#endif
}
