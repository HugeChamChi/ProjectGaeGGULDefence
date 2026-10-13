using System;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Applies the lab's short RGB glitch to a live Spine mesh without moving the boss or replacing its animation.</summary>
public sealed class DisigmanSpineGlitchFx : IDisposable
{
    private const float TickSeconds = .04f;
    private const float GhostAlpha = .6f;
    private static readonly int AmountId = Shader.PropertyToID("_GlitchAmount");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private readonly SkeletonRenderer _skeleton;
    private readonly MeshRenderer _renderer;
    private readonly MeshFilter _mesh;
    private readonly MeshRenderer[] _ghosts = new MeshRenderer[2];
    private readonly MeshFilter[] _ghostMeshes = new MeshFilter[2];
    private readonly Material[][] _ghostMaterials = new Material[2][];
    private readonly Dictionary<Material, Material> _materials = new Dictionary<Material, Material>();
    private readonly Dictionary<Material, Material> _previous = new Dictionary<Material, Material>();
    private readonly DisigmanGlitchLinkSettings _settings;
    private float _time = -1f;
    private int _seed;

    /// <summary>Captures the current atlas and creates two reusable mesh ghosts.</summary>
    public DisigmanSpineGlitchFx(SkeletonRenderer skeleton, DisigmanGlitchLinkSettings settings)
    {
        _skeleton = skeleton;
        _settings = settings;
        _renderer = skeleton.GetComponent<MeshRenderer>();
        _mesh = skeleton.GetComponent<MeshFilter>();
        if (_renderer == null || _mesh == null || settings.BossGlitchMaterial == null) return;
        var originals = _renderer.sharedMaterials;
        foreach (var material in originals)
        {
            if (material == null || _materials.ContainsKey(material)) continue;
            var copy = new Material(settings.BossGlitchMaterial) { mainTexture = material.mainTexture };
            _materials.Add(material, copy);
        }
        for (int i = 0; i < _ghosts.Length; i++)
        {
            var go = new GameObject("DisigmanGlitchGhost" + i, typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = skeleton.gameObject.layer;
            go.transform.SetParent(skeleton.transform, false);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sortingLayerID = _renderer.sortingLayerID;
            renderer.sortingOrder = _renderer.sortingOrder - 1;
            renderer.enabled = false;
            var materials = new Material[originals.Length];
            for (int j = 0; j < materials.Length; j++)
                materials[j] = new Material(settings.BossGlitchMaterial) { mainTexture = originals[j] != null ? originals[j].mainTexture : null };
            renderer.sharedMaterials = materials;
            _ghosts[i] = renderer;
            _ghostMeshes[i] = go.GetComponent<MeshFilter>();
            _ghostMaterials[i] = materials;
        }
        _skeleton.OnMeshAndMaterialsUpdated += UpdateMesh;
    }

    /// <summary>Restarts the short distortion, preserving pre-existing Spine overrides.</summary>
    public void Trigger()
    {
        if (_skeleton == null) return;
        if (_time < 0f)
        {
            _previous.Clear();
            var overrides = _skeleton.CustomMaterialOverride;
            foreach (var pair in _materials)
            {
                if (overrides.TryGetValue(pair.Key, out var previous)) _previous.Add(pair.Key, previous);
                overrides[pair.Key] = pair.Value;
            }
        }
        _time = 0f;
        _seed++;
    }

    /// <summary>Advances with the same scaled clock as the shot and data shards.</summary>
    public void Tick(float dt)
    {
        if (_time < 0f) return;
        if (_skeleton == null || !_skeleton.isActiveAndEnabled) { Restore(); return; }
        _time += dt;
        if (_time >= _settings.BossGlitchSeconds) { Restore(); return; }
        float k = 1f - _time / Mathf.Max(.01f, _settings.BossGlitchSeconds);
        foreach (var material in _materials.Values) material.SetFloat(AmountId, _settings.BossGlitchAmount * k);
        int tick = Mathf.FloorToInt(_time / TickSeconds);
        float scale = Mathf.Max(.0001f, Mathf.Abs(_skeleton.transform.lossyScale.x));
        for (int i = 0; i < _ghosts.Length; i++)
        {
            if (_ghosts[i] == null) continue;
            float hash = WorldGlitchBlocks.Hash(_seed * 31 + (i == 0 ? 7 : 13), tick);
            float spread = _settings.BossGhostOffset * k / scale * Mathf.Lerp(.5f, 1.3f, hash);
            float jitter = (WorldGlitchBlocks.Hash(_seed * 31, tick) - .5f) * 2f * _settings.BossJitter * k / scale;
            _ghosts[i].transform.localPosition = new Vector3((i == 0 ? -spread : spread) + jitter, 0f, 0f);
            _ghosts[i].enabled = _renderer != null && _renderer.enabled;
            Color color = i == 0 ? _settings.Cyan : _settings.Hot;
            color.a = GhostAlpha * k;
            foreach (var material in _ghostMaterials[i]) material.SetColor(ColorId, color);
        }
        UpdateMesh(_skeleton);
    }

    private void UpdateMesh(ISkeletonRenderer _)
    {
        if (_mesh == null) return;
        foreach (var mesh in _ghostMeshes) if (mesh != null) mesh.sharedMesh = _mesh.sharedMesh;
    }

    /// <summary>Restores only overrides owned by this effect, leaving other render effects intact.</summary>
    public void Restore()
    {
        if (_time >= 0f && _skeleton != null)
        {
            var overrides = _skeleton.CustomMaterialOverride;
            foreach (var pair in _materials)
            {
                if (!overrides.TryGetValue(pair.Key, out var current) || current != pair.Value) continue;
                if (_previous.TryGetValue(pair.Key, out var previous)) overrides[pair.Key] = previous;
                else overrides.Remove(pair.Key);
            }
        }
        _time = -1f;
        foreach (var ghost in _ghosts) if (ghost != null) ghost.enabled = false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Restore();
        if (_skeleton != null) _skeleton.OnMeshAndMaterialsUpdated -= UpdateMesh;
        foreach (var material in _materials.Values) Object.Destroy(material);
        foreach (var materials in _ghostMaterials)
            if (materials != null) foreach (var material in materials) Object.Destroy(material);
        foreach (var ghost in _ghosts) if (ghost != null) Object.Destroy(ghost.gameObject);
    }
}
