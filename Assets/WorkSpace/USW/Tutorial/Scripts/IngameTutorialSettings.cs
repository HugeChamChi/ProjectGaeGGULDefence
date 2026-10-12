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
        public TotemData[] TotemChoices;
        public TotemData RequiredTotem;
        public BossPatternData CounterPattern;
        [Header("Totem lessons")]
        [TextArea] public string TotemSwipeInstruction = "좌우로 밀어 다른 토템도 확인해 보세요.\n아직 선택하지 않고 효과와 범위를 살펴볼 거예요.";
        [TextArea] public string TotemRotationBadgeInstruction = "이 회전 아이콘은 토템의 효과 범위를 돌릴 수 있다는 뜻이에요.\n빗금이 있는 아이콘은 회전할 수 없어요.";
        [TextArea] public string TotemReturnInstruction = "확인했으면 가속 토템으로 돌아와 주세요.\n좌우로 밀어 가속 토템을 찾아보세요.";
        [TextArea] public string TotemReselectInstruction = "좋아요! 배경을 눌러 목록으로 돌아간 다음\n가속 토템을 다시 눌러 선택해 주세요.";
        [TextArea] public string CombatPracticeInstruction = "토템 이동과 회전을 모두 배웠어요!\n이제 WAVE 3 보스까지 물리쳐 보세요.";
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
        [TextArea] public string ChoicePreviewInstruction = "선택지를 길게 누르면 영향을 받는 유닛이 강조돼요.\n확인한 뒤 손을 떼어 돌아와 보세요.";
        [TextArea] public string ChoiceTermInstruction = "강조된 「용어」를 눌러 설명을 확인해 보세요.\n확인이 끝나면 설명 창을 닫아 주세요.";
        /// <summary>Quiet pause after closing a term before the choice pointer returns.</summary>
        [Range(1f, 2f)] public float ChoiceReturnPointDelaySeconds = 1.5f;
        /// <summary>Radius and stroke of the filling hold cue in reference canvas pixels.</summary>
        public float HoldCueRadius = 44f;
        /// <summary>Stroke width of the hold cue.</summary>
        public float HoldCueThickness = 8f;
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
    }
}
