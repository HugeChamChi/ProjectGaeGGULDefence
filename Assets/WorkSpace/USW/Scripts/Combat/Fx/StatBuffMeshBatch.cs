using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>씬의 버프 도형을 머티리얼·레이어·정렬 순서별 공용 메시로 그린다.</summary>
[DefaultExecutionOrder(10000)]
public sealed class StatBuffMeshBatch : MonoBehaviour
{
    private readonly struct Entry
    {
        public readonly SpriteRenderer Renderer;
        public readonly bool Glow;
        public Entry(SpriteRenderer renderer, bool glow) { Renderer = renderer; Glow = glow; }
    }

    private sealed class Geometry
    {
        public readonly Vector2[] Vertices;
        public readonly Vector2[] UV;
        public readonly ushort[] Triangles;
        public Geometry(Sprite sprite) { Vertices = sprite.vertices; UV = sprite.uv; Triangles = sprite.triangles; }
    }

    private sealed class Batch
    {
        public Mesh Mesh;
        public MeshRenderer Renderer;
        public readonly List<SpriteRenderer> Sprites = new();
        public readonly List<Vector3> Vertices = new();
        public readonly List<Vector2> UV = new();
        public readonly List<Color> Colors = new();
        public readonly List<int> Indices = new();
    }

    private readonly List<Entry> _entries = new();
    private readonly Dictionary<Sprite, Geometry> _geometry = new();
    private readonly Dictionary<(Material, Texture, bool), Material> _materials = new();
    private readonly Dictionary<(int, int, Material), Batch> _batches = new();
    private Shader _shader;
    private System.Comparison<SpriteRenderer> _compareSprites;
    private Vector3 _cameraPosition, _cameraForward;
    private bool _orthographic;
    private bool _visible = true, _glowVisible = true;
    public bool UseBatching { get; set; } = true;
    /// <summary>공통 앞/뒤 정렬을 쓰는 실험에서만 파츠별 순서를 두 밴드로 합친다.</summary>
    public bool MergeSharedLayers { get; set; }
    public int ActiveBatchCount { get; private set; }
    public int SubmittedSpriteCount { get; private set; }
    public int SubmittedVertexCount { get; private set; }

    public void Initialize(Shader shader) => _shader = shader;
    private void Awake() => _compareSprites = CompareSprites;

    private int CompareSprites(SpriteRenderer a, SpriteRenderer b)
    {
        int order = a.sortingOrder.CompareTo(b.sortingOrder);
        if (order != 0) return order;
        float da = _orthographic ? Vector3.Dot(a.bounds.center, _cameraForward) : (a.bounds.center - _cameraPosition).sqrMagnitude;
        float db = _orthographic ? Vector3.Dot(b.bounds.center, _cameraForward) : (b.bounds.center - _cameraPosition).sqrMagnitude;
        int depth = db.CompareTo(da);
        return depth != 0 ? depth : b.GetInstanceID().CompareTo(a.GetInstanceID());
    }

    public void Register(SpriteRenderer renderer, bool glow)
    {
        _entries.Add(new Entry(renderer, glow));
        renderer.forceRenderingOff = true;
    }

    public void SetVisibility(bool visible, bool glow)
    {
        _visible = visible; _glowVisible = glow;
    }

    private Material GetMaterial(SpriteRenderer renderer, bool glow)
    {
        var source = renderer.sharedMaterial;
        var key = (source, (Texture)renderer.sprite.texture, glow);
        if (_materials.TryGetValue(key, out var material)) return material;
        material = new Material(_shader) { name = "UnitBuffBatch_" + source.name, hideFlags = HideFlags.DontSave,
            mainTexture = renderer.sprite.texture, renderQueue = source.renderQueue };
        material.SetFloat("_Glow", glow ? 1f : 0f);
        material.SetColor("_Color", source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
        material.SetFloat("_Intensity", glow && source.HasProperty("_Intensity") ? source.GetFloat("_Intensity") : 1f);
        material.SetFloat("_Opacity", glow && source.HasProperty("_Opacity") ? source.GetFloat("_Opacity") : 1f);
        _materials.Add(key, material);
        return material;
    }

    private Batch GetBatch(SpriteRenderer sprite, bool glow)
    {
        var material = GetMaterial(sprite, glow);
        int order = MergeSharedLayers ? (sprite.sortingOrder < 0 ? -30000 : 30000) + (glow ? 1 : 0) : sprite.sortingOrder;
        var key = (sprite.sortingLayerID, order, material);
        if (_batches.TryGetValue(key, out var batch)) return batch;
        var go = new GameObject("BuffBatch_" + order + (glow ? "_Glow" : "_Color"));
        go.transform.SetParent(transform, false);
        var mesh = new Mesh { name = go.name, hideFlags = HideFlags.DontSave };
        mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerID = sprite.sortingLayerID;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        batch = new Batch { Mesh = mesh, Renderer = renderer };
        _batches.Add(key, batch);
        return batch;
    }

    private void LateUpdate()
    {
        ActiveBatchCount = SubmittedSpriteCount = SubmittedVertexCount = 0;
        foreach (var batch in _batches.Values) batch.Sprites.Clear();
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            var sprite = entry.Renderer;
            if (sprite == null) { _entries.RemoveAt(i); continue; }
            bool visible = _visible && (_glowVisible || !entry.Glow);
            sprite.forceRenderingOff = UseBatching || !visible;
            if (!UseBatching || !visible || !sprite.enabled || !sprite.gameObject.activeInHierarchy || sprite.sprite == null) continue;
            GetBatch(sprite, entry.Glow).Sprites.Add(sprite);
            SubmittedSpriteCount++;
        }
        var camera = Camera.main;
        if (camera != null)
        {
            _cameraPosition = camera.transform.position; _cameraForward = camera.transform.forward;
            _orthographic = camera.orthographic;
        }
        foreach (var batch in _batches.Values)
        {
            batch.Renderer.enabled = batch.Sprites.Count > 0;
            if (batch.Sprites.Count == 0) continue;
            // 같은 정렬 순서에서는 기존 투명 스프라이트처럼 먼 도형부터 그린다.
            if (camera != null)
                batch.Sprites.Sort(_compareSprites);
            batch.Vertices.Clear(); batch.UV.Clear(); batch.Colors.Clear(); batch.Indices.Clear();
            var worldToLocal = transform.worldToLocalMatrix;
            foreach (var renderer in batch.Sprites)
            {
                var sprite = renderer.sprite;
                if (!_geometry.TryGetValue(sprite, out var geometry)) _geometry.Add(sprite, geometry = new Geometry(sprite));
                var matrix = worldToLocal * renderer.transform.localToWorldMatrix;
                int offset = batch.Vertices.Count;
                foreach (var vertex in geometry.Vertices)
                {
                    var point = new Vector3(renderer.flipX ? -vertex.x : vertex.x, renderer.flipY ? -vertex.y : vertex.y, 0f);
                    batch.Vertices.Add(matrix.MultiplyPoint3x4(point));
                    batch.Colors.Add(renderer.color);
                }
                batch.UV.AddRange(geometry.UV);
                foreach (var index in geometry.Triangles) batch.Indices.Add(offset + index);
            }
            batch.Mesh.Clear();
            batch.Mesh.indexFormat = batch.Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            batch.Mesh.SetVertices(batch.Vertices);
            batch.Mesh.SetColors(batch.Colors);
            batch.Mesh.SetUVs(0, batch.UV);
            batch.Mesh.SetTriangles(batch.Indices, 0);
            SubmittedVertexCount += batch.Vertices.Count;
            ActiveBatchCount++;
        }
    }

    private void OnDestroy()
    {
        foreach (var batch in _batches.Values) Destroy(batch.Mesh);
        foreach (var material in _materials.Values) Destroy(material);
    }
}
