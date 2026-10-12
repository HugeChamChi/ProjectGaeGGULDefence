using UnityEngine;

/// <summary>스프라이트의 실제 삼각형 메시 면적에서 표식 좌표를 고른다. 투명 캔버스 bounds는 사용하지 않는다.</summary>
public sealed class HackingSpriteSurface
{
    private readonly Vector2[] _vertices;
    private readonly ushort[] _triangles;
    private readonly float[] _areas;
    private readonly float _totalArea;

    /// <summary>한 번 읽은 Sprite 메시를 캐시한다. 텍스처 Read/Write나 GPU 읽기는 필요 없다.</summary>
    public HackingSpriteSurface(Sprite sprite)
    {
        _vertices = sprite.vertices; _triangles = sprite.triangles;
        _areas = new float[_triangles.Length / 3];
        for (int i = 0; i < _areas.Length; i++)
        {
            Vector2 a = _vertices[_triangles[i * 3]], b = _vertices[_triangles[i * 3 + 1]], c = _vertices[_triangles[i * 3 + 2]];
            Vector2 ab = b - a, ac = c - a;
            _totalArea += Mathf.Abs(ab.x * ac.y - ab.y * ac.x) * .5f;
            _areas[i] = _totalArea;
        }
    }

    /// <summary>면적별 층화 표본을 삼각형 내부로 배치한다. inset은 삼각형 중심 쪽 저작 비율이다.</summary>
    public bool TryGetPoint(int index, int count, float inset, out Vector2 point)
    {
        point = Vector2.zero;
        if (_totalArea <= 0f || _areas.Length == 0 || count <= 0) return false;
        const float GoldenAngle = 137.508f;
        int slot = index * 7 % count;
        float target = (slot + .5f) / count * _totalArea;
        int triangle = 0;
        while (triangle < _areas.Length - 1 && target > _areas[triangle]) triangle++;
        float radius = Mathf.Sqrt((index + .5f) / count);
        float phase = Mathf.Repeat((index * GoldenAngle + 20f) / 360f, 1f);
        Vector2 a = _vertices[_triangles[triangle * 3]], b = _vertices[_triangles[triangle * 3 + 1]], c = _vertices[_triangles[triangle * 3 + 2]];
        point = a * (1f - radius) + b * (radius * (1f - phase)) + c * (radius * phase);
        point = Vector2.Lerp(point, (a + b + c) / 3f, Mathf.Clamp01(inset));
        return true;
    }
}
