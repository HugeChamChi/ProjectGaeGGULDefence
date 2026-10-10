using Spine;
using Spine.Unity;
using UnityEngine;

/// <summary>Plays the laboratory take on the real drone's visual child; combat placement stays unchanged.</summary>
[DefaultExecutionOrder(250)]
public sealed class GammanChargeWarpVisual : MonoBehaviour
{
    private SkeletonAnimation _skeleton;
    private MeshRenderer _renderer;
    private Bone _center, _muzzle;
    private Vector3 _localPosition, _localScale;
    private Quaternion _localRotation;
    private bool _wasHidden, _running;
    private DroneHackingData _data;
    private BossBase _target;
    private DroneBeamVisual _beam;
    private readonly LineRenderer[] _streaks = new LineRenderer[8];
    private float _elapsed;
    private int _lastBeat = -1;
    private int _sortingLayer, _sortingOrder;

    /// <summary>Captures the visual pose for exact restoration, including cancellation and pool return.</summary>
    public void Begin(DroneUnit drone, BossBase target, DroneHackingData data)
    {
        Clear();
        _skeleton = drone != null ? drone.GetComponent<SpineActorVisual>()?.Skeleton : null;
        _data = data; _target = target;
        if (_skeleton == null || data.TeleportTake?.Beats == null || data.TeleportTake.Beats.Length == 0) return;
        _renderer = _skeleton.GetComponent<MeshRenderer>();
        _wasHidden = _renderer.forceRenderingOff;
        _sortingLayer = _renderer.sortingLayerID; _sortingOrder = _renderer.sortingOrder;
        _localPosition = _skeleton.transform.localPosition;
        _localRotation = _skeleton.transform.localRotation;
        _localScale = _skeleton.transform.localScale;
        _center = _skeleton.Skeleton.FindBone("center");
        _muzzle = _skeleton.Skeleton.FindBone("muzzle");
        if (_beam == null)
        {
            var go = new GameObject("ChargeWarpBeam");
            go.transform.SetParent(transform, false);
            _beam = go.AddComponent<DroneBeamVisual>();
            for (int i = 0; i < _streaks.Length; i++)
            {
                var line = new GameObject("WarpStreak" + i);
                line.transform.SetParent(transform, false);
                var renderer = line.AddComponent<LineRenderer>();
                renderer.useWorldSpace = true; renderer.positionCount = 2;
                renderer.sortingLayerName = "FX"; renderer.sortingOrder = 44;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _streaks[i] = renderer;
            }
        }
        _beam.Initialize(data.BeamMaterial, data.MarkGlowMaterial);
        foreach (var line in _streaks) line.sharedMaterial = data.LineMaterial;
        _running = true; _lastBeat = -1;
        Draw(0);
    }

    /// <summary>Samples authored seconds; the caller owns pause, stun and cancellation timing.</summary>
    public void Draw(float elapsed) { _elapsed = Mathf.Max(0, elapsed); Render(); }

    private void LateUpdate() { if (_running) Render(); }

    private void Render()
    {
        if (!_running) return;
        if (_skeleton == null || _target == null || !_skeleton.gameObject.activeInHierarchy) { Clear(); return; }
        var take = _data.TeleportTake;
        float elapsed = _elapsed;
        int index = 0;
        while (index < take.Beats.Length - 1 && elapsed >= take.Beats[index].Duration)
            elapsed -= Mathf.Max(0, take.Beats[index++].Duration);
        var beat = take.Beats[index];
        float p = Mathf.Clamp01(elapsed / Mathf.Max(.001f, beat.Duration));
        if (index != _lastBeat)
        {
            _lastBeat = index;
            if (beat.Fire && _skeleton.Skeleton.Data.FindAnimation("attack") != null)
                _skeleton.AnimationState.SetAnimation(0, "attack", false);
        }
        var body = _skeleton.transform;
        body.localPosition = _localPosition;
        body.localRotation = _localRotation;
        body.localScale = Vector3.Scale(_localScale, new Vector3(1 - beat.Squeeze * p * .75f, 1 + beat.Squeeze * p * .25f, 1));
        Vector3 target = _target.transform.position;
        Vector3 center = _center != null ? _center.GetWorldPosition(body) : body.position;
        if (!beat.AtHome)
        {
            Vector3 point = target + Vector3.Lerp(beat.From, beat.To, 1 - Mathf.Pow(1 - p, 2));
            Vector3 facing = _muzzle != null ? _muzzle.GetWorldPosition(body) - center : Vector3.left;
            Vector3 aim = target - point;
            body.rotation = Quaternion.AngleAxis(Vector2.SignedAngle(facing, aim), Vector3.forward) * body.rotation;
            center = _center != null ? _center.GetWorldPosition(body) : body.position;
            body.position += point - center;
            center = point;
        }
        _renderer.forceRenderingOff = _wasHidden || !beat.Body;
        if (!beat.AtHome) { _renderer.sortingLayerName = "FX"; _renderer.sortingOrder = 43; }
        Vector3 muzzle = _muzzle != null ? _muzzle.GetWorldPosition(body) : center;
        float charge = beat.Fire ? .3f : beat.Charge * (.3f + .7f * p);
        Vector3 end = Vector3.Lerp(muzzle, target, Mathf.Clamp01(elapsed / Mathf.Max(.001f, _data.BeamExtensionSeconds)));
        _beam.Draw(muzzle, end, take.Tint, take.BeamWidth * (1 - .65f * Mathf.Pow(p, 8)),
            beat.Fire ? 1 : 0, beat.Body ? charge : 0, beat.Fire ? .8f : 0, .24f + charge * .34f, .42f);
        for (int i = 0; i < _streaks.Length; i++)
        {
            float snap = Mathf.Clamp01(elapsed / .085f);
            var line = _streaks[i]; line.enabled = beat.Snap && snap < 1;
            if (!line.enabled) continue;
            Vector3 pos = center + new Vector3((i - 3.5f) * .09f, (i % 2 == 0 ? 1 : -1) * snap * .35f, -.08f);
            float length = .4f + i % 3 * .25f;
            line.SetPosition(0, pos - Vector3.up * length * .5f);
            line.SetPosition(1, pos + Vector3.up * length * .5f);
            line.startWidth = .024f * (1 - snap); line.endWidth = .003f;
            Color color = Color.Lerp(take.Tint, Color.white, .75f); color.a = 1 - snap;
            line.startColor = line.endColor = color;
        }
    }

    /// <summary>Restores the actual drone even when the boss dies or the cast is interrupted.</summary>
    public void Clear()
    {
        if (_running && _skeleton != null)
        {
            _skeleton.transform.localPosition = _localPosition;
            _skeleton.transform.localRotation = _localRotation;
            _skeleton.transform.localScale = _localScale;
            if (_renderer != null)
            {
                _renderer.forceRenderingOff = _wasHidden;
                _renderer.sortingLayerID = _sortingLayer; _renderer.sortingOrder = _sortingOrder;
            }
        }
        _running = false;
        _beam?.Clear();
        foreach (var line in _streaks) if (line != null) line.enabled = false;
    }

    private void OnDisable() => Clear();
    private void OnDestroy() => Clear();
}
