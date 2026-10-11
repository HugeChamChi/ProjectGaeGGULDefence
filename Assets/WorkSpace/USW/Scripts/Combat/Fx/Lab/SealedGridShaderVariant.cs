using UnityEngine;

/// <summary>움직이는 봉인 사선 실험실의 색·불투명도·간격·속도 시안.</summary>
[CreateAssetMenu(menuName = "USW/Fx Lab/Sealed Grid Shader Variant")]
public sealed class SealedGridShaderVariant : ScriptableObject
{
    /// <summary>시안 버튼에 표시할 짧은 이름.</summary>
    public string DisplayName;
    /// <summary>시안 비교 시 안내할 특징.</summary>
    [TextArea] public string Description;
    /// <summary>사선 또는 사선 외 봉인 패턴.</summary>
    public SealedGridPatternKind PatternKind;
    /// <summary>칸 크기에 대한 패턴 전체 크기 비율.</summary>
    [Range(.2f, .85f)] public float PatternScale = .64f;
    /// <summary>칸 크기에 대한 패턴 선 굵기 비율.</summary>
    [Range(.01f, .08f)] public float PatternLineWidth = .035f;
    /// <summary>사선 외 패턴에 적용할 불투명도 배율.</summary>
    [Range(.1f, 1f)] public float PatternOpacity = 1f;
    /// <summary>칸 바닥을 덮는 회색과 불투명도.</summary>
    public Color FillColor = new Color(.09f, .12f, .14f, .9f);
    /// <summary>이동하는 사선의 색과 불투명도.</summary>
    public Color StripeColor = new Color(.38f, .44f, .47f, .96f);
    /// <summary>월드 좌표 기준 사선 반복 간격.</summary>
    [Min(.04f)] public float StripeSpacing = .16f;
    /// <summary>반복 주기에서 사선이 차지하는 비율.</summary>
    [Range(.02f, .98f)] public float StripeWidth = .24f;
    /// <summary>사선 이동 속도. 초당 반복 주기 수.</summary>
    public float ScrollSpeed = .5f;
}
