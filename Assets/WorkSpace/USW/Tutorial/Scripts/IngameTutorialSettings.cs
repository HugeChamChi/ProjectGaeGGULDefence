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
        [Min(0)] public float InitialCost = 20f;
        [Min(0)] public float CostIncrease = 20f;
        [Tooltip("Tutorial-only food income until the upgrade introduction finishes.")]
        [Min(0)] public float TrainingFoodPerSecond = 20f;
        [Min(0)] public float UpgradeFoodThreshold = 100f;
        [Header("Presentation")]
        [Min(0.1f)] public float RevealSeconds = 0.35f;
        [Range(5f, 7f)] public float ChiefDelaySeconds = 6f;
        [Min(0.1f)] public float CameraZoomSeconds = 0.6f;
        [Range(0.2f, 1f)] public float CameraZoomRatio = 0.7f;
        [Min(0)] public float CameraHoldSeconds = 0.5f;
        [Min(0.1f)] public float GestureSeconds = 1.6f;
        [Min(0)] public float HoldGestureSeconds = 0.65f;
        [Range(0f, 1f)] public float DimAlpha = 0.65f;
        [Min(0)] public float HighlightPaddingPixels = 12f;
        [Min(1)] public float OutlinePixels = 4f;
    }
}
