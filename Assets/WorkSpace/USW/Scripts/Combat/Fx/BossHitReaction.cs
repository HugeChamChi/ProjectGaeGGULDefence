using System;
using Spine;
using Spine.Unity;
using UnityEngine;

/// <summary>Adds the accepted Crocus hit pose and light tint without replacing its animation or combat state.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BossBase))]
public sealed class BossHitReaction : MonoBehaviour
{
    [SerializeField] private SkeletonAnimation _skeleton;
    [SerializeField] private BossHitReactionProfile _profile;
    private BossBase _boss;
    private Bone[] _bones;
    private BonePose[] _originalPoses;
    private bool _poseApplied;
    private bool _tintApplied;
    private Color _originalColor;
    private float _elapsed = float.PositiveInfinity;
    private float _tintElapsed = float.PositiveInfinity;
    private float _direction = 1f;

    /// <summary>Current heavy pose intensity; normal hits only affect color.</summary>
    public float Intensity { get; private set; }

    private void Awake() => _boss = GetComponent<BossBase>();

    private void OnEnable()
    {
        if (_skeleton == null || _profile == null) return;
        _boss.OnDamageDealt += OnDamageDealt;
        _boss.OnPatternStarted += OnPatternStarted;
        _boss.OnDeath += Clear;
        _skeleton.BeforeApply += BeforeApply;
        _skeleton.UpdateLocal += ApplyVisuals;
    }

    private void OnDisable()
    {
        if (_boss != null)
        {
            _boss.OnDamageDealt -= OnDamageDealt;
            _boss.OnPatternStarted -= OnPatternStarted;
            _boss.OnDeath -= Clear;
        }
        if (_skeleton != null)
        {
            _skeleton.BeforeApply -= BeforeApply;
            _skeleton.UpdateLocal -= ApplyVisuals;
        }
        Clear();
    }

    /// <summary>Starts the authored heavy pose after a damage effect confirms actual HP loss.</summary>
    public void HeavyImpact(Vector3 hitPosition)
    {
        if (!isActiveAndEnabled || _profile == null || _boss.IsDead || IsPatternPlaying() || !CacheBones()) return;
        _direction = hitPosition.x >= _boss.transform.position.x ? 1f : -1f;
        _elapsed = 0f;
    }

    private bool CacheBones()
    {
        if (_bones != null) return true;
        if (_skeleton == null || _skeleton.Skeleton == null) return false;
        var names = new[] { "body1", "head", "armL", "armR" };
        var bones = new Bone[names.Length];
        var poses = new BonePose[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            bones[i] = _skeleton.Skeleton.FindBone(names[i]);
            if (bones[i] == null) return false;
            poses[i] = new BonePose();
        }
        _bones = bones;
        _originalPoses = poses;
        return true;
    }

    private void OnDamageDealt(decimal damage, Vector3? position, BossDamageKind kind)
    {
        if (damage <= 0m || _boss.IsDead || kind == BossDamageKind.Burn) return;
        float pulse = _profile.HitTintHoldSeconds + _profile.HitTintFadeSeconds;
        if (_tintElapsed < Mathf.Max(pulse, _profile.HitTintIntervalSeconds)) return;
        _tintElapsed = 0f;
    }

    private void Update()
    {
        if (_profile == null) return;
        _elapsed += Time.deltaTime;
        _tintElapsed += Time.deltaTime;
        float attack = Mathf.Max(.001f, _profile.AttackSeconds);
        float hold = attack + _profile.HoldSeconds;
        float end = hold + Mathf.Max(.001f, _profile.RecoverSeconds);
        Intensity = _elapsed < attack ? Mathf.Sin(_elapsed / attack * Mathf.PI * .5f)
            : _elapsed < hold ? 1f : _elapsed < end ? 1f - Mathf.SmoothStep(0f, 1f, (_elapsed - hold) / (end - hold)) : 0f;
    }

    private bool IsPatternPlaying()
    {
        string clip = _skeleton?.AnimationState?.GetTrack(0)?.Animation?.Name;
        return clip != null && clip.StartsWith("skill_", StringComparison.Ordinal);
    }

    private void OnPatternStarted(BossPatternData pattern)
    {
        RestorePose();
        _elapsed = float.PositiveInfinity;
        Intensity = 0f;
    }

    private void BeforeApply(ISkeletonAnimation animation)
    {
        RestorePose();
        RestoreTint();
    }

    private void ApplyVisuals(ISkeletonRenderer renderer)
    {
        RestorePose();
        RestoreTint();
        if (_boss.IsDead) return;
        if (Intensity > 0f && !IsPatternPlaying() && CacheBones())
        {
            for (int i = 0; i < _bones.Length; i++) _originalPoses[i].Set(_bones[i].Pose);
            _poseApplied = true;
            var body = _bones[0].Pose;
            body.ScaleX *= 1f - _profile.BodyCompression * Intensity;
            body.ScaleY *= 1f + _profile.BodyExpansion * Intensity;
            body.Rotation += _profile.BodyTilt * Intensity * _direction;
            _bones[1].Pose.Rotation -= _profile.HeadTilt * Intensity * _direction;
            _bones[2].Pose.Rotation += _profile.ArmBrace * Intensity;
            _bones[3].Pose.Rotation -= _profile.ArmBrace * Intensity;
        }
        float fade = Mathf.Max(.001f, _profile.HitTintFadeSeconds);
        float progress = Mathf.Clamp01((_tintElapsed - _profile.HitTintHoldSeconds) / fade);
        if (progress >= 1f) return;
        var skeleton = _skeleton.Skeleton;
        _originalColor = skeleton.GetColor();
        float strength = Mathf.Clamp01(_profile.HitTintStrength) * (1f - Mathf.SmoothStep(0f, 1f, progress));
        Color multiplier = Color.Lerp(Color.white, _profile.HitTint, strength);
        skeleton.SetColor(_originalColor.r * multiplier.r, _originalColor.g * multiplier.g,
            _originalColor.b * multiplier.b, _originalColor.a);
        _tintApplied = true;
    }

    private void RestorePose()
    {
        if (!_poseApplied) return;
        for (int i = 0; i < _bones.Length; i++) _bones[i].Pose.Set(_originalPoses[i]);
        _poseApplied = false;
    }

    private void RestoreTint()
    {
        if (!_tintApplied || _skeleton == null || _skeleton.Skeleton == null) return;
        _skeleton.Skeleton.SetColor(_originalColor);
        _tintApplied = false;
    }

    private void Clear()
    {
        RestorePose();
        RestoreTint();
        _elapsed = _tintElapsed = float.PositiveInfinity;
        Intensity = 0f;
    }
}
