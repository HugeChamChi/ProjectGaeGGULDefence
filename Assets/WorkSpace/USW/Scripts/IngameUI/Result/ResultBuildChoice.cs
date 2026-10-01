using System;
using UnityEngine;

/// <summary>이번 판에 고른 선택지의 표시 정보. 설명은 선택 당시 확정한 문자열을 전달한다.</summary>
[Serializable]
public class ResultBuildChoice
{
    /// <summary>선택지 아이콘.</summary>
    public Sprite Icon;

    /// <summary>선택지 이름.</summary>
    public string Name;

    /// <summary>선택한 효과의 설명.</summary>
    public string Description;
}
