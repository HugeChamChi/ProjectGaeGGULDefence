using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>FxLab only: existing choices with an explicit field-view toggle and continuous pause ownership.</summary>
[DefaultExecutionOrder(1000)]
public sealed class FieldViewFxLab : MonoBehaviour
{
    private const float BootTimeout = 25f;
    private const float FadeSeconds = 0.18f;
    private const float DemoFood = 10000f;
    private const int DemoUnits = 6;
    private const float HudSlideSeconds = 0.28f;
    private const float HudSlideDistance = 600f;
    [SerializeField] private LevelUpUI _choices;
    [SerializeField] private CanvasGroup _panel;
    [SerializeField] private Button _button;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private GameObject _shield;
    [SerializeField] private GameObject _controls;
    [SerializeField] private RectTransform _lowerHud;
    [SerializeField] private CanvasGroup _lowerHudGroup;
    private TimeScaleService _time;
    private Tween _fade;
    private Tween _hudSlide;
    private Vector2 _hudHome;
    private float _hudAlpha;
    private bool _hudInteractable;
    private bool _hudRaycasts;
    private bool _hudCaptured;
    private bool _ready;
    private bool _transitioning;
    private LevelUpCardUI[] _cards = Array.Empty<LevelUpCardUI>();

    /// <summary>Whether the choices are hidden for inspection.</summary>
    public bool ViewingField { get; private set; }
    /// <summary>Whether the lab finished booting and revealing the existing choices.</summary>
    public bool Ready => _ready;

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
        _shield.SetActive(false);
        BindLowerHud();
        _choices.Opening += OnChoicesOpening;
        RefreshLabel();
    }

    private void Start() => BootAsync(this.GetCancellationTokenOnDestroy()).Forget(ReportError);

    private async UniTask BootAsync(CancellationToken token)
    {
        // Match the editor-preview preparation used by the other ingame FxLabs.
        await UniTask.Delay(TimeSpan.FromSeconds(1), DelayType.Realtime, cancellationToken: token);
        var container = Container;
        var game = container.Resolve<GameManager>();
        _time = container.Resolve<TimeScaleService>();
        game.OnStartButtonPressed();
        await UniTask.WaitUntil(() => FindFirstObjectByType<BossBase>() != null && game.CurrentState == GameManager.GameState.Playing,
            cancellationToken: token).Timeout(TimeSpan.FromSeconds(BootTimeout));
        container.Resolve<CurrencyManager>().AddCurrency(DemoFood);
        var spawner = container.Resolve<UnitSpawner>();
        for (int i = 0; i < DemoUnits; i++)
        {
            spawner.OnSpawnButtonPressed();
            await UniTask.Delay(TimeSpan.FromSeconds(0.22f), cancellationToken: token);
        }
        await UniTask.Delay(TimeSpan.FromSeconds(0.6f), cancellationToken: token);
        BindLowerHud();
        _time.Pause(this);
        _choices.Show();
        await UniTask.WaitUntil(() => _choices.IsReadyForSelection, cancellationToken: token);
        foreach (var hold in _panel.GetComponentsInChildren<UI_Peekthrough>(true)) hold.enabled = false;
        _cards = _panel.GetComponentsInChildren<LevelUpCardUI>(true);
        foreach (var card in _cards) card.ConfigurePeek(null);
        _controls.SetActive(true);
        _ready = true;
    }

    /// <summary>Button click: hide all choice visuals, or return to the same cards without redrawing.</summary>
    public void Toggle()
    {
        if (!_ready || _transitioning || !_panel.gameObject.activeInHierarchy) return;
        _transitioning = true;
        _button.interactable = false;
        _shield.SetActive(true);
        _panel.interactable = false;
        foreach (var card in _cards) { card.CancelDescriptionPress(); card.AllowSelection = false; }
        ViewingField = !ViewingField;
        RefreshLabel();
        _fade?.Kill();
        _fade = _panel.DOFade(ViewingField ? 0f : 1f, FadeSeconds).SetUpdate(true);
        CompleteTransitionAsync(this.GetCancellationTokenOnDestroy()).Forget(ReportError);
    }

    private async UniTask CompleteTransitionAsync(CancellationToken token)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(FadeSeconds), DelayType.Realtime, cancellationToken: token);
        // Keep the shield until every finger is up; a field press must never become a card click.
        await UniTask.WaitUntil(() => Input.touchCount == 0 && !Input.GetMouseButton(0), cancellationToken: token);
        await UniTask.NextFrame(token);
        if (!ViewingField)
        {
            foreach (var card in _cards) { card.CancelDescriptionPress(); card.AllowSelection = true; }
            _panel.interactable = true;
            _shield.SetActive(false);
        }
        _button.interactable = true;
        _transitioning = false;
    }

    private void Update()
    {
        if (!_ready || _panel.gameObject.activeInHierarchy) return;
        _controls.SetActive(false);
        SetHudHidden(false);
        _time?.Release(this);
        _ready = false;
    }

    private void OnChoicesOpening() => SetHudHidden(true);

    private void BindLowerHud()
    {
        if (_hudCaptured) return;
        if (_lowerHud == null)
            foreach (var candidate in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (candidate.name == "CombatHudPager") { _lowerHud = candidate; break; }
        if (_lowerHud == null) return;
        _lowerHudGroup = _lowerHud.GetComponent<CanvasGroup>();
        if (_lowerHudGroup == null) _lowerHudGroup = _lowerHud.gameObject.AddComponent<CanvasGroup>();
        _hudHome = _lowerHud.anchoredPosition;
        _hudAlpha = _lowerHudGroup.alpha;
        _hudInteractable = _lowerHudGroup.interactable;
        _hudRaycasts = _lowerHudGroup.blocksRaycasts;
        _hudCaptured = true;
    }

    private void SetHudHidden(bool hidden)
    {
        if (_lowerHud == null || _lowerHudGroup == null) return;
        _hudSlide?.Kill();
        _lowerHudGroup.interactable = false;
        _lowerHudGroup.blocksRaycasts = false;
        _lowerHudGroup.alpha = _hudAlpha;
        var destination = hidden ? _hudHome + Vector2.down * HudSlideDistance : _hudHome;
        _hudSlide = _lowerHud.DOAnchorPos(destination, HudSlideSeconds).SetUpdate(true).SetEase(Ease.InOutCubic)
            .OnComplete(() =>
            {
                _lowerHudGroup.alpha = hidden ? 0f : _hudAlpha;
                _lowerHudGroup.interactable = !hidden && _hudInteractable;
                _lowerHudGroup.blocksRaycasts = !hidden && _hudRaycasts;
            });
    }

    private void RefreshLabel()
    {
        const float IconWidth = 42f;
        const float Gap = 18f;
        _label.text = ViewingField ? "선택지로 돌아가기" : "필드 보기";
        float width = _label.preferredWidth;
        _label.rectTransform.sizeDelta = new Vector2(width + 8f, 86f);
        _label.rectTransform.anchoredPosition = new Vector2((IconWidth + Gap) * 0.5f, 2f);
        var eye = _button.GetComponentInChildren<FieldViewEye>(true);
        if (eye != null) eye.rectTransform.anchoredPosition = new Vector2(-(width + Gap) * 0.5f, 2f);
    }

    private void OnDestroy()
    {
        _fade?.Kill();
        _hudSlide?.Kill();
        if (_choices != null) _choices.Opening -= OnChoicesOpening;
        if (_lowerHud != null) _lowerHud.anchoredPosition = _hudHome;
        if (_lowerHudGroup != null)
        {
            _lowerHudGroup.alpha = _hudAlpha;
            _lowerHudGroup.interactable = _hudInteractable;
            _lowerHudGroup.blocksRaycasts = _hudRaycasts;
        }
        _time?.Release(this);
    }

    private void ReportError(Exception error)
    {
        if (error is not OperationCanceledException) Debug.LogException(error, this);
    }
}
