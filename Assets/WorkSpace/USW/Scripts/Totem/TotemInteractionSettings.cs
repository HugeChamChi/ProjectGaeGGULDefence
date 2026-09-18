using UnityEngine;

/// <summary>Run inventory capacity and touch interaction settings.</summary>
[CreateAssetMenu(menuName = "Game/Totem Interaction Settings")]
public sealed class TotemInteractionSettings : ScriptableObject
{
    [SerializeField, Range(1, 5)] private int _capacity = 5;
    [SerializeField, Min(0.1f)] private float _moveHoldSeconds = 0.45f;
    [SerializeField, Min(1f)] private float _rotationDeadZonePixels = 24f;
    /// <summary>Maximum number of stored totems.</summary>
    public int Capacity => Mathf.Clamp(_capacity, 1, 5);
    /// <summary>Hold duration before starting a positional drag.</summary>
    public float MoveHoldSeconds => _moveHoldSeconds;
    /// <summary>Release inside this screen-space radius to cancel rotation.</summary>
    public float RotationDeadZonePixels => _rotationDeadZonePixels;
}
