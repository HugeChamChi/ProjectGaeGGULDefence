using UnityEngine;

/// <summary>드론이 생성 순서대로 채우는 오너 기준 월드 좌표 슬롯.</summary>
[CreateAssetMenu(menuName = "Game/Drone Formation Settings")]
public sealed class DroneFormationSettings : ScriptableObject
{
    [SerializeField] private Vector2[] _slotOffsets = System.Array.Empty<Vector2>();

    /// <summary>기존 드론을 재배치하지 않고 순서대로 사용하는 고정 슬롯.</summary>
    public Vector2[] SlotOffsets => _slotOffsets;
}
