using UnityEngine;

/// <summary>오너의 저작된 외형 치수와 궤도 설정. 기존 고정 슬롯도 보존한다.</summary>
[CreateAssetMenu(menuName = "Game/Drone Formation Settings")]
public sealed class DroneFormationSettings : ScriptableObject
{
    [SerializeField] private Vector2[] _slotOffsets = System.Array.Empty<Vector2>();
    [SerializeField] private DroneOrbitSettings _orbit;
    [SerializeField, Min(0f)] private float _ownerHalfWidth;
    [SerializeField, Min(0f)] private float _ownerHeadHeight;

    /// <summary>공통 궤도 외형 설정. 비어 있거나 비활성이면 고정 슬롯을 사용한다.</summary>
    public DroneOrbitSettings Orbit => _orbit;
    /// <summary>오너의 월드 반폭. 대기 애니메이션/임시 스케일과 독립인 저작값.</summary>
    public float OwnerHalfWidth => _ownerHalfWidth;
    /// <summary>오너 피벗에서 머리까지의 월드 높이.</summary>
    public float OwnerHeadHeight => _ownerHeadHeight;

    /// <summary>기존 드론을 재배치하지 않고 순서대로 사용하는 고정 슬롯.</summary>
    public Vector2[] SlotOffsets => _slotOffsets;
}
