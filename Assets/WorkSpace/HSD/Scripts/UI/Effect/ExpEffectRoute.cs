using System;
using UnityEngine;

/// <summary>Canvas 비율 좌표로 저장하는 3구간 베지어 경로. 끝 손잡이는 시작/도착점의 상대 좌표다.</summary>
[Serializable]
public sealed class ExpEffectRoute
{
    /// <summary>세 개의 연결된 베지어 곡선에 필요한 제어점 수.</summary>
    public const int PointCount = 10;
    [SerializeField] private Vector2[] _points = {
        Vector2.zero, new Vector2(-0.38f, 0f), new Vector2(-0.10f, 0.83f),
        new Vector2(-0.10f, 0.48f), new Vector2(-0.10f, -0.12f),
        new Vector2(0.38f, -0.12f), new Vector2(0.38f, -0.04f),
        new Vector2(0.38f, 0.08f), new Vector2(0f, -0.16f), Vector2.zero };

    /// <summary>유효한 경로 데이터인지 확인한다.</summary>
    public bool IsValid => _points != null && _points.Length == PointCount;

    /// <summary>시작/도착 좌표를 반영한 제어점을 반환한다.</summary>
    public Vector2 GetPoint(int index, Vector2 start, Vector2 end, bool mirror)
    {
        if (index == 0) return start;
        if (index == PointCount - 1) return end;
        Vector2 point = _points[index];
        if (index == 1 || index == PointCount - 2)
        {
            if (mirror) point.x = -point.x;
            return (index == 1 ? start : end) + point;
        }
        if (mirror) point.x = 1f - point.x;
        return point;
    }

    /// <summary>씬 뷰 좌표를 저장한다. 중간 앵커를 이동하면 인접 손잡이도 함께 이동한다.</summary>
    public void SetPoint(int index, Vector2 point, Vector2 start, Vector2 end, bool mirror)
    {
        if (index <= 0 || index >= PointCount - 1) return;
        Vector2 delta = point - GetPoint(index, start, end, mirror);
        if (index == 1 || index == PointCount - 2)
        {
            point -= index == 1 ? start : end;
            if (mirror) point.x = -point.x;
        }
        else if (mirror) point.x = 1f - point.x;
        _points[index] = point;
        if (index % 3 != 0) return;
        if (mirror) delta.x = -delta.x;
        _points[index - 1] += delta;
        _points[index + 1] += delta;
    }

    /// <summary>좌우 대칭 데이터를 독립 경로로 복사한다.</summary>
    public void CopyMirroredFrom(ExpEffectRoute source)
    {
        _points = new Vector2[PointCount];
        for (int i = 1; i < PointCount - 1; i++)
            _points[i] = source.GetPoint(i, Vector2.zero, Vector2.zero, true);
    }

    /// <summary>정규화한 경로 진행도로 위치를 계산한다.</summary>
    public Vector2 Evaluate(float progress, Vector2 start, Vector2 end, bool mirror)
    {
        float scaled = Mathf.Clamp01(progress) * 3f;
        int segment = Mathf.Min(2, Mathf.FloorToInt(scaled));
        float t = scaled - segment, u = 1f - t;
        int index = segment * 3;
        return u * u * u * GetPoint(index, start, end, mirror)
            + 3f * u * u * t * GetPoint(index + 1, start, end, mirror)
            + 3f * u * t * t * GetPoint(index + 2, start, end, mirror)
            + t * t * t * GetPoint(index + 3, start, end, mirror);
    }
}
