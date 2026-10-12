using System;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 용어 사전의 항목 하나. 카드 설명에서 <see cref="Aliases"/> 중 하나가 나오면 링크가 걸리고,
/// 누르면 바텀 시트에 설명·수치가, 영상이 있으면 시트 위에 필름 영상이 뜬다.
/// </summary>
[Serializable]
public sealed class TermEntry
{
    [Tooltip("바뀌지 않는 키 (링크 ID). 예: chief_skill")]
    public string Id;
    [Tooltip("시트 제목")]
    public string DisplayName;
    [Tooltip("카드 설명에서 이 용어로 인식할 표기들. 긴 표기가 먼저 맞는다")]
    public string[] Aliases = Array.Empty<string>();
    /// <summary>Other glossary entries linked from this explanation's body.</summary>
    public string[] RelatedTerms = Array.Empty<string>();
    [Tooltip("분류 배지 글자 (족장 / 유닛 / 자원 / 규칙 …)")]
    public string Kind;
    public Color KindColor = new Color(0.42f, 0.44f, 0.53f);
    [Tooltip("기본 설명. 등급별 표·세부 규칙은 넣지 않는다")]
    [TextArea(3, 8)] public string Body;

    /// <summary>Short authored explanation. Empty entries retain the original body.</summary>
    [TextArea(1, 4)] public string BriefBody;
    /// <summary>Detailed authored explanation. Empty entries retain the original body.</summary>
    [TextArea(2, 8)] public string DetailedBody;

    /// <summary>Resolves a mode without truncating rules or changing the original entry.</summary>
    public string GetBody(DescriptionMode mode)
    {
        string requested = mode == DescriptionMode.Detailed ? DetailedBody : BriefBody;
        return string.IsNullOrWhiteSpace(requested) ? Body ?? string.Empty : TermFacts.FormatBody(requested, FactSource);
    }

    [Header("수치 한 줄 (전투와 같은 SO에서 읽음)")]
    public TermFactKind FactKind;
    [Tooltip("FactKind에 맞는 SO (AlphanSkillData / 베탕 UnitData / DroneHackingData / DisigmanData)")]
    public ScriptableObject FactSource;

    [Header("영상 (움직임이 핵심인 용어만)")]
    public VideoClip Clip;
    [Tooltip("필름 아래 자막. 비우면 DisplayName")]
    public string ClipCaption;

    /// <summary>시트에 표시할 수치 한 줄. 출처가 없거나 맞지 않으면 빈 문자열.</summary>
    public string BuildFacts() => TermFacts.Build(FactKind, FactSource);

    /// <summary>필름 자막.</summary>
    public string Caption => string.IsNullOrEmpty(ClipCaption) ? DisplayName : ClipCaption;
}
