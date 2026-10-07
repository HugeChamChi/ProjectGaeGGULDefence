using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 강화 화면 모양 설정. UI3600 제작 아트를 우선 사용하며, 미연결 시 기존 도형으로 표시한다.
/// Active는 다음 수치·추천 화살표, Accent는 선택·최대 레벨을 강조한다.
/// 크기는 기준 해상도 1080x1920 캔버스 단위.
/// </summary>
[CreateAssetMenu(fileName = "UpgradeViewSettings", menuName = "USW/OutGame/Upgrade View Settings")]
public class ResearchViewSettings : ScriptableObject
{
    /// <summary>스탯 하나의 표시 정보.</summary>
    [Serializable]
    public class StatInfo
    {
        public ResearchStat Stat;
        [Tooltip("하단 패널에 보이는 이름")]
        public string DisplayName;
        [Tooltip("true면 +2%, false면 +150")]
        public bool IsPercent = true;
    }

    [Header("폰트 / 스프라이트")]
    [Tooltip("제목·버튼·수치")]
    public TMP_FontAsset FontBold;
    [Tooltip("보조 글자")]
    public TMP_FontAsset FontRegular;
    [Tooltip("둥근 네모 채움 (9-slice)")]
    public Sprite RoundedFill;
    [Tooltip("둥근 네모 테두리 (9-slice)")]
    public Sprite RoundedOutline;
    public Sprite CircleFill;
    [Tooltip("원 테두리 (원형 진행 바에도 쓴다)")]
    public Sprite CircleOutline;
    [Tooltip("노드 아래 부드러운 그림자")]
    public Sprite Shadow;
    public Sprite UpgradeArrow;
    public Sprite GoldIcon;
    [Tooltip("택1에서 막힌 노드 표시")]
    public Sprite BlockedIcon;
    [Tooltip("둥근 네모 모서리 크기 배율 (클수록 모서리가 작아진다)")]
    public float RoundedCornerScale = 4f;

    [Header("UI3600 제작 아트 (미연결 시 기존 도형 사용)")]
    /// <summary>전체 트리 배경.</summary>
    public Sprite PanelBackground;
    /// <summary>상단 타이틀 바.</summary>
    public Sprite TopBarSprite;
    /// <summary>잠금 노드 바탕.</summary>
    public Sprite NodeLockedSprite;
    /// <summary>강화 가능한 노드 바탕.</summary>
    public Sprite NodeAvailableSprite;
    /// <summary>추천 노드 바탕과 강조 테두리.</summary>
    public Sprite NodeRecommendSprite;
    /// <summary>최대 레벨 노드 안의 MAX 표시.</summary>
    [Tooltip("노드 안의 MAX 글자 이미지")]
    public Sprite NodeMaxSprite;
    /// <summary>추천 배지 바탕.</summary>
    public Sprite RecommendBadgeSprite;
    /// <summary>다섯 단계의 강화 표시 도트.</summary>
    public Sprite LevelDotsSprite;
    /// <summary>최대 강화 노드의 아이콘 바탕.</summary>
    [Tooltip("최대 강화 노드의 금색 아이콘 바탕")]
    public Sprite NodeIconSlotSprite;
    /// <summary>미해금 구간 연결선.</summary>
    public Sprite LineLockedSprite;
    /// <summary>해금 구간 연결선.</summary>
    public Sprite LineActiveSprite;
    /// <summary>하단 정보 카드 배경.</summary>
    public Sprite CardBackground;
    /// <summary>하단 카드의 큰 아이콘 프레임.</summary>
    public Sprite IconFrameSprite;
    /// <summary>강화 버튼 바탕.</summary>
    public Sprite UpgradeButtonSprite;
    /// <summary>하단 MAX와 추천 버튼의 금색 바탕.</summary>
    [Tooltip("하단 MAX/추천 버튼의 금색 바탕. 글자는 폰트로 표시")]
    public Sprite MaxLabelSprite;

    /// <summary>상태별 제작 아트가 모두 연결되었는가.</summary>
    public bool HasNodeArt => NodeLockedSprite != null && NodeAvailableSprite != null && NodeRecommendSprite != null && NodeIconSlotSprite != null;

    [Header("색")]
    public Color Background = new Color32(0x15, 0x19, 0x23, 0xFF);
    [Tooltip("카드·노드·하단 패널 바탕")]
    public Color Surface = new Color32(0x22, 0x28, 0x36, 0xFF);
    [Tooltip("글자·아이콘 (밝은 색)")]
    public Color Ink = new Color32(0xE9, 0xEC, 0xF2, 0xFF);
    [Tooltip("보조 글자")]
    public Color Muted = new Color32(0x8C, 0x94, 0xA6, 0xFF);
    [Tooltip("잠긴 노드 테두리·아이콘, 아직 안 간 연결선")]
    public Color Faint = new Color32(0x3A, 0x42, 0x54, 0xFF);
    [Tooltip("스탯 줄 바탕, 비활성 버튼, 알림 바탕")]
    public Color Soft = new Color32(0x2C, 0x33, 0x44, 0xFF);
    [Tooltip("포인트 1: 지금 올릴 수 있음, 다음 수치, 선택 표시, [강화] 버튼")]
    public Color Active = new Color32(0x2C, 0xC4, 0xB6, 0xFF);
    [Tooltip("포인트 2: 최대 레벨 노드, 끝낸 길, MAX")]
    public Color Accent = new Color32(0xF2, 0xA9, 0x3B, 0xFF);
    [Tooltip("포인트 색(청록·금색) 칸 위에 올리는 글자·아이콘")]
    public Color OnColor = Color.white;
    public Color ShadowColor = new Color(0f, 0f, 0f, 0.35f);

    [Header("노드")]
    public float NodeSize = 176f;
    public float CircleNodeSize = 196f;
    [Tooltip("마름모(특별) 노드 한 변 길이")]
    public float DiamondNodeSize = 146f;
    public float SelectionPadding = 12f;
    [Tooltip("올릴 수 있는 노드 테두리가 깜빡이는 주기(초)")]
    public float PulseSeconds = 1.4f;
    [Range(0f, 1f)] public float PulseMinAlpha = 0.45f;

    [Header("연결선")]
    public float LineWidth = 8f;

    [Header("배치 (한 화면에 6~7줄)")]
    [Tooltip("가운데 줄기의 가로 위치 (화면 가운데 기준)")]
    public float TreeCenterX = -70f;
    [Tooltip("왼쪽/오른쪽 칸이 가운데에서 떨어진 거리")]
    public float ColumnOffset = 190f;
    [Tooltip("옆가지(Column 2) 특별 노드의 가로 위치 (화면 가운데 기준)")]
    public float SpecialColumnX = 350f;
    public float RowSpacing = 255f;
    public float ContentPadding = 200f;

    [Header("하단 패널")]
    public float PanelHeight = 430f;
    [Tooltip("처음 실행할 때 [추천 따라가기] 켜짐 여부 (이후엔 저장된 값)")]
    public bool FollowByDefault = true;

    [Header("알림")]
    public float ToastSeconds = 1.3f;

    [Header("문구")]
    public string TitleFormat = "강화 {0}";
    public string UpgradeLabel = "강화";
    public string MaxLabel = "MAX";
    public string LockedLabel = "잠김";
    public string ChangeChoiceLabel = "선택 바꾸기";
    public string RecommendLabel = "추천";
    public string RecommendButtonLabel = "추천 강화";
    public string FollowLabel = "추천 따라가기";
    public string ToastFormat = "{0} Lv.{1}   {2}";
    public string GateLabelFormat = "{0}단계 · 전체 강화 {1}/{2}";
    public string GateOpenFormat = "{0}단계 열림";
    public string LayoutButtonFormat = "구성: {0}";
    public string ResetLabel = "초기화";

    [Header("스탯 표시")]
    public StatInfo[] Stats = Array.Empty<StatInfo>();

    /// <summary>스탯 표시 정보. 등록이 없으면 enum 이름을 쓴다.</summary>
    public StatInfo GetStat(ResearchStat stat)
    {
        foreach (var info in Stats)
            if (info != null && info.Stat == stat) return info;
        return new StatInfo { Stat = stat, DisplayName = stat.ToString(), IsPercent = true };
    }

    /// <summary>수치 표기: +2% 또는 +150.</summary>
    public string FormatValue(ResearchStat stat, float value) =>
        GetStat(stat).IsPercent ? $"+{value * 100f:0.##}%" : $"+{value:0.##}";

    /// <summary>노드 모양별 크기.</summary>
    public float SizeOf(ResearchNodeData node) => node.Shape switch
    {
        ResearchNodeShape.Circle => CircleNodeSize,
        ResearchNodeShape.Special => DiamondNodeSize,
        _ => NodeSize,
    };
}
