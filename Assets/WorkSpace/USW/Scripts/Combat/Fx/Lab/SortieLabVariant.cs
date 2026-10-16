using System;
using UnityEngine;

/// <summary>출격 실험실의 시안 한 개. 인스펙터에서 Play 중에 값을 바꿔 볼 수 있다.</summary>
[Serializable]
public sealed class SortieLabVariant
{
    /// <summary>버튼·상태 줄에 쓰는 짧은 이름.</summary>
    public string Label;
    /// <summary>상태 줄에 보이는 한 줄 설명.</summary>
    [TextArea] public string Description;
    /// <summary>움직임 방식.</summary>
    public SortieLabMotionKind Kind;
}
