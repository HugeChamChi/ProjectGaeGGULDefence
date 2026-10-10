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
        [Min(0)] public float InitialCost = 20f;
        [Min(0)] public float CostIncrease = 20f;
        [Tooltip("Tutorial-only food income until the upgrade introduction finishes.")]
        [Min(0)] public float TrainingFoodPerSecond = 20f;
        [Min(0)] public float UpgradeFoodThreshold = 100f;
        [Header("Presentation")]
        [TextArea] public string ChoicePreviewInstruction = "선택지를 길게 누르면 영향을 받는 유닛이 강조돼요.\n확인한 뒤 손을 떼어 돌아와 보세요.";
        [TextArea] public string ChoiceTermInstruction = "강조된 「용어」를 눌러 설명을 확인해 보세요.\n확인이 끝나면 설명 창을 닫아 주세요.";
        [TextArea] public string ChoiceSelectInstruction = "「넹?」 선택지를 눌러 보상을 받아 보세요.";
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
