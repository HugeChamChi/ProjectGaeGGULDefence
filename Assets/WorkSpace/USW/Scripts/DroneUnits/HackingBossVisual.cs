using UnityEngine;
using Spine;
using Spine.Unity;

/// <summary>HackBloom의 빛점 축적/연쇄 기폭을 실제 잔고에 연결한 고정 개수 시각 풀.</summary>
public sealed class HackingBossVisual : MonoBehaviour
{
    private const int MarkCount = 8;
    private const float PopSpacing = .035f;
    private const float PopDuration = .28f;
    private readonly SpriteRenderer[] _marks = new SpriteRenderer[MarkCount];
    private readonly float[] _popAt = new float[MarkCount];
    private Color _color;
    private int _visible;
    private float _ratio;
    private BossBase _initialBoss;
    private DroneHackingData _initialData;
    private int _setupFrame;
    private SkeletonAnimation _skeleton;
    private readonly Bone[] _bones = new Bone[MarkCount];
    private readonly Vector2[] _boneLocal = new Vector2[MarkCount];
    /// <summary>보스 렌더러 범위 안에 표식을 배치한다. 스택 수만큼 오브젝트를 만들지 않는다.</summary>
    public void Initialize(BossBase boss, DroneHackingData data)
    {
        _initialBoss = boss; _initialData = data; _setupFrame = Time.frameCount;
    }
    private void BuildMarks(BossBase boss, DroneHackingData data)
    {
        _color = data.HackColor;
        _skeleton = boss.GetComponentInChildren<SkeletonAnimation>();
        var renderers = boss.GetComponentsInChildren<Renderer>();
        var bounds = new Bounds(boss.transform.position, Vector3.one);
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        }
        for (int i = 0; i < MarkCount; i++)
        {
            var go = new GameObject("HackMark" + i);
            go.transform.SetParent(transform, false);
            float angle = i * Mathf.PI * 2f / MarkCount;
            go.transform.position = bounds.center + new Vector3(Mathf.Cos(angle) * bounds.extents.x * .6f,
                Mathf.Sin(angle) * bounds.extents.y * .5f, -.1f);
            var mark = go.AddComponent<SpriteRenderer>();
            mark.sprite = HackGaugeSprites.Disc;
            mark.sortingLayerName = "FX";
            mark.sortingOrder = 35;
            mark.color = _color;
            _marks[i] = mark;
            _popAt[i] = -1f;
            if (_skeleton != null && _skeleton.Skeleton != null)
            {
                Vector3 local = _skeleton.transform.InverseTransformPoint(go.transform.position);
                float closest = float.MaxValue;
                foreach (var bone in _skeleton.Skeleton.Bones)
                {
                    var pose = bone.AppliedPose;
                    float distance = ((Vector2)local - new Vector2(pose.WorldX, pose.WorldY)).sqrMagnitude;
                    if (distance < closest) { closest = distance; _bones[i] = bone; }
                }
                if (_bones[i] != null)
                {
                    _bones[i].AppliedPose.WorldToLocal(local.x, local.y, out float x, out float y);
                    _boneLocal[i] = new Vector2(x, y);
                }
            }
        }
    }
    /// <summary>잔고 비율만큼 표식을 표시한다.</summary>
    public void SetStacks(int stacks, int capacity)
    {
        _ratio = Mathf.Clamp01(stacks / (float)Mathf.Max(1, capacity));
        _visible = Mathf.Clamp(Mathf.CeilToInt(_ratio * MarkCount), 0, MarkCount);
    }
    /// <summary>남은 표식을 보존하고 소비분이 차지한 표식만 순서대로 터뜨린다.</summary>
    public void Detonate(int remaining, int capacity, int consumed)
    {
        int before = _visible;
        SetStacks(remaining, capacity);
        if (consumed <= 0) return;
        for (int i = _visible; i < before; i++) _popAt[i] = Time.time + (i - _visible) * PopSpacing;
        if (before == _visible && before > 0) _popAt[before - 1] = Time.time;
    }
    private void LateUpdate()
    {
        if (_marks[0] == null)
        {
            if (_initialBoss == null || Time.frameCount <= _setupFrame + 1) return;
            BuildMarks(_initialBoss, _initialData);
        }
        for (int i = 0; i < MarkCount; i++)
        {
            var mark = _marks[i];
            if (mark == null) continue;
            float elapsed = _popAt[i] < 0f ? PopDuration : Time.time - _popAt[i];
            bool popping = elapsed < PopDuration;
            mark.enabled = i < _visible || popping;
            float p = Mathf.Clamp01(elapsed / PopDuration);
            mark.transform.localScale = Vector3.one * (popping ? .12f + p * .65f : .12f);
            Color tint = _ratio < .5f ? _color : Color.Lerp(new Color(.3f, .62f, 1f), new Color(.6f, .48f, 1f), (_ratio - .5f) * 2);
            if (_ratio >= 1f) tint = Color.Lerp(tint, Color.white, .5f + .5f * Mathf.Sin(Time.time * 6f));
            mark.color = popping ? new Color(1f, 1f, 1f, 1f - p) : tint;
            if (!popping && _skeleton != null && _bones[i] != null)
            {
                _bones[i].AppliedPose.LocalToWorld(_boneLocal[i].x, _boneLocal[i].y, out float x, out float y);
                Vector3 position = _skeleton.transform.TransformPoint(new Vector3(x, y, 0));
                position.z = transform.position.z - .1f;
                mark.transform.position = position;
            }
            if (!popping) _popAt[i] = -1f;
        }
    }
}
