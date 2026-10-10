using Spine.Unity;
using UnityEngine;

/// <summary>실험실 셰이더와 Spine 사본으로 해킹탄·감망 접촉을 재생한다. 전투 위치는 바꾸지 않는다.</summary>
public sealed class HackingCastVisual : MonoBehaviour
{
    private SkeletonAnimation _copy;
    private MeshRenderer _copyRenderer;
    private DroneBeamVisual _beam;
    private BossBase _target;
    private Vector3 _origin;
    private DroneHackingData _data;
    private bool _consumer;
    private float _progress, _recovery;
    private bool _running;
    private DroneUnit _source;
    /// <summary>전투 컴포넌트 없는 Spine 사본과 공유 셰이더 빔을 만들고 재사용한다.</summary>
    public void Begin(DroneUnit source, BossBase target, DroneHackingData data, bool consumer)
    {
        Clear();
        _target = target; _data = data; _consumer = consumer;
        _source = source;
        _origin = source != null ? source.MuzzlePosition : transform.position;
        if (_beam == null)
        {
            var go = new GameObject("HackingLabBeam");
            go.transform.SetParent(transform, false);
            _beam = go.AddComponent<DroneBeamVisual>();
        }
        _beam.Initialize(data.BeamMaterial, data.MarkGlowMaterial);
        var original = source != null ? source.GetComponent<SpineActorVisual>()?.Skeleton : null;
        if (consumer && original != null)
        {
            if (_copy != null && _copy.skeletonDataAsset != original.skeletonDataAsset)
            {
                Destroy(_copy.gameObject);
                _copy = null;
            }
            if (_copy == null)
            {
                _copy = SkeletonAnimation.NewSkeletonAnimationGameObject(original.skeletonDataAsset).skeletonAnimation;
                _copy.name = "HackTeleportSpine";
                _copy.transform.SetParent(transform, false);
                _copyRenderer = _copy.GetComponent<MeshRenderer>();
                _copyRenderer.sortingLayerName = "FX"; _copyRenderer.sortingOrder = 43;
            }
            _copy.gameObject.SetActive(true);
            _copy.Skeleton.SetSkin(original.Skeleton.Skin);
            _copy.Skeleton.SetupPose();
            _copy.AnimationState.ClearTracks();
            string animation = _copy.Skeleton.Data.FindAnimation("attack") != null ? "attack" : "idle";
            if (_copy.Skeleton.Data.FindAnimation(animation) != null) _copy.AnimationState.SetAnimation(0, animation, false);
            _copy.transform.rotation = Quaternion.identity;
            _copy.transform.localScale = Vector3.one;
            var parentScale = _copy.transform.lossyScale;
            var size = original.transform.lossyScale;
            _copy.transform.localScale = new Vector3(size.x / parentScale.x, size.y / parentScale.y, size.z / parentScale.z);
        }
        _running = true; _recovery = -1f;
        Draw(0);
    }
    /// <summary>전투 시간 진행률로 움직이며 피해를 만들지 않는다.</summary>
    public void Draw(float progress)
    {
        _progress = Mathf.Clamp01(progress);
        Render();
    }
    /// <summary>접촉 이펙트와 Spine 사본을 기존 복귀 시간에 맞춰 정리한다.</summary>
    public void Recover(float progress) { _recovery = Mathf.Clamp01(progress); Render(); }
    private void LateUpdate() { if (_running) Render(); }
    private void Render()
    {
        if (!_running || _target == null) { Clear(); return; }
        Vector3 contact = _target.transform.position;
        float fade = _recovery < 0 ? 1 : 1 - _recovery;
        if (!_consumer)
        {
            Vector3 tip = Vector3.Lerp(_origin, contact, _progress);
            Vector3 tail = Vector3.Lerp(_origin, contact, Mathf.Max(0, _progress - .18f));
            _beam.Draw(tail, tip, _data.HackColor, _data.CastBeamWidth, fade, .35f * fade, fade);
            return;
        }
        Vector3 point = contact + _data.TeleportOffset;
        if (_copy != null)
        {
            Vector3 home = _source != null ? _source.transform.position : _origin;
            if (_recovery >= .55f) point = Vector3.Lerp(point, home, Mathf.SmoothStep(0, 1, (_recovery - .55f) / .45f));
            _copy.transform.position = point;
            _copy.transform.rotation = Quaternion.identity;
            var muzzle = _copy.Skeleton.FindBone("muzzle");
            var center = _copy.Skeleton.FindBone("center");
            if (muzzle != null && center != null)
            {
                Vector3 facing = muzzle.GetWorldPosition(_copy.transform) - center.GetWorldPosition(_copy.transform);
                Vector3 aim = contact - point;
                _copy.transform.rotation = Quaternion.Euler(0, 0,
                    Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg - Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg);
            }
            _copyRenderer.enabled = _progress >= .2f && fade > .01f;
            _copy.Skeleton.SetColor(1, 1, 1, fade);
            if (muzzle != null) point = muzzle.GetWorldPosition(_copy.transform);
        }
        float fire = _progress >= .8f && (_recovery < 0 || _recovery < .55f) ? fade : 0;
        _beam.Draw(point, contact, _data.HackColor, _data.CastBeamWidth, fire, fade, fire);
    }
    /// <summary>취소/복귀 시 모든 외형을 숨긴다.</summary>
    public void Clear()
    {
        _running = false;
        _beam?.Clear();
        if (_copy != null) _copy.gameObject.SetActive(false);
    }
    private void OnDisable() => Clear();
}
