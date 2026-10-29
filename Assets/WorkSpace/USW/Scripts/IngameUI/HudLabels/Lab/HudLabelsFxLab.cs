using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>
/// FxLab driver for L35: boots the copied game scene (no units, so nothing changes the boss),
/// then switches label drafts A/B/C and HUD states. Stands in for the one-line game call
/// <c>_timerLabel?.SetRemaining(remaining, isWaitTime)</c> in <c>UIManager.UpdateTimerUI</c>.
/// </summary>
public sealed class HudLabelsFxLab : MonoBehaviour
{
    /// <summary>HUD state shown in the lab.</summary>
    public enum LabState { Plenty, Low, ManyLines, Waiting }

    [SerializeField] private HudValueLabel _timerLabel;
    [SerializeField] private HudValueLabel _healthLabel;
    [SerializeField] private HudLabelSettings[] _presets;
    [SerializeField] private string[] _presetNames;
    [SerializeField] private Button[] _presetButtons;
    [SerializeField] private Button[] _stateButtons;
    [SerializeField] private Button _freezeButton;
    [SerializeField] private TMP_Text _freezeLabel;
    [SerializeField] private TMP_Text _caption;
    [SerializeField] private GameObject _controls;

    [Header("Lab values")]
    [SerializeField] private float _plentySeconds = 90f;
    [SerializeField] private float _lowStartSeconds = 12f;
    [SerializeField] private float _lowLoopFloor = 1.5f;
    [SerializeField] private float _waitSeconds = 5f;
    [SerializeField] private int _manyLines = 12;
    [SerializeField] private float _bootTimeout = 30f;

    private static readonly string[] StateNames = { "여유", "10초 이하", "체력 여러 줄", "보스 등장 대기" };
    private static readonly Color Selected = Color.white;
    private static readonly Color Idle = new Color(0.55f, 0.58f, 0.68f, 1f);

    private TimerController _timer;
    private UIManager _ui;
    private UI_BossHpBar _bar;
    private TimeScaleService _time;
    private BossBase _boss;
    private int _bossLines;
    private float _waitRemaining;
    private bool _frozen;

    /// <summary>Whether the game booted and the labels are live.</summary>
    public bool Ready { get; private set; }
    /// <summary>Current draft index into the presets.</summary>
    public int Preset { get; private set; }
    /// <summary>Current HUD state.</summary>
    public LabState State { get; private set; }
    /// <summary>Whether lab time is paused.</summary>
    public bool Frozen => _frozen;
    /// <summary>Timer label under test.</summary>
    public HudValueLabel TimerLabel => _timerLabel;
    /// <summary>Health label under test.</summary>
    public HudValueLabel HealthLabel => _healthLabel;

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
        for (int i = 0; i < _presetButtons.Length; i++)
        {
            int index = i;
            _presetButtons[i].onClick.AddListener(() => SelectPreset(index));
        }
        for (int i = 0; i < _stateButtons.Length; i++)
        {
            var state = (LabState)i;
            _stateButtons[i].onClick.AddListener(() => SelectState(state));
        }
        _freezeButton.onClick.AddListener(ToggleFreeze);
    }

    private void Start() => BootAsync(this.GetCancellationTokenOnDestroy()).Forget(e => Debug.LogException(e, this));

    private async UniTask BootAsync(CancellationToken token)
    {
        // Same editor-preview start as the other ingame FxLabs.
        await UniTask.Delay(TimeSpan.FromSeconds(1), DelayType.Realtime, cancellationToken: token);
        var container = Container;
        var game = container.Resolve<GameManager>();
        _time = container.Resolve<TimeScaleService>();
        _timer = container.Resolve<TimerController>();
        _ui = container.Resolve<UIManager>();
        game.OnStartButtonPressed();
        await UniTask.WaitUntil(() => FindFirstObjectByType<BossBase>() != null && game.CurrentState == GameManager.GameState.Playing,
            cancellationToken: token).Timeout(TimeSpan.FromSeconds(_bootTimeout));
        _boss = FindFirstObjectByType<BossBase>();
        _bar = FindFirstObjectByType<UI_BossHpBar>();
        _bossLines = _bar != null ? _bar.LineCount : 1;
        _timer.OnTimerTick += OnTimerTick;
        _controls.SetActive(true);
        SelectPreset(0);
        SelectState(LabState.Plenty);
        Ready = true;
    }

    private void OnDestroy()
    {
        if (_timer != null) _timer.OnTimerTick -= OnTimerTick;
        _time?.Release(this);
    }

    // Lab stand-in for the game hook: UIManager.UpdateTimerUI → _timerLabel.SetRemaining.
    private void OnTimerTick(float remaining)
    {
        if (State != LabState.Waiting) _timerLabel.SetRemaining(remaining);
    }

    /// <summary>Switches label draft A/B/C.</summary>
    public void SelectPreset(int index)
    {
        Preset = Mathf.Clamp(index, 0, _presets.Length - 1);
        _timerLabel.Settings = _presets[Preset];
        _healthLabel.Settings = _presets[Preset];
        if (_timer != null) _timerLabel.SetRemaining(State == LabState.Waiting ? _waitRemaining : _timer.RemainingTime, State == LabState.Waiting);
        RefreshButtons();
    }

    /// <summary>Puts the real HUD into one of the comparison states.</summary>
    public void SelectState(LabState state)
    {
        if (_timer == null) return;
        State = state;
        switch (state)
        {
            case LabState.Plenty:
            case LabState.ManyLines:
                _timer.StartTimer(_plentySeconds);
                break;
            case LabState.Low:
                _timer.StartTimer(_lowStartSeconds);
                break;
            case LabState.Waiting:
                _timer.StopTimer();
                _waitRemaining = _waitSeconds;
                // UIManager only rewrites the wait number when the whole second changes; the game
                // always enters a wait from a different second, the lab may not, so clear it first.
                _ui.UpdateTimerUI(0f, true);
                ShowWait();
                break;
        }
        if (_boss != null)
            _ui.BeginBossHp(_boss.CurrentHp, _boss.MaxHp, state == LabState.ManyLines ? _manyLines : _bossLines);
        RefreshButtons();
    }

    /// <summary>Pauses or resumes lab time so a state can be inspected still.</summary>
    public void ToggleFreeze()
    {
        _frozen = !_frozen;
        if (_frozen) _time.Pause(this); else _time.Release(this);
        RefreshButtons();
    }

    private void Update()
    {
        if (!Ready) return;
        if (State == LabState.Low && _timer.RemainingTime < _lowLoopFloor) _timer.StartTimer(_lowStartSeconds);
        if (State == LabState.Plenty || State == LabState.ManyLines)
            if (_timer.RemainingTime < _plentySeconds * 0.5f) _timer.StartTimer(_plentySeconds);
        if (State == LabState.Waiting)
        {
            _waitRemaining -= Time.deltaTime;
            if (_waitRemaining <= 0f) _waitRemaining = _waitSeconds;
            ShowWait();
        }
    }

    // Mirrors WaveManager's boss-arrival countdown, which drives the same number with isWaitTime.
    private void ShowWait()
    {
        _ui.UpdateTimerUI(_waitRemaining, true);
        _timerLabel.SetRemaining(_waitRemaining, true);
    }

    private void RefreshButtons()
    {
        for (int i = 0; i < _presetButtons.Length; i++) _presetButtons[i].image.color = i == Preset ? Selected : Idle;
        for (int i = 0; i < _stateButtons.Length; i++) _stateButtons[i].image.color = i == (int)State ? Selected : Idle;
        _freezeButton.image.color = _frozen ? Selected : Idle;
        if (_freezeLabel != null) _freezeLabel.text = _frozen ? "재생" : "멈춤";
        if (_caption != null)
            _caption.text = $"{_presetNames[Preset]}  /  {StateNames[(int)State]}";
    }
}
