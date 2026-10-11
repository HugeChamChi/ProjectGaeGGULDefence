using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 선택지 등에서 누르면 설명이 뜨는 용어 모음. 문구·별칭·수치 출처·영상의 원본이다.
/// 처음 생성은 Tools/USW/UI/Create Term Glossary, 이후 수정은 이 에셋에서 직접 한다.
/// </summary>
[CreateAssetMenu(fileName = "TermGlossary", menuName = "Game/UI/Term Glossary")]
public sealed class TermGlossary : ScriptableObject
{
    [SerializeField] private List<TermEntry> _entries = new List<TermEntry>();

    /// <summary>모든 용어.</summary>
    public IReadOnlyList<TermEntry> Entries => _entries;

    /// <summary>링크 ID로 찾는다.</summary>
    public bool TryFind(string id, out TermEntry entry)
    {
        foreach (var candidate in _entries)
        {
            if (candidate != null && candidate.Id == id) { entry = candidate; return true; }
        }
        entry = null;
        return false;
    }

#if UNITY_EDITOR
    /// <summary>편집기 생성 도구 전용: 같은 Id가 없을 때만 추가한다 (이미 있는 문구는 덮어쓰지 않는다).</summary>
    public bool AddIfMissing(TermEntry entry)
    {
        if (entry == null || TryFind(entry.Id, out _)) return false;
        _entries.Add(entry);
        return true;
    }
#endif
}
