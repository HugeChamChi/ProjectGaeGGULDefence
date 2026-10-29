using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>
/// FxLab driver for the unit tier badge / skill charge drafts. Boots the copied game scene, fills the
/// 6x4 grid with mixed-tier units through the real factory/spawner, and connects each unit with the one
/// entry call the game will use: <c>UnitStatusBadgeOverlay.Register(unit)</c>.
/// </summary>
public sealed class UnitStatusBadgeFxLab : MonoBehaviour
{
    /// <summary>Charge state shown in the lab.</summary>
    public enum LabMode
    {
        /// <summary>Combat held; every gauge pinned to 0 / 50 / 100 % by cell.</summary>
        Pinned = 0,
        /// <summary>Normal combat; gauges charge and skills fire.</summary>
        Live = 1,
        /// <summary>Combat held; all gauges jump from near full to full every cycle to replay the ready flash.</summary>
        ReadyMoment = 2
    }

    /// <summary>Final-tier look laid over the selected draft (lab comparison only).</summary>
    public enum LegendLook
    {
        /// <summary>As the draft authors it (number 4).</summary>
        Draft = 0,
        /// <summary>Star in the same color as the Normal number.</summary>
        WhiteStar = 1,
        /// <summary>Outlined star in the same color as the Normal number.</summary>
        OutlineStar = 2,
        /// <summary>Random Dice style: lower tiers glint now and then; gold star with a light sweep.</summary>
        GoldSweep = 3,
        /// <summary>Random Dice style: gold star with sparkle dust.</summary>
        GoldSparkle = 4,
        /// <summary>Random Dice style: gold star with a rainbow sheen.</summary>
        GoldShimmer = 5,
        /// <summary>Random Dice style: gold star with light running around the border.</summary>
        GoldBorderRun = 6
    }

    [SerializeField] private UnitStatusBadge _prefab;
    [SerializeField] private UnitStatusBadgeSettings[] _drafts;
    [SerializeField] private string[] _draftNames;
    [Header("Field roster (cells in column-major order)")]
    [SerializeField] private UnitData[] _roster;
    [SerializeField] private int[] _cellUnit;
    [SerializeField] private Tier[] _cellTier;
    [SerializeField] private float[] _cellCharge;
    [Header("Controls")]
    [SerializeField] private Button _currentButton;
    [SerializeField] private Button[] _draftButtons;
    [SerializeField] private Button[] _modeButtons;
    [SerializeField] private Button[] _legendButtons;
    [SerializeField] private TMP_Text _caption;
    [SerializeField] private GameObject _controls;

    [Header("Lab values")]
    [SerializeField] private float _bootTimeout = 30f;
    [SerializeField] private float _spawnTimeout = 10f;
    [SerializeField] private float _liveTimerSeconds = 90f;
    [SerializeField, Range(0f, 1f)] private float _healBelow = 0.8f;
    [SerializeField, Range(0f, 1f)] private float _readyFrom = 0.85f;
    [SerializeField] private float _readyCycleSeconds = 2f;
    [SerializeField] private Color _legendGold = new Color(1f, 0.80f, 0.25f);

    private static readonly string[] ModeNames = { "고정 0/50/100%", "진행", "준비 순간" };
    private static readonly string[] LegendNames = { "레전드 숫자 4", "레전드 흰 별", "레전드 속 빈 별", "금별+빛 쓸기", "금별+반짝이 가루", "금별+무지개 윤광", "금별+테두리 빛" };
    private static readonly Color Selected = Color.white;
    private static readonly Color Idle = new Color(0.55f, 0.58f, 0.68f, 1f);

    private readonly List<UnitBase> _units = new List<UnitBase>();
    private UnitStatusBadgeOverlay _overlay;
    private UnitStatusOverlay _currentOverlay;
    private FieldPauseVisuals _fieldPause;
    private TimerController _timer;
    private BossManager _bosses;
    private bool _held;
    private float _readyClock;
    private UnitStatusBadgeSettings _legendCopy;

    /// <summary>Whether the field is filled and the badges are live.</summary>
    public bool Ready { get; private set; }
    /// <summary>-1 = current game display, otherwise draft index.</summary>
    public int Draft { get; private set; }
    /// <summary>Current charge mode.</summary>
    public LabMode Mode { get; private set; }
    /// <summary>Final-tier look laid over the draft.</summary>
    public LegendLook Legend { get; private set; }
    /// <summary>Badge overlay under test.</summary>
    public UnitStatusBadgeOverlay Overlay => _overlay;
    /// <summary>The game's current status overlay, for comparison.</summary>
    public UnitStatusOverlay CurrentOverlay => _currentOverlay;
    /// <summary>Units placed by the lab.</summary>
    public IReadOnlyList<UnitBase> Units => _units;
    /// <summary>Draft settings.</summary>
    public IReadOnlyList<UnitStatusBadgeSettings> Drafts => _drafts;

    private IObjectResolver Container => FindFirstObjectByType<InGameLifetimeScope>()?.Container;

    private void Awake()
    {
#if UNITY_EDITOR
        var container = Container;
        var game = container?.Resolve<GameManager>();
        if (game != null && !game.IsEditorPreview)
        {
            game.gameObject.AddComponent<IngameEditorPreview>().Configure(container.Resolve<WaveManager>());
            game.EnableEditorPreview();
            container.Resolve<ResearchRunBonuses>()?.PrepareEditorPreview();
        }
#endif
        _controls.SetActive(false);
        _currentButton.onClick.AddListener(() => SelectDraft(-1));
        for (int i = 0; i < _draftButtons.Length; i++)
        {
            int index = i;
            _draftButtons[i].onClick.AddListener(() => SelectDraft(index));
        }
        for (int i = 0; i < _modeButtons.Length; i++)
        {
            var mode = (LabMode)i;
            _modeButtons[i].onClick.AddListener(() => SelectMode(mode));
        }
        if (_legendButtons != null)
            for (int i = 0; i < _legendButtons.Length; i++)
            {
                var look = (LegendLook)i;
                _legendButtons[i].onClick.AddListener(() => SelectLegend(look));
            }
    }

    private void Start() => BootAsync(this.GetCancellationTokenOnDestroy()).Forget(e => Debug.LogException(e, this));

    private async UniTask BootAsync(CancellationToken token)
    {
        // Same editor-preview start as the other ingame FxLabs.
        await UniTask.Delay(TimeSpan.FromSeconds(1), DelayType.Realtime, cancellationToken: token);
        var container = Container;
        var game = container.Resolve<GameManager>();
        _timer = container.Resolve<TimerController>();
        _bosses = container.Resolve<BossManager>();
        _fieldPause = container.Resolve<FieldPauseVisuals>();
        var exp = container.Resolve<ExpManager>();
        var grid = container.Resolve<GridManager>();
        var factory = container.Resolve<UnitFactory>();
        var spawner = container.Resolve<UnitSpawner>();
        game.OnStartButtonPressed();
        await UniTask.WaitUntil(() => _bosses.CurrentBoss != null && game.CurrentState == GameManager.GameState.Playing,
            cancellationToken: token).Timeout(TimeSpan.FromSeconds(_bootTimeout));
        // The lab keeps one fight going: no level-up choices, boss healed, timer topped up.
        exp.DeferLevelUps = true;

        int index = 0;
        for (int x = 0; x < grid.Columns; x++)
        for (int z = 0; z < grid.Rows; z++, index++)
        {
            var cell = grid.GetCell(x, z);
            if (cell == null) continue;
            if (cell.OccupyingUnit != null) { _units.Add(cell.OccupyingUnit); continue; }
            if (index >= _cellUnit.Length) continue;
            var unit = factory.CreateUnitFromData(_roster[_cellUnit[index]], _cellTier[index]);
            if (unit == null) continue;
            spawner.PlaceUnitWithEffect(unit, cell);
            _units.Add(unit);
        }
        await UniTask.WaitUntil(() => _units.TrueForAll(u => u == null || (u.isActiveAndEnabled && u.currentCell != null)),
            cancellationToken: token).Timeout(TimeSpan.FromSeconds(_spawnTimeout));

        // The game overlay is created by GridManager on the first unit; keep it for the "current" comparison.
        _currentOverlay = grid.GetComponentInChildren<UnitStatusOverlay>(true);
        _overlay = UnitStatusBadgeOverlay.Create(grid.transform, _prefab);
        // Lab stand-in for GridManager.RegisterUnitStatus → overlay.Register(unit).
        foreach (var unit in _units) _overlay.Register(unit);

        _controls.SetActive(true);
        SelectDraft(0);
        SelectMode(LabMode.Pinned);
        Ready = true;
    }

    private void OnDestroy()
    {
        if (_held) _fieldPause?.Exit(this);
        if (_legendCopy != null) Destroy(_legendCopy);
    }

    /// <summary>Shows the game's current display (-1) or a draft.</summary>
    public void SelectDraft(int index)
    {
        Draft = Mathf.Clamp(index, -1, _drafts.Length - 1);
        bool current = Draft < 0;
        if (_currentOverlay != null) _currentOverlay.gameObject.SetActive(current);
        if (_overlay != null)
        {
            _overlay.gameObject.SetActive(!current);
            if (!current) ApplySettings();
        }
        RefreshButtons();
    }

    /// <summary>Lays a final-tier look over the selected draft without touching the draft asset.</summary>
    public void SelectLegend(LegendLook look)
    {
        Legend = look;
        if (_overlay != null && Draft >= 0) ApplySettings();
        RefreshButtons();
    }

    private void ApplySettings()
    {
        var draft = _drafts[Draft];
        if (Legend == LegendLook.Draft) { _overlay.Settings = draft; return; }
        if (_legendCopy != null) Destroy(_legendCopy);
        _legendCopy = Instantiate(draft);
        _legendCopy.Tiers = (UnitStatusBadgeSettings.TierStyle[])draft.Tiers.Clone();
        int top = _legendCopy.Tiers.Length - 1;
        bool gold = Legend >= LegendLook.GoldSweep;
        // Random Dice style: the lower tiers glint now and then; the final tier carries the shader effect instead.
        for (int i = 0; i < top; i++) { var lower = _legendCopy.Tiers[i]; lower.Glint = gold; _legendCopy.Tiers[i] = lower; }
        var style = _legendCopy.Tiers[top];
        style.Symbol = Legend == LegendLook.OutlineStar ? UnitStatusBadgeSettings.TierSymbol.StarOutline : UnitStatusBadgeSettings.TierSymbol.Star;
        style.Glint = false;
        style.Number = gold ? _legendGold : _legendCopy.Tiers[0].Number;
        style.Effect = !gold ? UnitStatusBadgeSettings.TagEffect.None
            : (UnitStatusBadgeSettings.TagEffect)(Legend - LegendLook.GoldSweep + (int)UnitStatusBadgeSettings.TagEffect.Sweep);
        style.RimWidth = 0f;
        _legendCopy.Tiers[top] = style;
        _overlay.Settings = _legendCopy;
    }

    /// <summary>Switches between pinned, live and ready-moment charge.</summary>
    public void SelectMode(LabMode mode)
    {
        Mode = mode;
        bool hold = mode != LabMode.Live;
        if (hold && !_held) _fieldPause.HoldAttacks(this);
        if (!hold && _held)
        {
            _fieldPause.Exit(this);
            _timer.StartTimer(_liveTimerSeconds);
        }
        _held = hold;
        if (mode == LabMode.Pinned) PinCharges();
        if (mode == LabMode.ReadyMoment) { _readyClock = 0f; SetGaugeCharges(_readyFrom); }
        RefreshButtons();
    }

    /// <summary>Sets every gauge back to its cell's 0 / 50 / 100 % value.</summary>
    public void PinCharges()
    {
        for (int i = 0; i < _units.Count && i < _cellCharge.Length; i++) SetCharge(_units[i], _cellCharge[i]);
    }

    /// <summary>Sets every charging unit to one value (lab only).</summary>
    public void SetGaugeCharges(float fraction)
    {
        foreach (var unit in _units) SetCharge(unit, fraction);
    }

    // Lab stand-in for real charging: timed skills use the shared timer, attack-charged units their private counters.
    private static void SetCharge(UnitBase unit, float fraction)
    {
        if (unit == null || !unit.HasSkillCooldownGauge) return;
        if (unit is Drone_Deltan deltan) SetCount(unit, "_attackCount", Mathf.RoundToInt(fraction * deltan.RequiredAttacks));
        else if (unit is Disigman disigman) SetCount(unit, "_chargedAttacks", Mathf.RoundToInt(fraction * disigman.RequiredAttacks));
        else unit._skillTimer = fraction * unit.GetCurrentSkillInterval();
    }

    private static void SetCount(UnitBase unit, string field, int value)
    {
        var info = unit.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        if (info == null) Debug.LogWarning($"UnitStatusBadgeFxLab: {unit.GetType().Name}.{field} not found", unit);
        else info.SetValue(unit, value);
    }

    private void Update()
    {
        if (!Ready) return;
        if (Mode == LabMode.Live)
        {
            if (_timer.RemainingTime < _liveTimerSeconds * 0.5f) _timer.StartTimer(_liveTimerSeconds);
            var boss = _bosses.CurrentBoss;
            if (boss != null && !boss.IsDead && boss.CurrentHp < boss.MaxHp * (decimal)_healBelow) boss.Heal(boss.MaxHp);
        }
        else if (Mode == LabMode.ReadyMoment)
        {
            float before = _readyClock;
            _readyClock += Time.unscaledDeltaTime;
            float half = _readyCycleSeconds * 0.5f;
            if (before < half && _readyClock >= half) SetGaugeCharges(1f);
            if (_readyClock >= _readyCycleSeconds) { _readyClock = 0f; SetGaugeCharges(_readyFrom); }
        }
    }

    private void RefreshButtons()
    {
        _currentButton.image.color = Draft < 0 ? Selected : Idle;
        for (int i = 0; i < _draftButtons.Length; i++) _draftButtons[i].image.color = i == Draft ? Selected : Idle;
        for (int i = 0; i < _modeButtons.Length; i++) _modeButtons[i].image.color = i == (int)Mode ? Selected : Idle;
        if (_legendButtons != null)
            for (int i = 0; i < _legendButtons.Length; i++) _legendButtons[i].image.color = i == (int)Legend ? Selected : Idle;
        if (_caption != null)
            _caption.text = $"{(Draft < 0 ? "현재 게임" : _draftNames[Draft])}  /  {ModeNames[(int)Mode]}  /  {LegendNames[(int)Legend]}";
    }
}
