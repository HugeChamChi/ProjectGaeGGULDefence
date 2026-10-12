using UnityEngine;

/// <summary>전투 드론 H 궤도의 공통 외형 설정. 자폭 드론과 전투 수치에는 적용하지 않는다.</summary>
[CreateAssetMenu(menuName = "Game/Drone Orbit Settings")]
public sealed class DroneOrbitSettings : ScriptableObject
{
    [SerializeField] private bool _enabled = true;
    [SerializeField, Min(0.01f)] private float _droneScale = 0.7f;
    [SerializeField, Min(0.01f)] private float _periodSeconds = 6f;
    [SerializeField, Min(0f)] private float _horizontalPadding = 0.1f;
    [SerializeField, Min(0f)] private float _verticalRadius = 0.16f;
    [SerializeField, Min(0f)] private float _centerHeightRatio = 0.45f;
    [SerializeField] private int _backSortingOffset = -1;
    [SerializeField] private int _frontSortingOffset = 1;
    [SerializeField] private bool _faceMotion = true;
    [SerializeField, Min(0f)] private float _turnSeconds = 0.15f;

    /// <summary>끄면 기존 고정 슬롯·크기·호버·칸 기준 좌우를 사용한다.</summary>
    public bool Enabled => _enabled;
    /// <summary>저작된 드론 프리팹 크기에 곱할 배율.</summary>
    public float DroneScale => _droneScale;
    /// <summary>한 바퀴의 전투 시간. 선택 정지 중에는 실제 시간을 사용한다.</summary>
    public float PeriodSeconds => _periodSeconds;
    /// <summary>오너 반폭에 더할 궤도 가로 여유.</summary>
    public float HorizontalPadding => _horizontalPadding;
    /// <summary>타원의 세로 반지름.</summary>
    public float VerticalRadius => _verticalRadius;
    /// <summary>오너 머리 높이에 곱할 중심 높이 비율.</summary>
    public float CenterHeightRatio => _centerHeightRatio;
    /// <summary>타원 위쪽 반의 오너 정렬 기준 차이.</summary>
    public int BackSortingOffset => _backSortingOffset;
    /// <summary>타원 아래쪽 반의 오너 정렬 기준 차이.</summary>
    public int FrontSortingOffset => _frontSortingOffset;
    /// <summary>이동 방향 좌우 전환. 끄면 기존 판 가운데 보기.</summary>
    public bool FaceMotion => _faceMotion;
    /// <summary>왼쪽과 오른쪽 사이의 완전 전환 시간.</summary>
    public float TurnSeconds => _turnSeconds;
}
