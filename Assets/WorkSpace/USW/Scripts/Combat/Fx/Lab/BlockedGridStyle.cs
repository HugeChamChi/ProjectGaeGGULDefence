using UnityEngine;

/// <summary>배치 불가 표시의 색과 형태를 조정하는 실험실 전용 시안 데이터.</summary>
[CreateAssetMenu(menuName = "USW/Fx Lab/Blocked Grid Style")]
public sealed class BlockedGridStyle : ScriptableObject
{
    /// <summary>시안 버튼과 제목에 표시할 이름.</summary>
    public string DisplayName;
    /// <summary>시안의 의도와 비교 기준.</summary>
    [TextArea] public string Description;
    /// <summary>표시할 도형 구성.</summary>
    public BlockedGridStyleKind Kind;
    /// <summary>바닥을 덮는 색.</summary>
    public Color Fill = new Color(.12f, .17f, .21f, .60f);
    /// <summary>테두리 색.</summary>
    public Color Edge = new Color(.65f, .75f, .78f, .65f);
    /// <summary>사선 또는 체인 색.</summary>
    public Color Pattern = new Color(.63f, .70f, .73f, .28f);
    /// <summary>자물쇠 또는 X의 색.</summary>
    public Color Symbol = new Color(.86f, .91f, .92f, .90f);
    /// <summary>칸 크기에 대한 테두리 두께 비율.</summary>
    [Range(.01f, .08f)] public float EdgeWidth = .025f;
    /// <summary>칸 크기에 대한 사선 간격 비율.</summary>
    [Range(.10f, .35f)] public float HatchSpacing = .18f;
}
