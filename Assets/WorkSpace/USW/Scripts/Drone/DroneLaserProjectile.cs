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
    private DroneBeamVisual _visual;
    private SpineActorVisual _emitter;
    private Vector3 _origin;
    private bool _launched;

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
        base.Launch(from, to, onComplete, data, sourceUnit, sizeMultiplier);
    }

    private void LateUpdate()
    {
        if (!_launched) return;
        Vector3 from = _emitter != null && _emitter.isActiveAndEnabled ? _emitter.MuzzlePosition : _origin;
        _visual.Draw(from, transform.position, _tint, _width, 1f, .9f, .7f);
    }

    /// <inheritdoc />
    protected override void OnDisable()
    {
        _launched = false;
        _emitter = null;
        _visual?.Clear();
        base.OnDisable();
    }
}
