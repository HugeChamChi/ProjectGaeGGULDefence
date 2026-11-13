using System;
using GaeGGUL.UI.Unit;
using GGD.BackendCharts;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

/// <summary>
/// Connects the conversion stone system to the scene: grants stones after each boss clear, shows the center toast,
/// builds the HUD counter and the unit conversion button. Inactive in the tutorial.
/// </summary>
public sealed class ConversionBinding : IInitializable, IDisposable
{
    /// <summary>Resources path of the stone icon sprite.</summary>
    public const string IconResource = "Conversion/ConversionStoneIcon";
    /// <summary>TMP sprite asset name under Resources/Sprite Assets, used inline in toasts.</summary>
    public const string ToastSpriteTag = "<sprite=\"ConversionStone\" index=0>";

    private readonly ConversionStoneWallet _wallet;
    private readonly ConversionService _service;
    private readonly WaveManager _waves;
    private readonly GameManager _game;
    private readonly UIManager _ui;
    private readonly MergeManager _merge;
    private readonly IObjectResolver _resolver;
    private CenterToast _toast;
    private ConversionStoneHud _hud;
    private ConversionButtonView _button;
    private bool _active;

    public ConversionBinding(ConversionStoneWallet wallet, ConversionService service, WaveManager waves, GameManager game,
        UIManager ui, MergeManager merge, IObjectResolver resolver)
    { _wallet = wallet; _service = service; _waves = waves; _game = game; _ui = ui; _merge = merge; _resolver = resolver; }

    /// <summary>Subscribes before the first boss and builds the UI once.</summary>
    public void Initialize()
    {
        if (BalanceChart.TutorialRunning) return;
        _active = true;
        _resolver.TryResolve(out _toast);
        _waves.OnBossKillTimed += OnBossKill;
        _game.OnGameStart += _wallet.Reset;

        var icon = Resources.Load<Sprite>(IconResource);
        if (_ui.CurrencyText != null) _hud = ConversionStoneHud.Create(_ui.CurrencyText, _wallet, icon);
        var panel = Object.FindFirstObjectByType<UI_UnitInfoPanel>(FindObjectsInactive.Include);
        if (panel != null)
        {
            _button = ConversionButtonView.Create((RectTransform)panel.transform, _service, _wallet, icon,
                _ui.CurrencyText != null ? _ui.CurrencyText.font : null, Warn);
            _merge.OnUnitSelected += _button.Show;
            _merge.OnSelectionCleared += _button.Hide;
            DragHandler.OnDragStartedEvent += HideButton;
        }
    }

    private void HideButton() => _button?.Hide();

    private void Warn(string message)
    {
        if (!string.IsNullOrEmpty(message)) _toast?.Show(message, CenterToastKind.Warning);
    }

    private void OnBossKill(EndlessBossRole role, double combatSeconds)
    {
        if (role != EndlessBossRole.Hard && role != EndlessBossRole.Breather) return;
        var reward = ConversionRules.ForKill(_game.Config, role, combatSeconds);
        _wallet.Add(reward.Stones);
        _toast?.Show(ToastText(reward), CenterToastKind.Info);
    }

    /// <summary>"신속한 처치 (icon) +2", "보스 처치 (icon) +1" or "보스 처치" with no stones.</summary>
    public static string ToastText(ConversionKillReward reward)
    {
        if (reward.Stones <= 0) return "보스 처치";
        return (reward.Fast ? "신속한 처치 " : "보스 처치 ") + ToastSpriteTag + " +" + reward.Stones;
    }

    /// <summary>Unsubscribes at scene end.</summary>
    public void Dispose()
    {
        if (!_active) return;
        _waves.OnBossKillTimed -= OnBossKill;
        _game.OnGameStart -= _wallet.Reset;
        if (_button != null)
        {
            _merge.OnUnitSelected -= _button.Show;
            _merge.OnSelectionCleared -= _button.Hide;
        }
        DragHandler.OnDragStartedEvent -= HideButton;
    }
}
