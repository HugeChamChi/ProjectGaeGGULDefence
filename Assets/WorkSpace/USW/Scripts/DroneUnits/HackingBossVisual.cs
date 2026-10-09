using UnityEngine;
using UnityEngine.Rendering;
using Spine;
using Spine.Unity;

/// <summary>5스택마다 FXLab의 후광·코어 표식을 보스 뼈에 붙이는 고정 시각 풀.</summary>
public sealed class HackingBossVisual : MonoBehaviour
{
    private const float PopSpacing = .035f, PopDuration = .28f, AttachDuration = .2f, GoldenAngle = 137.508f;
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private sealed class Mark
    {
        public Renderer Glow, Core, Ring;
        public Bone Bone;
        public Vector2 Local;
        public Vector3 FallbackLocal, PopPosition;
        public float BornAt = -1f, PopAt = -1f;
        public bool Attached;
    }
    private Mark[] _marks;
    private MaterialPropertyBlock _block;
    private BossBase _boss;
    private DroneHackingData _data;
    private SkeletonAnimation _skeleton;
    private int _setupFrame, _visible;
    private float _ratio;

    /// <summary>보스 메시 갱신 뒤 몸에 표식을 고정한다.</summary>
    public void Initialize(BossBase boss, DroneHackingData data)
    {
        _boss = boss; _data = data; _setupFrame = Time.frameCount;
    }

    /// <summary>완료된 5스택 묶음만 표시한다. 0~4는0개, 50은10개.</summary>
    public void SetStacks(int stacks, int capacity)
    {
        int perMark = _data != null ? Mathf.Max(1, _data.StacksPerMark) : 5;
        _ratio = Mathf.Clamp01(stacks / (float)Mathf.Max(1, capacity));
        _visible = Mathf.Clamp(stacks, 0, capacity) / perMark;
    }

    /// <summary>소비로 사라진 묶음만 차례로 터뜨리고 나머지 빛점은 유지한다.</summary>
    public void Detonate(int remaining, int capacity, int consumed)
    {
        int before = _visible;
        SetStacks(remaining, capacity);
        if (_marks == null || consumed <= 0) return;
        for (int i = _visible; i < Mathf.Min(before, _marks.Length); i++)
        {
            var mark = _marks[i];
            if (!mark.Attached) continue;
            mark.Attached = false;
            mark.PopAt = Time.time + (i - _visible) * PopSpacing;
            mark.PopPosition = Position(mark);
        }
    }

    private void BuildMarks()
    {
        _skeleton = _boss.GetComponentInChildren<SkeletonAnimation>();
        Renderer renderer = _skeleton != null ? _skeleton.GetComponent<MeshRenderer>() : _boss.GetComponentInChildren<SpriteRenderer>();
        if (renderer == null || renderer.bounds.size.sqrMagnitude < .0001f) return;
        Bounds bounds = renderer.bounds;
        var centerBone = _skeleton != null ? _skeleton.Skeleton?.FindBone("body2") : null;
        Vector3 center = centerBone != null ? centerBone.GetWorldPosition(_skeleton.transform) : bounds.center;
        center.z = _boss.transform.position.z - .65f;
        _block = new MaterialPropertyBlock();
        _marks = new Mark[Mathf.Max(1, _data.Capacity / Mathf.Max(1, _data.StacksPerMark))];
        int order = Mathf.Max(54, renderer.sortingOrder + 2);
        for (int i = 0; i < _marks.Length; i++)
        {
            var mark = new Mark
            {
                Glow = Quad("HackGlow" + i, _data.MarkGlowMaterial, order),
                Core = Quad("HackCore" + i, _data.MarkGlowMaterial, order + 1),
                Ring = Quad("HackRing" + i, _data.MarkRingMaterial, order + 2)
            };
            int slot = i * 7 % _marks.Length;
            float radius = Mathf.Sqrt((slot + .5f) / _marks.Length);
            float angle = (slot * GoldenAngle + 20f) * Mathf.Deg2Rad;
            Vector3 world = center + new Vector3(Mathf.Cos(angle) * bounds.extents.x * .55f * radius,
                Mathf.Sin(angle) * bounds.extents.y * .42f * radius, 0f);
            mark.FallbackLocal = _boss.transform.InverseTransformPoint(world);
            if (_skeleton != null && _skeleton.Skeleton != null)
            {
                Vector3 local = _skeleton.transform.InverseTransformPoint(world);
                float closest = float.MaxValue;
                foreach (var bone in _skeleton.Skeleton.Bones)
                {
                    if (!IsBodyBone(bone.Data.Name)) continue;
                    var pose = bone.AppliedPose;
                    float distance = ((Vector2)local - new Vector2(pose.WorldX, pose.WorldY)).sqrMagnitude;
                    if (distance < closest) { closest = distance; mark.Bone = bone; }
                }
                if (mark.Bone != null)
                {
                    mark.Bone.AppliedPose.WorldToLocal(local.x, local.y, out float x, out float y);
                    mark.Local = new Vector2(x, y);
                }
            }
            _marks[i] = mark;
        }
    }

    private static bool IsBodyBone(string name)
        => name == "body1" || name == "body2" || name == "head" || name == "nose"
        || name == "armL" || name == "armR" || name == "soulderL" || name == "soulderR"
        || name == "guntletL" || name == "guntletR" || name == "handL" || name == "handR";

    private Vector3 Position(Mark mark)
    {
        Vector3 position = _boss.transform.TransformPoint(mark.FallbackLocal);
        if (_skeleton != null && mark.Bone != null)
        {
            mark.Bone.AppliedPose.LocalToWorld(mark.Local.x, mark.Local.y, out float x, out float y);
            position = _skeleton.transform.TransformPoint(new Vector3(x, y, 0));
        }
        position.z = _boss.transform.position.z - .65f;
        return position;
    }

    private void LateUpdate()
    {
        if (_boss == null || _data == null) return;
        if (_marks == null)
        {
            if (Time.frameCount <= _setupFrame + 1) return;
            BuildMarks();
            if (_marks == null) return;
        }
        float now = Time.time;
        Color tint = _ratio < .5f ? _data.HackColor
            : Color.Lerp(new Color(.3f, .62f, 1f), new Color(.6f, .48f, 1f), (_ratio - .5f) * 2);
        for (int i = 0; i < _marks.Length; i++)
        {
            var mark = _marks[i];
            if (i < _visible && !mark.Attached)
            {
                mark.Attached = true; mark.BornAt = now; mark.PopAt = -1f;
            }
            else if (i >= _visible && mark.Attached)
            {
                mark.Attached = false; mark.PopAt = -1f;
            }
            if (mark.PopAt >= 0f)
            {
                float q = Mathf.Clamp01((now - mark.PopAt) / PopDuration);
                float fade = 1f - q, ease = 1f - Mathf.Pow(1f - q, 3);
                Draw(mark.Glow, mark.PopPosition, _data.MarkGlowSize * (1f + ease), tint, fade * fade);
                Draw(mark.Core, mark.PopPosition, _data.MarkCoreSize * (1f + ease), Color.white, fade);
                Draw(mark.Ring, mark.PopPosition, Mathf.Lerp(.15f, 1.05f, ease), tint, fade);
                if (q >= 1f) mark.PopAt = -1f;
                continue;
            }
            if (!mark.Attached) { Hide(mark); continue; }
            Vector3 position = Position(mark);
            float born = Mathf.Clamp01((now - mark.BornAt) / AttachDuration);
            float pulse = .5f + .5f * Mathf.Sin(now * 5f + i * GoldenAngle);
            float popScale = 1f + .7f * (1f - born) * (1f - born);
            Color color = _ratio >= 1f ? Color.Lerp(tint, Color.white, pulse * .5f) : tint;
            Draw(mark.Glow, position, (_data.MarkGlowSize + .15f * pulse) * popScale, color, .75f + .2f * pulse);
            Draw(mark.Core, position, (_data.MarkCoreSize + .05f * pulse) * popScale, Color.white * 1.2f, .95f);
            Draw(mark.Ring, position, Mathf.Lerp(.12f, .55f, 1f - (1f - born) * (1f - born)), color, 1f - born);
        }
    }

    private Renderer Quad(string name, Material material, int order)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = "FX"; renderer.sortingOrder = order;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.enabled = false;
        return renderer;
    }

    private void Draw(Renderer renderer, Vector3 position, float size, Color color, float alpha)
    {
        renderer.enabled = alpha > .005f && renderer.sharedMaterial != null;
        if (!renderer.enabled) return;
        renderer.transform.position = position;
        Vector3 scale = transform.lossyScale;
        renderer.transform.localScale = new Vector3(size / Mathf.Max(.0001f, Mathf.Abs(scale.x)), size / Mathf.Max(.0001f, Mathf.Abs(scale.y)), 1f);
        color.a = alpha;
        _block.Clear(); _block.SetColor(ColorId, color); renderer.SetPropertyBlock(_block);
    }

    private static void Hide(Mark mark) => mark.Glow.enabled = mark.Core.enabled = mark.Ring.enabled = false;

    private void OnDisable()
    {
        if (_marks == null) return;
        foreach (var mark in _marks) { Hide(mark); mark.Attached = false; mark.PopAt = -1f; }
    }
}
