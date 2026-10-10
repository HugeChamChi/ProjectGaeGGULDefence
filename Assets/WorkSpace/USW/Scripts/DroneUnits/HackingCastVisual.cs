using UnityEngine;

/// <summary>해킹탄과 실험실 원본 타임라인의 감망 워프 연출을 재생한다.</summary>
public sealed class HackingCastVisual : MonoBehaviour
{
    private GammanChargeWarpVisual _warp;
    private DroneBeamVisual _beam;
    private BossBase _target;
    private Vector3 _origin;
    private DroneHackingData _data;
    private bool _consumer;
    private float _progress, _recovery;
    private bool _running;
    /// <summary>해킹탄 또는 실제 드론 외형의 워프 타임라인을 시작한다.</summary>
    public void Begin(DroneUnit source, BossBase target, DroneHackingData data, bool consumer)
    {
        Clear();
        _target = target; _data = data; _consumer = consumer;
        _origin = source != null ? source.MuzzlePosition : transform.position;
        _running = true; _recovery = -1f;
        if (consumer)
        {
            if (_warp == null) _warp = gameObject.AddComponent<GammanChargeWarpVisual>();
            _warp.Begin(source, target, data);
            return;
        }
        if (_beam == null)
        {
            var go = new GameObject("HackingLabBeam");
            go.transform.SetParent(transform, false);
            _beam = go.AddComponent<DroneBeamVisual>();
        }
        _beam.Initialize(data.BeamMaterial, data.MarkGlowMaterial);
        Draw(0);
    }
    /// <summary>전투 시간 진행률로 움직이며 피해를 만들지 않는다.</summary>
    public void Draw(float progress)
    {
        _progress = Mathf.Clamp01(progress);
        Render();
    }
    /// <summary>접촉 이후 원본 타임라인의 반동·점멸 복귀를 진행한다.</summary>
    public void Recover(float progress) { _recovery = Mathf.Clamp01(progress); Render(); }
    private void LateUpdate() { if (_running) Render(); }
    private void Render()
    {
        if (!_running || _target == null) { Clear(); return; }
        if (_consumer)
        {
            _warp.Draw(_recovery < 0 ? _progress * _data.ConsumerContactSeconds
                : _data.ConsumerContactSeconds + _recovery * _data.ConsumerRecoverySeconds);
            return;
        }
        Vector3 contact = _target.transform.position;
        float fade = _recovery < 0 ? 1 : 1 - _recovery;
        Vector3 tip = Vector3.Lerp(_origin, contact, _progress);
        Vector3 tail = Vector3.Lerp(_origin, contact, Mathf.Max(0, _progress - .18f));
        _beam.Draw(tail, tip, _data.HackColor, _data.CastBeamWidth, fade, .35f * fade, fade);
    }
    /// <summary>취소/복귀 시 모든 외형을 숨긴다.</summary>
    public void Clear()
    {
        _running = false;
        _beam?.Clear();
        _warp?.Clear();
    }
    private void OnDisable() => Clear();
}
