using UnityEngine;

/// <summary>Run inventory capacity and touch interaction settings.</summary>
[CreateAssetMenu(menuName = "Game/Totem Interaction Settings")]
public sealed class TotemInteractionSettings : ScriptableObject
{
    [SerializeField, Range(1, 5)] private int _capacity = 5;
    [SerializeField, Min(0.1f)] private float _moveHoldSeconds = 0.45f;
    [SerializeField, Min(1f)] private float _rotationDeadZonePixels = 24f;

    [Header("Hold Feedback (누르고 있을 때 원형 게이지 + 슬로우)")]
    [Tooltip("누른 뒤 이 시간(실제 초)이 지나야 게이지와 슬로우가 시작된다. 짧은 탭에서 깜빡이지 않게 한다.")]
    [SerializeField, Min(0f)] private float _holdFeedbackDelaySeconds = 0.1f;
    [Tooltip("누르고 있는 동안의 게임 속도 (1 = 정상). 보스 타이머·전투도 같은 배율로 느려진다.")]
    [SerializeField, Range(0.1f, 1f)] private float _holdSlowTimeScale = 0.6f;
    [Tooltip("정상 속도 ↔ 슬로우 전환에 걸리는 실제 초.")]
    [SerializeField, Min(0.01f)] private float _holdSlowRampSeconds = 0.12f;
    [Tooltip("원형 게이지 전체 크기 (월드 단위, 쿼드 한 변).")]
    [SerializeField, Min(0.1f)] private float _holdRingWorldSize = 2.4f;
    [Tooltip("GaeGGUL/Totem Hold Ring 셰이더 머티리얼. 색·두께·점 개수는 머티리얼에서 조정.")]
    [SerializeField] private Material _holdRingMaterial;

    /// <summary>Maximum number of stored totems.</summary>
    public int Capacity => Mathf.Clamp(_capacity, 1, 5);
    /// <summary>Hold duration before starting a positional drag.</summary>
    public float MoveHoldSeconds => _moveHoldSeconds;
    /// <summary>Release inside this screen-space radius to cancel rotation.</summary>
    public float RotationDeadZonePixels => _rotationDeadZonePixels;
    /// <summary>Unscaled seconds after press before the hold gauge and slow motion begin (kept below MoveHoldSeconds).</summary>
    public float HoldFeedbackDelaySeconds => Mathf.Clamp(_holdFeedbackDelaySeconds, 0f, _moveHoldSeconds * 0.9f);
    /// <summary>Game time scale requested while a totem is held.</summary>
    public float HoldSlowTimeScale => _holdSlowTimeScale;
    /// <summary>Unscaled seconds to blend between normal speed and the hold slow scale.</summary>
    public float HoldSlowRampSeconds => _holdSlowRampSeconds;
    /// <summary>World-space edge length of the hold gauge quad.</summary>
    public float HoldRingWorldSize => _holdRingWorldSize;
    /// <summary>Material using the GaeGGUL/Totem Hold Ring shader; the gauge is skipped when null.</summary>
    public Material HoldRingMaterial => _holdRingMaterial;
}
