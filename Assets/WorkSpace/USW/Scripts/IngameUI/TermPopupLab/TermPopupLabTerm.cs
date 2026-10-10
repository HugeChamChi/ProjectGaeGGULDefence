using System;
using UnityEngine;

/// <summary>용어 팝업 실험실(FxLab_TermPopup)에서 쓰는 용어 한 개의 표시 문구. 실험실 전용 예시 데이터다.</summary>
[Serializable]
public sealed class TermPopupLabTerm
{
    [Tooltip("카드 설명의 [[용어]] 표기와 같은 이름")]
    public string Id;
    [Tooltip("디버프 / 유닛 / 규칙 등 분류 배지")]
    public string Kind;
    public Color KindColor = new Color(0.42f, 0.44f, 0.53f);
    [Tooltip("A 말풍선 + 자세히 모드에서 말풍선에 쓰는 한두 줄 요약")]
    [TextArea] public string Short;
    [Tooltip("시트와 A 단독 모드에서 쓰는 전체 설명")]
    [TextArea] public string Body;
    [Tooltip("수치 한 줄 (비우면 숨김). 예: 최대 5중첩 · 지속 4초")]
    public string Facts;
}
