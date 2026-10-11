using UnityEngine;

/// <summary>딜표 레이아웃, 색과 부드러운 전환 속도 시안.</summary>
[CreateAssetMenu(menuName = "USW/UI Lab/Damage Chart Style")]
public sealed class DamageChartStyle : ScriptableObject
{
    /// <summary>시안 버튼 이름.</summary>
    public string DisplayName;
    /// <summary>시안의 읽기 기준.</summary>
    public string Description;
    /// <summary>화면 구성.</summary>
    public DamageChartLayoutKind Layout;
    /// <summary>창 배경색.</summary>
    public Color PanelColor;
    /// <summary>행 또는 카드 배경색.</summary>
    public Color RowColor;
    /// <summary>주요 글자색.</summary>
    public Color TextColor;
    /// <summary>보조 글자색.</summary>
    public Color MutedColor;
    /// <summary>선택과 전체 수치 강조색.</summary>
    public Color AccentColor;
    /// <summary>개체 합산 수 안내를 숨긴다.</summary>
    public bool HideCountCaption;
    /// <summary>막대 기준과 집계 방식 안내를 숨긴다.</summary>
    public bool HideGuideText;
    /// <summary>장식 대신 넓은 행과 정돈된 요약 영역을 사용하는 가로 막대 배치.</summary>
    public bool RefinedWideLayout;
    /// <summary>안쪽 선이 없는 KHJ 창 배경 면.</summary>
    public Sprite PanelSprite;
    /// <summary>KHJ 행 배경 면. 그래프 스프라이트에는 사용하지 않는다.</summary>
    public Sprite RowSprite;
    /// <summary>KHJ 초상화 프레임 배경 면.</summary>
    public Sprite PortraitFrameSprite;
    /// <summary>KHJ 선택 및 실험실 버튼 배경 면.</summary>
    public Sprite ButtonSprite;
    /// <summary>모서리 마스크와 검은 테두리의 KHJ 실루엣.</summary>
    public Sprite FrameShapeSprite;
    /// <summary>수치와 막대가 목표에 접근하는 시간.</summary>
    [Min(.05f)] public float ValueSeconds = .30f;
    /// <summary>순위 위치 이동 시간.</summary>
    [Min(.05f)] public float RankSeconds = .45f;
    /// <summary>시안 등장 페이드 시간.</summary>
    [Min(.05f)] public float RevealSeconds = .24f;
}
