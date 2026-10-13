using UnityEngine;

namespace GaeGGUL.Tutorial
{
    /// <summary>Authored economy, timings and deterministic content for TutorialScene.</summary>
    [CreateAssetMenu(menuName = "GaeGGUL/Tutorial/Ingame Settings")]
    public sealed class IngameTutorialSettings : ScriptableObject
    {
        [Header("Fixed summon order: attack / matching attack / two supporting units")]
        public UnitData[] SpawnUnits;
        public Vector2Int[] SpawnCells;
        [Header("Fixed tutorial rewards")]
        public UnitData MergeUnit;
        public string UpgradeTarget = "Deltan";
        public LevelUpData[] LevelUpChoices;
        /// <summary>
        /// 레벨업에서 고른 카드 뒤에 바로 지급하는 카드(사용자 10-13: 다 쏴 제껴버려 9102·긴급 교신 9105).
        /// ≡ 메뉴 수업의 선택지 보관함이 세 장으로 보이고, 족장 스킬 수업에서 기폭이 함께 터진다.
        /// </summary>
        public LevelUpData[] GrantedChoices;
        public TotemData[] TotemChoices;
        public TotemData RequiredTotem;
        /// <summary>Actual non-rotatable totem used only to illustrate its crossed rotation badge.</summary>
        public TotemData NonRotatableExample;
        public BossPatternData CounterPattern;
        [Header("Totem lessons")]
        [TextArea] public string TotemSwipeInstruction = "좌우로 밀어 다른 토템도 확인해 보세요.\n아직 선택하지 않고 효과와 범위를 살펴볼 거예요.";
        [TextArea] public string TotemRotationBadgeInstruction = "이 회전 아이콘은 토템의 효과 범위를 돌릴 수 있다는 뜻이에요.";
        /// <summary>Separate explanation focused on the crossed comparison badge.</summary>
        [TextArea] public string TotemNoRotationInstruction = "빗금이 있는 아이콘은 회전할 수 없어요.";
        /// <summary>Gap between the real rotation badge and its tutorial comparison, in canvas pixels.</summary>
        [Min(0f)] public float TotemBadgeComparisonGap = 16f;
        [TextArea] public string TotemReturnInstruction = "확인했으면 가속 토템으로 돌아와 주세요.\n좌우로 밀어 가속 토템을 찾아보세요.";
        [TextArea] public string CombatPracticeInstruction = "토템 이동과 회전을 모두 배웠어요!\n이제 WAVE 3 보스까지 물리쳐 보세요.";
        [Header("≡ menu lesson (before the WAVE 3 battle)")]
        [TextArea] public string MenuOpenInstruction = "전투 중에 볼 수 있는 정보는 이 메뉴에 모여 있어요.\n메뉴 버튼을 눌러 보세요.";
        [TextArea] public string MenuDamageInstruction = "딜 기여도를 눌러 보세요.";
        [TextArea] public string MenuDamageExplanation = "지금까지 누가 피해를 얼마나 줬는지 볼 수 있어요.\n전투 중에도 열어 둔 채로 확인할 수 있어요.";
        [TextArea] public string MenuReopenInstruction = "메뉴를 다시 열어 보세요.";
        [TextArea] public string MenuSelectionInstruction = "이번엔 선택지를 눌러 보세요.";
        [TextArea] public string MenuSelectionExplanation = "레벨업에서 얻은 선택지는 여기에 모여요.\n모서리의 얼굴이 효과를 받는 유닛이에요.";
        [TextArea] public string MenuSettingsExplanation = "소리와 진동은 여기 설정에서 바꿀 수 있어요.\n튜토리얼 중에는 잠시 이용할 수 없어요.";
        [Min(0)] public float InitialCost = 20f;
        [Min(0)] public float CostIncrease = 20f;
        [Tooltip("Tutorial-only food income until the upgrade introduction finishes.")]
        [Min(0)] public float TrainingFoodPerSecond = 20f;
        [Min(0)] public float UpgradeFoodThreshold = 100f;
        [Header("Presentation")]
        /// <summary>Released pointing hand used between tutorial gestures.</summary>
        public Sprite HandIdleSprite;
        /// <summary>Pressed pointing hand used for taps, holds and drags.</summary>
        public Sprite HandPressedSprite;
        /// <summary>Hand size at the authored canvas reference width.</summary>
        public Vector2 HandSize = new Vector2(160f, 160f);
        /// <summary>Normalized fingertip position in the released artwork.</summary>
        public Vector2 HandIdleTip = new Vector2(0.23f, 0.86f);
        /// <summary>Normalized fingertip position in the pressed artwork.</summary>
        public Vector2 HandPressedTip = new Vector2(0.18f, 0.76f);
        /// <summary>End of the pressed phase for a repeating tap gesture.</summary>
        [Range(0f, 1f)] public float HandTapPressedFraction = 0.5f;
        [TextArea] public string ChoicePreviewInstruction = "<b><color=#FF9D32>선택지를 꾹 누르고 있어 보세요.</color></b>\n영향을 받는 유닛이 강조돼요.\n필드에 해당 유닛이 없으면 강조 표시가 나타나지 않아요.\n확인한 뒤 손을 떼어 주세요.";
        [TextArea] public string ChoiceTermInstruction = "강조된 「용어」를 눌러 설명을 확인해 보세요.\n확인이 끝나면 설명 창을 닫아 주세요.";
        /// <summary>Instruction shown when the term lesson returns to the required choice.</summary>
        [TextArea] public string ChoiceSelectInstruction = "이제 <color=#FF9D32>「넹?」 선택지</color>를 눌러 선택해 주세요.";
        /// <summary>Quiet pause after closing a term before the choice pointer returns.</summary>
        [Range(1f, 2f)] public float ChoiceReturnPointDelaySeconds = 1.5f;
        /// <summary>Radius and stroke of the filling hold cue in reference canvas pixels.</summary>
        public float HoldCueRadius = 44f;
        /// <summary>Stroke width of the hold cue.</summary>
        public float HoldCueThickness = 8f;
        /// <summary>Hold cue offset from the fingertip in reference canvas pixels.</summary>
        public Vector2 HoldCueOffset = new Vector2(-84f, 64f);
        /// <summary>Visible hold cue color.</summary>
        public Color HoldCueColor = new Color(1f, 0.8f, 0.25f, 1f);
        [Min(0)] public float ObserveCombatSeconds = 5f;
        [Min(0.1f)] public float ExperienceFillSeconds = 2f;
        [Min(0.1f)] public float RevealSeconds = 0.35f;
        [Min(0.1f)] public float FocusSeconds = 0.65f;
        [Range(5f, 7f)] public float ChiefDelaySeconds = 6f;
        [Min(0.1f)] public float CameraZoomSeconds = 0.6f;
        [Range(0.2f, 1f)] public float CameraZoomRatio = 0.7f;
        [Min(0)] public float CameraHoldSeconds = 0.5f;
        [Min(0.1f)] public float GestureSeconds = 1.6f;
        [Min(0)] public float HoldGestureSeconds = 0.65f;
        [Range(0f, 1f)] public float DimAlpha = 0.65f;
        [Min(0)] public float HighlightPaddingPixels = 0f;
        /// <summary>Rounded soft-edged dim holes. Without it the overlay falls back to hard rectangles.</summary>
        public Material SpotlightMaterial;
        /// <summary>Spotlight corner radius in reference canvas pixels (1080 wide).</summary>
        [Min(0)] public float SpotlightCornerRadius = 28f;
        /// <summary>Distance outside a hole over which the dim fades in, in reference canvas pixels.</summary>
        [Min(0)] public float SpotlightFeather = 28f;
    }
}
