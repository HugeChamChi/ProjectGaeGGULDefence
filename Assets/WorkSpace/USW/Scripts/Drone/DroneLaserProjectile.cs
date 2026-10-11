using System;
using UnityEngine;

/// <summary>Laboratory laser presentation with the existing projectile movement and hit contract.</summary>
[DefaultExecutionOrder(300)]
public sealed class DroneLaserProjectile : Projectile
{
    [SerializeField] private Material _beamMaterial;
    [SerializeField] private Material _glowMaterial;
    [SerializeField] private Color _tint = Color.cyan;
    [SerializeField, Min(.01f)] private float _width = .18f;
    [SerializeField, Min(0f), Tooltip("Zero keeps the muzzle beam; positive values draw a travelling segment in world units.")]
    private float _segmentLength;
    private DroneBeamVisual _visual;
    private SpineActorVisual _emitter;
    private Vector3 _origin;
    private bool _launched;

    /// <summary>Muzzle-connected drone beams use the sustained cue; travelling bolts use the launch cue.</summary>
    public bool IsSustainedBeam => _segmentLength <= 0f;

    /// <summary>Tracks a live muzzle; recorded replay shots retain their recorded origin.</summary>
    public void BindEmitter(SpineActorVisual emitter) => _emitter = emitter;

    /// <inheritdoc />
    public override void Launch(Vector3 from, Vector3 to, Action<Projectile> onComplete,
        ProjectileData data = null, UnitBase sourceUnit = null, float sizeMultiplier = 1f)
    {
        _origin = from;
        if (_visual == null) _visual = gameObject.AddComponent<DroneBeamVisual>();
        _visual.Initialize(_beamMaterial, _glowMaterial);
        _launched = true;
        if (IsSustainedBeam) _audioManager?.SetLaserActive(this, true);
        base.Launch(from, to, onComplete, data, sourceUnit, sizeMultiplier);
    }

    private void LateUpdate()
    {
        if (!_launched) return;
        Vector3 from = _emitter != null && _emitter.isActiveAndEnabled ? _emitter.MuzzlePosition : _origin;
        if (_segmentLength > 0f)
        {
            Vector3 travelled = transform.position - _origin;
            from = transform.position - Vector3.ClampMagnitude(travelled, _segmentLength);
        }
        _visual.Draw(from, transform.position, _tint, _width, 1f, _segmentLength > 0f ? 0f : .9f, .7f);
    }

    /// <inheritdoc />
    protected override void OnDisable()
    {
        _audioManager?.SetLaserActive(this, false);
        _launched = false;
        _emitter = null;
        _visual?.Clear();
        base.OnDisable();
    }

    /// <inheritdoc />
    protected override void OnHit(Vector3 pos)
    {
        _audioManager?.SetLaserActive(this, false);
        base.OnHit(pos);
    }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        _audioManager?.SetLaserActive(this, false);
        base.OnDestroy();
    }
}
