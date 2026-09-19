using UnityEngine;

/// <summary>외부 이미지 없이 머리 위를 도는 별 세 개를 한 메시로 렌더링한다.</summary>
public sealed class StunStarsVisual : MonoBehaviour
{
    private const int StarCount = 3;
    private const int Points = 10;
    private readonly Vector3[] _vertices = new Vector3[StarCount * (Points + 1)];
    private Mesh _mesh;
    private Transform _owner;
    private SpriteRenderer _body;
    private StunVisualSettings _settings;
    private float _angle;

    /// <summary>공용 머티리얼을 사용하며 유닛 호흡 스케일과 독립적인 월드 크기를 유지한다.</summary>
    public void Initialize(UnitBase owner, StunVisualSettings settings)
    {
        _owner = owner.transform;
        _body = owner.GetComponentInChildren<SpriteRenderer>();
        _settings = settings;
        _mesh = new Mesh { name = "Stun stars" };
        _mesh.MarkDynamic();
        var triangles = new int[StarCount * Points * 3];
        var colors = new Color[_vertices.Length];
        for (int star = 0; star < StarCount; star++)
        {
            int first = star * (Points + 1);
            for (int point = 0; point < Points; point++)
            {
                int index = (star * Points + point) * 3;
                triangles[index] = first;
                triangles[index + 1] = first + 1 + point;
                triangles[index + 2] = first + 1 + (point + 1) % Points;
            }
        }
        for (int i = 0; i < colors.Length; i++) colors[i] = settings.Color;
        _mesh.vertices = _vertices;
        _mesh.triangles = triangles;
        _mesh.colors = colors;
        gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
        var renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = settings.Material;
        if (_body != null) renderer.sortingLayerID = _body.sortingLayerID;
        renderer.sortingOrder = (_body != null ? _body.sortingOrder : 0) + 10;
        RenderStars();
    }

    private void LateUpdate()
    {
        if (_owner == null) { Destroy(gameObject); return; }
        if (Time.timeScale <= 0f) return;
        _angle += _settings.DegreesPerSecond * Mathf.Deg2Rad * Time.deltaTime;
        RenderStars();
    }

    private void RenderStars()
    {
        var anchor = _owner.position;
        if (_body != null) anchor.y = _body.bounds.max.y;
        anchor.y += _settings.HeadOffset;
        transform.position = anchor;
        for (int star = 0; star < StarCount; star++)
        {
            float orbit = _angle + star * Mathf.PI * 2f / StarCount;
            var center = new Vector3(Mathf.Cos(orbit) * _settings.OrbitRadius, Mathf.Sin(orbit) * _settings.OrbitHeight, 0f);
            int first = star * (Points + 1);
            _vertices[first] = center;
            for (int point = 0; point < Points; point++)
            {
                float angle = Mathf.PI * 0.5f + point * Mathf.PI * 2f / Points;
                float radius = _settings.StarRadius * (point % 2 == 0 ? 1f : 0.45f);
                _vertices[first + point + 1] = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            }
        }
        _mesh.vertices = _vertices;
        _mesh.RecalculateBounds();
    }

    private void OnDestroy() { if (_mesh != null) Destroy(_mesh); }
}
