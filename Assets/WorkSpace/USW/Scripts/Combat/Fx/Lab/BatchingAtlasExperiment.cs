using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>테스트 씬 전용. 원본 에셋을 건드리지 않고 유닛/UI 아틀라스와 렌더 기여도를 비교한다.</summary>
[DefaultExecutionOrder(20000)]
public sealed class BatchingAtlasExperiment : MonoBehaviour
{
    private sealed class Atlas
    {
        public Texture2D Texture;
        public readonly Dictionary<Texture, Rect> Rects = new();
    }
    private sealed class Body
    {
        public MeshRenderer Renderer;
        public MeshFilter Filter;
        public Material[] Original, Packed;
        public Rect Rect;
        public ShadowCastingMode Shadow;
        public readonly List<Vector2> CurrentUV = new();
    }
    private sealed class Picture
    {
        public Image Image;
        public Sprite Original, Packed;
    }
    private sealed class UnitSprite
    {
        public SpriteRenderer Renderer;
        public Sprite Original, Packed;
    }
    private sealed class UVData
    {
        public Vector2[] Original, Packed;
    }
    private readonly List<Body> _bodies = new();
    private readonly List<Picture> _pictures = new();
    private readonly List<UnitSprite> _sprites = new();
    private readonly Dictionary<Mesh, UVData> _uvs = new();
    private readonly List<Material> _materials = new();
    private readonly List<Sprite> _ownedSprites = new();
    private readonly List<Canvas> _canvases = new();
    private Atlas _unitAtlas, _uiAtlas;
    private StatBuffBatchingTest _test;
    public bool UnitAtlasEnabled { get; private set; }
    public bool UIAtlasEnabled { get; private set; }
    public bool Measuring { get; private set; }
    public string Results { get; private set; } = "Ready";
    public bool ControlsVisible = true;
    private bool _noShadows;
    private bool _initialSharedFxLayers;
    private bool _initialMergedLayers;
    public bool NoUnitShadows { get => _noShadows; set => _noShadows = value; }

    private static Texture2D ReadTexture(Texture source)
    {
        var previous = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;
        var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        copy.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        return copy;
    }

    private static Atlas Pack(HashSet<Texture> sources, string name)
    {
        var keys = new List<Texture>(sources);
        var copies = new Texture2D[keys.Count];
        for (int i = 0; i < keys.Count; i++) copies[i] = ReadTexture(keys[i]);
        var atlas = new Atlas { Texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp } };
        var rects = atlas.Texture.PackTextures(copies, 4, 4096, false);
        for (int i = 0; i < keys.Count; i++) { atlas.Rects.Add(keys[i], rects[i]); Destroy(copies[i]); }
        atlas.Texture.Apply(false, true);
        return atlas;
    }

    private Sprite RepackSprite(Sprite source, Atlas atlas)
    {
        var texture = source != null ? source.texture : Texture2D.whiteTexture;
        var packed = atlas.Rects[texture];
        var rect = source != null ? source.rect : new Rect(0, 0, texture.width, texture.height);
        float sx = packed.width * atlas.Texture.width / texture.width;
        float sy = packed.height * atlas.Texture.height / texture.height;
        var resultRect = new Rect(packed.x * atlas.Texture.width + rect.x * sx, packed.y * atlas.Texture.height + rect.y * sy, rect.width * sx, rect.height * sy);
        var pivot = source != null ? new Vector2(source.pivot.x / rect.width, source.pivot.y / rect.height) : Vector2.one * .5f;
        var border = source != null ? source.border : Vector4.zero;
        border = new Vector4(border.x * sx, border.y * sy, border.z * sx, border.w * sy);
        var sprite = Sprite.Create(atlas.Texture, resultRect, pivot, (source != null ? source.pixelsPerUnit : 100f) * sx, 0, SpriteMeshType.FullRect, border);
        sprite.name = "Experiment_" + (source != null ? source.name : "White");
        _ownedSprites.Add(sprite);
        return sprite;
    }

    public string Initialize()
    {
        if (_test != null) return Results;
        _test = FindFirstObjectByType<StatBuffBatchingTest>();
        _initialSharedFxLayers = _test.SharedFxLayers;
        _initialMergedLayers = _test.MeshBatch.MergeSharedLayers;
        var textures = new HashSet<Texture>();
        foreach (var unit in FindObjectsByType<UnitBase>(FindObjectsSortMode.None))
        {
            foreach (var renderer in unit.GetComponentsInChildren<MeshRenderer>(true))
            {
                var original = renderer.sharedMaterials;
                if (original.Length != 1 || original[0] == null || original[0].mainTexture == null) continue;
                textures.Add(original[0].mainTexture);
                _bodies.Add(new Body { Renderer = renderer, Filter = renderer.GetComponent<MeshFilter>(), Original = original, Shadow = renderer.shadowCastingMode });
            }
            foreach (var renderer in unit.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite == null) continue;
                textures.Add(renderer.sprite.texture);
                _sprites.Add(new UnitSprite { Renderer = renderer, Original = renderer.sprite });
            }
        }
        _unitAtlas = Pack(textures, "TEST_UnitSharedAtlas");
        var shared = new Dictionary<Shader, Material>();
        foreach (var body in _bodies)
        {
            var source = body.Original[0];
            if (!shared.TryGetValue(source.shader, out var material))
            {
                material = new Material(source) { name = "TEST_UnitSharedMaterial", mainTexture = _unitAtlas.Texture };
                shared.Add(source.shader, material); _materials.Add(material);
            }
            body.Packed = new[] { material };
            body.Rect = _unitAtlas.Rects[source.mainTexture];
        }
        foreach (var sprite in _sprites) sprite.Packed = RepackSprite(sprite.Original, _unitAtlas);
        textures.Clear();
        foreach (var image in FindObjectsByType<Image>(FindObjectsSortMode.None))
        {
            if (!image.isActiveAndEnabled || image.material.shader.name != "UI/Default") continue;
            var sprite = image.overrideSprite;
            if (sprite != null && sprite.packed) continue;
            textures.Add(sprite != null ? sprite.texture : Texture2D.whiteTexture);
            _pictures.Add(new Picture { Image = image, Original = sprite });
        }
        _uiAtlas = Pack(textures, "TEST_UISharedAtlas");
        foreach (var picture in _pictures) picture.Packed = RepackSprite(picture.Original, _uiAtlas);
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (canvas.isActiveAndEnabled) _canvases.Add(canvas);
        Results = $"Units: {_bodies.Count} Spine + {_sprites.Count} sprites; UI: {_pictures.Count} Images. Atlas: units {_unitAtlas.Texture.width}x{_unitAtlas.Texture.height}, UI {_uiAtlas.Texture.width}x{_uiAtlas.Texture.height}.";
        Debug.Log("[AtlasExperiment] " + Results);
        return Results;
    }

    public void SetAtlases(bool units, bool ui)
    {
        if (UnitAtlasEnabled && !units)
            foreach (var pair in _uvs) if (pair.Key != null) pair.Key.uv = pair.Value.Original;
        UnitAtlasEnabled = units; UIAtlasEnabled = ui;
        foreach (var picture in _pictures) if (picture.Image != null) picture.Image.overrideSprite = ui ? picture.Packed : picture.Original;
        foreach (var sprite in _sprites) if (sprite.Renderer != null) sprite.Renderer.sprite = units ? sprite.Packed : sprite.Original;
        RefreshBodies();
    }

    private void RefreshBodies()
    {
        foreach (var body in _bodies)
        {
            if (body.Renderer == null || body.Filter == null) continue;
            body.Renderer.sharedMaterials = UnitAtlasEnabled ? body.Packed : body.Original;
            body.Renderer.shadowCastingMode = _noShadows ? ShadowCastingMode.Off : body.Shadow;
            if (!UnitAtlasEnabled) continue;
            var mesh = body.Filter.sharedMesh;
            if (mesh == null || mesh.vertexCount == 0) continue;
            if (!_uvs.TryGetValue(mesh, out var data) || data.Original.Length != mesh.vertexCount)
            {
                data = new UVData { Original = new Vector2[mesh.vertexCount], Packed = new Vector2[mesh.vertexCount] };
                _uvs[mesh] = data;
            }
            mesh.GetUVs(0, body.CurrentUV);
            bool alreadyPacked = true;
            for (int i = 0; i < data.Packed.Length; i++)
                if (body.CurrentUV[i] != data.Packed[i]) { alreadyPacked = false; break; }
            if (!alreadyPacked)
                for (int i = 0; i < data.Packed.Length; i++)
                {
                    data.Original[i] = body.CurrentUV[i];
                    data.Packed[i] = body.Rect.position + Vector2.Scale(data.Original[i], body.Rect.size);
                }
            mesh.uv = data.Packed;
        }
    }

    private void LateUpdate() => RefreshBodies();

    private void Update()
    {
        if (!Measuring && Input.GetKeyDown(KeyCode.F8))
            SetControlsVisible(!ControlsVisible);
    }

    public void BeginMeasurement()
    {
        if (!Measuring && _test != null) StartCoroutine(Measure());
    }

    private IEnumerator Measure()
    {
        Measuring = true;
        _test.ControlsVisible = false;
        var previews = new List<MonoBehaviour>();
        foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (behaviour.enabled && behaviour.GetType().Name == "IngameEditorPreview") { previews.Add(behaviour); behaviour.enabled = false; }
        var report = new System.Text.StringBuilder("Mode,MeanBatches,Min,Max,DrawCalls,SetPass,FXMeshes\n");
        for (int mode = 0; mode < 13; mode++)
        {
            bool unitAtlas = mode == 1 || mode >= 3, uiAtlas = mode >= 2;
            SetAtlases(unitAtlas, uiAtlas);
            int mask = mode < 4 || mode == 12 ? 7 : mode - 4;
            _noShadows = mode == 12;
            _test.SetBatching(true);
            _test.SetVisibility((mask & 1) != 0, (mask & 2) != 0);
            foreach (var canvas in _canvases) if (canvas != null) canvas.enabled = (mask & 4) != 0;
            string name = mode < 4 ? new[] { "Baseline", "UnitAtlas", "UIAtlas", "BothAtlases" }[mode] : mode == 12 ? "BothAtlases_NoUnitShadows" : "Mask" + mask + "_FX1_Unit2_UI4";
            Results = "Measuring " + name;
            Debug.Log("[AtlasExperiment] " + Results);
            for (int i = 0; i < 20; i++) yield return new WaitForEndOfFrame();
            float batches = 0, draws = 0, passes = 0; int min = int.MaxValue, max = 0;
            for (int i = 0; i < 120; i++)
            {
                yield return new WaitForEndOfFrame();
#if UNITY_EDITOR
                int b = UnityEditor.UnityStats.batches;
                batches += b; draws += UnityEditor.UnityStats.drawCalls; passes += UnityEditor.UnityStats.setPassCalls;
                min = Mathf.Min(min, b); max = Mathf.Max(max, b);
#endif
            }
            report.AppendLine($"{name},{batches / 120:F2},{min},{max},{draws / 120:F2},{passes / 120:F2},{_test.MeshBatch.ActiveBatchCount}");
        }
        _noShadows = false;
        SetAtlases(true, true);
        foreach (var canvas in _canvases) if (canvas != null) canvas.enabled = true;
        _test.SetVisibility(true, true);
        foreach (var preview in previews) if (preview != null) preview.enabled = true;
        _test.ControlsVisible = true;
        ControlsVisible = true;
        Results = report.ToString();
        System.IO.Directory.CreateDirectory("outputs/stat-buff-atlas-experiment");
        System.IO.File.WriteAllText("outputs/stat-buff-atlas-experiment/measurements.csv", Results);
        Debug.Log("[AtlasExperiment] " + Results);
        Measuring = false;
    }

    public void BeginLayerComparison()
    {
        if (!Measuring && _test != null) StartCoroutine(CompareLayers());
    }

    public void SetControlsVisible(bool visible)
    {
        ControlsVisible = visible;
        if (_test != null) _test.ControlsVisible = visible;
        foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (behaviour.GetType().Name == "IngameEditorPreview") behaviour.enabled = visible;
    }

    public void SetFourBatchMode(bool enabled)
    {
        if (enabled) _test.SharedFxLayers = true;
        _test.MeshBatch.MergeSharedLayers = enabled;
    }

    public void BeginFourBatchComparison()
    {
        if (!Measuring && _test != null) StartCoroutine(CompareFourBatches());
    }

    private IEnumerator CompareFourBatches()
    {
        Measuring = true;
        SetControlsVisible(false);
        SetAtlases(true, true);
        _noShadows = true; _test.SharedFxLayers = true; _test.SetBatching(true);
        var report = new System.Text.StringBuilder("Mode,MeanBatches,Min,Max,DrawCalls,SetPass,FxMin,FxMax\n");
        for (int mode = 0; mode < 11; mode++)
        {
            bool compact = mode != 0 && mode != 10;
            SetFourBatchMode(compact);
            int mask = mode < 2 || mode == 10 ? 7 : mode - 2;
            _test.SetVisibility((mask & 1) != 0, (mask & 2) != 0);
            foreach (var canvas in _canvases) if (canvas != null) canvas.enabled = (mask & 4) != 0;
            Results = mode == 0 ? "SharedLayersBaseline" : mode == 1 ? "FourBatchTarget" : mode == 10 ? "SharedLayersRepeat" : "Mask" + mask;
            Debug.Log("[FourBatchFX] " + Results);
            for (int i = 0; i < 20; i++) yield return new WaitForEndOfFrame();
            float batches = 0, draws = 0, passes = 0;
            int min = int.MaxValue, max = 0, fxMin = int.MaxValue, fxMax = 0;
            for (int i = 0; i < 120; i++)
            {
                yield return new WaitForEndOfFrame();
#if UNITY_EDITOR
                int b = UnityEditor.UnityStats.batches;
                batches += b; draws += UnityEditor.UnityStats.drawCalls; passes += UnityEditor.UnityStats.setPassCalls;
                min = Mathf.Min(min, b); max = Mathf.Max(max, b);
#endif
                fxMin = Mathf.Min(fxMin, _test.MeshBatch.ActiveBatchCount);
                fxMax = Mathf.Max(fxMax, _test.MeshBatch.ActiveBatchCount);
            }
            report.AppendLine($"{Results},{batches / 120:F2},{min},{max},{draws / 120:F2},{passes / 120:F2},{fxMin},{fxMax}");
        }
        foreach (var canvas in _canvases) if (canvas != null) canvas.enabled = true;
        _test.SetVisibility(true, true); SetFourBatchMode(true);
        Results = report.ToString();
        System.IO.Directory.CreateDirectory("outputs/four-batch-fx");
        System.IO.File.WriteAllText("outputs/four-batch-fx/measurements.csv", Results);
        Debug.Log("[FourBatchFX] " + Results);
        Measuring = false; SetControlsVisible(false);
    }

    private IEnumerator CompareLayers()
    {
        SetFourBatchMode(false);
        Measuring = true;
        _test.ControlsVisible = false;
        var previews = new List<MonoBehaviour>();
        foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (behaviour.enabled && behaviour.GetType().Name == "IngameEditorPreview") { previews.Add(behaviour); behaviour.enabled = false; }
        SetAtlases(true, true); _test.SetBatching(true); _test.SetVisibility(true, true);
        var report = new System.Text.StringBuilder("Mode,MeanBatches,Min,Max,DrawCalls,SetPass,FXMeshes\n");
        for (int mode = 0; mode < 4; mode++)
        {
            _test.SharedFxLayers = mode == 1 || mode == 2;
            _noShadows = mode == 2;
            Results = new[] { "ExactUnitSorting", "SharedFxLayers", "SharedFxLayers_NoUnitShadows", "ExactUnitSorting_Repeat" }[mode];
            Debug.Log("[AtlasExperiment] " + Results);
            for (int i = 0; i < 20; i++) yield return new WaitForEndOfFrame();
            float batches = 0, draws = 0, passes = 0; int min = int.MaxValue, max = 0;
            for (int i = 0; i < 120; i++)
            {
                yield return new WaitForEndOfFrame();
#if UNITY_EDITOR
                int b = UnityEditor.UnityStats.batches;
                batches += b; draws += UnityEditor.UnityStats.drawCalls; passes += UnityEditor.UnityStats.setPassCalls;
                min = Mathf.Min(min, b); max = Mathf.Max(max, b);
#endif
            }
            report.AppendLine($"{Results},{batches / 120:F2},{min},{max},{draws / 120:F2},{passes / 120:F2},{_test.MeshBatch.ActiveBatchCount}");
        }
        _test.SharedFxLayers = true; _noShadows = true;
        foreach (var preview in previews) if (preview != null) preview.enabled = true;
        _test.ControlsVisible = true; ControlsVisible = true;
        Results = report.ToString();
        System.IO.Directory.CreateDirectory("outputs/stat-buff-atlas-experiment");
        System.IO.File.WriteAllText("outputs/stat-buff-atlas-experiment/layer-comparison.csv", Results);
        Debug.Log("[AtlasExperiment] " + Results);
        Measuring = false;
    }

    private void OnGUI()
    {
        if (!ControlsVisible || Measuring || _test == null) return;
        float scale = Mathf.Max(1f, Screen.width / 1100f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        GUI.Box(new Rect(8, 352, 590, 312), "TEST ONLY — unit/UI atlas (F8: show/hide controls)");
        if (GUI.Button(new Rect(20, 382, 270, 32), "UNIT ATLAS: " + UnitAtlasEnabled)) SetAtlases(!UnitAtlasEnabled, UIAtlasEnabled);
        if (GUI.Button(new Rect(300, 382, 276, 32), "UI ATLAS: " + UIAtlasEnabled)) SetAtlases(UnitAtlasEnabled, !UIAtlasEnabled);
        if (GUI.Button(new Rect(20, 422, 556, 32), "MEASURE ATLAS + FX / UNIT / UI CONTRIBUTIONS")) BeginMeasurement();
        if (GUI.Button(new Rect(20, 462, 270, 32), "SHARED FX LAYERS: " + _test.SharedFxLayers))
        {
            _test.SharedFxLayers = !_test.SharedFxLayers;
            if (!_test.SharedFxLayers) SetFourBatchMode(false);
        }
        if (GUI.Button(new Rect(300, 462, 276, 32), "UNIT SHADOWS OFF: " + _noShadows)) _noShadows = !_noShadows;
        if (GUI.Button(new Rect(20, 502, 556, 32), "MEASURE EXACT / SHARED FX LAYERS")) BeginLayerComparison();
        if (GUI.Button(new Rect(20, 542, 556, 32), "FX <=4 BATCHES: " + _test.MeshBatch.MergeSharedLayers)) SetFourBatchMode(!_test.MeshBatch.MergeSharedLayers);
        if (GUI.Button(new Rect(20, 582, 556, 32), "MEASURE <=4 FX + UNIT / UI CONTRIBUTIONS")) BeginFourBatchComparison();
        GUI.Label(new Rect(20, 622, 560, 36), "Results: outputs/four-batch-fx/measurements.csv");
        GUI.matrix = Matrix4x4.identity;
    }

    private void OnDestroy()
    {
        foreach (var canvas in _canvases) if (canvas != null) canvas.enabled = true;
        if (_test != null)
        {
            _test.ControlsVisible = true; _test.SharedFxLayers = _initialSharedFxLayers;
            if (_test.MeshBatch != null) _test.MeshBatch.MergeSharedLayers = _initialMergedLayers;
        }
        foreach (var picture in _pictures) if (picture.Image != null) picture.Image.overrideSprite = picture.Original;
        foreach (var sprite in _sprites) if (sprite.Renderer != null) sprite.Renderer.sprite = sprite.Original;
        foreach (var body in _bodies) if (body.Renderer != null) { body.Renderer.sharedMaterials = body.Original; body.Renderer.shadowCastingMode = body.Shadow; }
        foreach (var pair in _uvs) if (pair.Key != null) pair.Key.uv = pair.Value.Original;
        foreach (var material in _materials) Destroy(material);
        foreach (var sprite in _ownedSprites) Destroy(sprite);
        if (_unitAtlas != null) Destroy(_unitAtlas.Texture);
        if (_uiAtlas != null) Destroy(_uiAtlas.Texture);
    }
}
