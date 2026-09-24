using TMPro;
using UnityEngine;

namespace GaeGGUL.UI.Totem
{
    [CreateAssetMenu(fileName = "TotemDisplaySettings", menuName = "UI/TotemDisplaySettings")]
    public class TotemDisplaySettings : ScriptableObject
    {
        [Header("Field")]
        public Sprite fieldSprite;
        public string fieldLabel = "필드";

        [Header("Totem")]
        public Sprite totemSprite;
        public string totemLabel = "토템 위치";

        [Header("Effect")]
        public Sprite effectSprite;
        public string effectLabel = "효과 범위";

        [Header("Debuff")]
        public Sprite debuffSprite;
        public string debuffLabel = "디버프 적용";

        [Header("Range Grid Style (적용 범위 칸만 표시)")]
        [Tooltip("칸 최대 크기(UI 단위). 범위가 작아도 이보다 커지지 않고, 박스를 넘칠 때만 줄어든다")]
        [Min(1f)] public float maxCellSize = 40f;
        [Tooltip("칸 간격 = 칸 크기 × 이 비율")]
        [Range(0f, 1f)] public float cellSpacingRatio = 0.22f;
        [Tooltip("토템 자신의 칸 색 (꽉 채움)")]
        public Color totemCellColor = new Color(0.97f, 0.97f, 0.98f, 1f);
        [Tooltip("효과 그룹이 없는 토템의 범위 색")]
        public Color defaultRangeColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        [Tooltip("디버프 범위 색")]
        public Color debuffRangeColor = new Color(0.75f, 0.45f, 1f, 1f);
        [Tooltip("범위 칸 안쪽 반투명 채움의 알파")]
        [Range(0f, 1f)] public float rangeFillAlpha = 0.08f;
        [Tooltip("테두리 최소 밝기(HSV V). 어두운 그룹 색도 어두운 배경에서 보이도록 UI에서만 올린다")]
        [Range(0f, 1f)] public float outlineMinBrightness = 0.8f;
        [Tooltip("9-slice 테두리 스프라이트")]
        public Sprite rangeOutlineSprite;
        [Tooltip("테두리 스프라이트 두께 배율 (Image.pixelsPerUnitMultiplier). 클수록 얇아진다")]
        [Min(0.01f)] public float outlineThicknessMultiplier = 1.5f;

        [Header("Range Caption")]
        public string captionText = "적용 범위";
        public TMP_FontAsset captionFont;
        [Min(1f)] public float captionFontSize = 22f;
        public Color captionColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        [Tooltip("그리드 아래 캡션에 비워 둘 높이(UI 단위)")]
        [Min(0f)] public float captionReserve = 32f;

        [Header("No Range")]
        [Tooltip("범위가 없는 토템일 때 박스 가운데 문구. 비우면 예전처럼 박스째 숨긴다 (폰트는 captionFont 사용)")]
        public string noRangeText = "범위 없음";
        [Min(1f)] public float noRangeFontSize = 24f;
        public Color noRangeColor = new Color(0.6f, 0.6f, 0.63f, 1f);
    }
}
