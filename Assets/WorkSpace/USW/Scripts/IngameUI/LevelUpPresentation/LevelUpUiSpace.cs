using UnityEngine;

/// <summary>
/// 레벨업 연출 공용 좌표 유틸. Canvas가 서로 다르거나(레벨업 패널 ↔ HUD) 월드 유닛을 UI 위에 표시할 때
/// 화면 좌표를 거쳐 변환한다.
/// </summary>
public static class LevelUpUiSpace
{
    /// <summary>다른 Canvas에 있는 source의 화면 위치를 target 평면의 월드 좌표로 변환한다.</summary>
    public static Vector3 WorldPointIn(RectTransform target, RectTransform source)
    {
        if (target == null || source == null) return source != null ? source.position : Vector3.zero;
        var screen = RectTransformUtility.WorldToScreenPoint(CanvasCamera(source), source.position);
        return RectTransformUtility.ScreenPointToWorldPointInRectangle(target, screen, CanvasCamera(target), out var world)
            ? world : source.position;
    }

    /// <summary>필드 유닛(월드 좌표)의 피벗 + offsetY 지점을 root Canvas 평면 좌표로 변환한다.</summary>
    public static Vector3 UnitCanvasPoint(RectTransform root, UnitBase unit, float offsetY)
    {
        var cam = Camera.main;
        if (cam == null || unit == null) return root.position;
        Vector3 screen = cam.WorldToScreenPoint(unit.transform.position + Vector3.up * offsetY);
        return RectTransformUtility.ScreenPointToWorldPointInRectangle(root, screen, CanvasCamera(root), out var point)
            ? point : root.position;
    }

    /// <summary>t가 속한 최상위 Canvas의 RectTransform. 없으면 null.</summary>
    public static RectTransform RootCanvasOf(Transform t)
    {
        if (t == null) return null;
        var canvas = t.GetComponentInParent<Canvas>(true);
        return canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
    }

    /// <summary>Canvas 렌더 모드에 맞는 UI 카메라 (Overlay면 null).</summary>
    public static Camera CanvasCamera(Transform t)
    {
        var canvas = t != null ? t.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return null;
        canvas = canvas.rootCanvas;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    /// <summary>초 → UniTask.Delay용 밀리초.</summary>
    public static int Ms(float seconds) => Mathf.RoundToInt(seconds * 1000f);
}
