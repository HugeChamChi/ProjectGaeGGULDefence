using UnityEngine;

/// <summary>화면 해상도와 무관하게 월드 유닛·토템 조작에 사용하는 기준 픽셀 설정.</summary>
[CreateAssetMenu(fileName = "WorldInteractionSettings", menuName = "Game/World Interaction Settings")]
public sealed class WorldInteractionSettings : ScriptableObject
{
    /// <summary>게임 UI와 동일한 기준 화면 너비.</summary>
    [Min(1f)] public float ReferenceWidth = 1080f;
    /// <summary>클릭에서 드래그로 전환하는 기준 화면 이동 거리.</summary>
    [Min(1f)] public float DragThresholdPixels = 12f;
    /// <summary>셀 경계의 작은 빈틈을 허용하는 기준 픽셀 거리.</summary>
    [Min(0f)] public float DropEdgeTolerancePixels = 10f;

    /// <summary>현재 화면 너비에 맞춘 드래그 시작 거리.</summary>
    public float DragThresholdForScreen => DragThresholdPixels * Screen.width / Mathf.Max(1f, ReferenceWidth);

    /// <summary>게임의 Z=0 조작 평면에서 기준 픽셀 거리를 월드 거리로 변환한다. 원근·직교 카메라 모두 지원한다.</summary>
    public float DropToleranceWorld(Camera camera)
    {
        if (camera == null || Screen.width <= 0) return 0f;
        float pixels = DropEdgeTolerancePixels * Screen.width / Mathf.Max(1f, ReferenceWidth);
        var screen = camera.WorldToScreenPoint(Vector3.zero);
        var plane = new Plane(Vector3.forward, Vector3.zero);
        var first = camera.ScreenPointToRay(screen);
        var second = camera.ScreenPointToRay(screen + Vector3.right * pixels);
        return plane.Raycast(first, out float firstDistance) && plane.Raycast(second, out float secondDistance)
            ? Vector3.Distance(first.GetPoint(firstDistance), second.GetPoint(secondDistance)) : 0f;
    }
}
