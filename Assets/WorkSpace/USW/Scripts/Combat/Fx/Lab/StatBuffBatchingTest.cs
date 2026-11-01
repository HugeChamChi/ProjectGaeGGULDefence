using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;
using Cysharp.Threading.Tasks;

/// <summary>복사한 게임 씬에서 20유닛의 공용 아틀라스 버프 배칭을 비교한다.</summary>
[DefaultExecutionOrder(-7900)]
public sealed class StatBuffBatchingTest : MonoBehaviour
{
    public UnitData Unit;
    public UnitData[] UnitRoster;
    public UnitBuffVisualSettings Settings;
    public int SpawnedCount { get; private set; }
    public bool BuffsVisible = true;
    public bool UnitsVisible = true;
    public bool GlowVisible = true;
    public bool BatchingEnabled = true;
    public bool StressAllStyles = true;
    public bool ControlsVisible = true;
    public bool SharedFxLayers;
    [Min(.5f)] public float ReplayInterval = 2f;
    public int EffectCount => _effects.Count;
    public StatBuffMeshBatch MeshBatch => _pool?.MeshBatch;
    public string Measurement { get; private set; } = "Press MEASURE to compare identical scene conditions.";
    public bool Measuring { get; private set; }
    private readonly List<StatBuffFx> _effects = new();
    private sealed class Target
    {
        public UnitBase Unit;
        public Renderer[] Renderers;
        public SortingGroup Group;
    }
    private readonly List<Target> _effectTargets = new();
    private float _nextReplayAt;
    private readonly List<Renderer> _unitRenderers = new();
    private StatBuffFxPool _pool;
    private bool _ownsPreviewBalance;
    private string _status = "Preparing scene...";

    private void Awake()
    {
#if UNITY_EDITOR
        var resolver = InGameLifetimeScope.GlobalResolver;
        gameObject.AddComponent<IngameEditorPreview>().Configure(resolver.Resolve<WaveManager>());
        resolver.Resolve<ResearchRunBonuses>().PrepareEditorPreview();
        resolver.Resolve<GameManager>().EnableEditorPreview();
#endif
    }

    private IEnumerator Start()
    {
        var resolver = InGameLifetimeScope.GlobalResolver;
        var initializer = resolver.Resolve<GameInitializer>();
        while (!initializer.IsReady) yield return null;
        var grid = resolver.Resolve<GridManager>();
        var factory = resolver.Resolve<UnitFactory>();
        var cells = grid.GetEmptyCells();
        var roster = UnitRoster != null && UnitRoster.Length > 0 ? UnitRoster : new[] { Unit };
        foreach (var data in roster) yield return data.LoadAssetsAsync().ToCoroutine();
        // 로그인 없이도 실제 팩토리 경로로 생성하기 위한 씬 전용 SO 능력치 스냅샷.
        if (!UnitBalanceRuntime.IsLoaded)
        {
            var definitions = new List<UnitBalanceSnapshot.Definition>();
            var names = new[] { "normal", "rare", "epic", "legend" };
            foreach (var data in roster)
            {
                var stats = new UnitBalanceSnapshot.Stats[4];
                for (int i = 0; i < stats.Length; i++)
                {
                    var tier = TierUtil.All[i];
                    stats[i] = new UnitBalanceSnapshot.Stats { Tier = names[i], Attack = data.atk.Get(tier),
                        AttackFrequency = data.attackSpeed.Get(tier), SkillCooldown = data.skillCooldown.Get(tier),
                        FoodProduction = data.foodProduction.Get(tier), MaxDroneCount = data.maxDroneCount.Get(tier) };
                }
                definitions.Add(new UnitBalanceSnapshot.Definition {
                    UnitKey = data.name, AssetKey = data.name, Name = data.unitName, Stats = stats });
            }
            UnitBalanceRuntime.Install(new UnitBalanceSnapshot(new UnitBalanceSnapshot.Config {
                SchemaVersion = 1, Units = definitions.ToArray() }));
            _ownsPreviewBalance = true;
        }
        _pool = new StatBuffFxPool(Settings);
        var styles = StressAllStyles ? new[] { Settings.Attack, Settings.Speed, Settings.Mixed, Settings.Disabled } : new[] { Settings.Mixed };
        for (int i = 0; i < Mathf.Min(20, cells.Count); i++)
        {
            var unit = factory.CreateUnitFromData(roster[i % roster.Length]);
            var cell = cells[i];
            resolver.Resolve<UnitSpawner>().PlaceUnitWithEffect(unit, cell);
            var target = new Target { Unit = unit, Renderers = unit.GetComponentsInChildren<Renderer>(true),
                Group = unit.GetComponentInChildren<SortingGroup>(true) };
            foreach (var style in styles)
            {
                var fx = _pool.Rent(style);
                fx.Play(unit.transform, style, 0f, Settings.Scale);
                _effects.Add(fx); _effectTargets.Add(target);
            }
            SpawnedCount++;
        }
        _nextReplayAt = Time.unscaledTime + ReplayInterval;
        _status = $"Units: {SpawnedCount}/20 ({roster.Length} types) | FX: {_effects.Count} | Replay {ReplayInterval:F1}s";
        Debug.Log("[StatBuffBatchingTest] " + _status);
        yield return new WaitForSeconds(2f);
        foreach (var unit in FindObjectsByType<UnitBase>(FindObjectsSortMode.None))
            _unitRenderers.AddRange(unit.GetComponentsInChildren<Renderer>(true));
        SetVisibility(BuffsVisible, UnitsVisible, GlowVisible);
        var experiment = gameObject.AddComponent<BatchingAtlasExperiment>();
        experiment.Initialize();
    }

    private void LateUpdate()
    {
        bool replay = StressAllStyles && Time.unscaledTime >= _nextReplayAt;
        if (replay) _nextReplayAt = Time.unscaledTime + ReplayInterval;
        for (int i = 0; i < _effects.Count; i++)
        {
            var fx = _effects[i]; var target = _effectTargets[i];
            int layer = 0, order = Mathf.RoundToInt(-target.Unit.transform.position.y * 100f);
            if (target.Group != null) { layer = target.Group.sortingLayerID; order = target.Group.sortingOrder; }
            else foreach (var renderer in target.Renderers)
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                { layer = renderer.sortingLayerID; order = renderer.sortingOrder; break; }
            fx.SetSorting(layer, SharedFxLayers ? 30000 : order + Settings.FrontOffset, SharedFxLayers ? -30000 : order + Settings.BehindOffset);
            if (replay) fx.Play(target.Unit.transform, fx.Style, 0f, Settings.Scale);
        }
    }

    public void SetVisibility(bool buffs, bool units, bool glow = true)
    {
        BuffsVisible = buffs; UnitsVisible = units; GlowVisible = glow;
        if (MeshBatch != null) MeshBatch.SetVisibility(buffs, glow);
        foreach (var renderer in _unitRenderers)
            if (renderer != null) renderer.forceRenderingOff = !units;
    }

    public void SetBatching(bool enabled)
    {
        BatchingEnabled = enabled;
        if (MeshBatch != null) MeshBatch.UseBatching = enabled;
    }

    public void BeginMeasurement()
    {
        if (!Measuring && _unitRenderers.Count > 0) StartCoroutine(Measure());
    }

    private IEnumerator Measure()
    {
        Measuring = true;
        var report = new System.Text.StringBuilder("Mode: Batches avg [min-max] / DrawCalls / SetPass\n");
        var modes = new[] { "FX OFF", "Original sprites", "Merged meshes", "Original repeat", "Merged repeat" };
        for (int mode = 0; mode < modes.Length; mode++)
        {
            SetBatching(mode == 2 || mode == 4);
            SetVisibility(mode != 0, true);
            Measurement = report + "Sampling " + modes[mode] + "...";
            for (int i = 0; i < 30; i++) yield return new WaitForEndOfFrame();
            float batches = 0, draws = 0, passes = 0;
            int min = int.MaxValue, max = 0;
            for (int i = 0; i < 120; i++)
            {
                yield return new WaitForEndOfFrame();
#if UNITY_EDITOR
                int count = UnityEditor.UnityStats.batches;
                batches += count; draws += UnityEditor.UnityStats.drawCalls; passes += UnityEditor.UnityStats.setPassCalls;
                min = Mathf.Min(min, count); max = Mathf.Max(max, count);
#endif
            }
            report.AppendLine($"{modes[mode]}: {batches / 120f:F1} [{min}-{max}] / {draws / 120f:F1} / {passes / 120f:F1}");
        }
        Measurement = report.ToString();
        Debug.Log("[StatBuffBatchingTest] " + Measurement);
        SetBatching(true); SetVisibility(true, true);
        Measuring = false;
    }

    private void OnGUI()
    {
        if (!ControlsVisible) return;
        float scale = Mathf.Max(1f, Screen.width / 1100f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        GUI.Box(new Rect(8, 42, 590, 305), _status);
        GUI.enabled = !Measuring;
        if (GUI.Button(new Rect(20, 72, 180, 36), "BUFF FX: " + (BuffsVisible ? "ON" : "OFF"))) SetVisibility(!BuffsVisible, UnitsVisible, GlowVisible);
        if (GUI.Button(new Rect(208, 72, 180, 36), "UNIT BODY: " + (UnitsVisible ? "ON" : "OFF"))) SetVisibility(BuffsVisible, !UnitsVisible, GlowVisible);
        if (GUI.Button(new Rect(396, 72, 180, 36), "GLOW: " + (GlowVisible ? "ON" : "OFF"))) SetVisibility(BuffsVisible, UnitsVisible, !GlowVisible);
        if (GUI.Button(new Rect(20, 116, 556, 32), "MERGED MESH: " + (BatchingEnabled ? "ON" : "OFF (original SpriteRenderers)"))) SetBatching(!BatchingEnabled);
        if (GUI.Button(new Rect(20, 154, 556, 32), "MEASURE ORIGINAL / MERGED (120 frames each)")) BeginMeasurement();
        GUI.enabled = true;
        GUI.Label(new Rect(20, 192, 560, 145), Measurement);
        GUI.matrix = Matrix4x4.identity;
    }

    private void OnDestroy()
    {
        _pool?.Dispose();
        if (_ownsPreviewBalance) UnitBalanceRuntime.Clear();
    }
}
