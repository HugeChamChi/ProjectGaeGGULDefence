using UnityEngine;

/// <summary>인스펙터에서 이 필드의 라벨을 한국어로 표시한다.</summary>
public class KoreanLabelAttribute : PropertyAttribute
{
    public string Label { get; }
    public KoreanLabelAttribute(string label) => Label = label;
}
