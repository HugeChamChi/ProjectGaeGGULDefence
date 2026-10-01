using UnityEngine;
using VContainer;

/// <summary>
/// 인게임 보스 피해 숫자 — 모비노기형(보스 옆에 크고 기울어진 숫자를 세로로 쌓기).
/// 화면 공간 캔버스 위에 MobinogiDamageView를 띄우고, 켜져 있는 동안 기존 DamageFloaterManager 출력은 끈다.
/// 수치·색·폰트는 DamageStyleLabSettings 에셋(BossDamageNumberSettings)에서 읽는다.
/// </summary>
[RequireComponent(typeof(Canvas))]
public sealed class BossDamageNumbers : MonoBehaviour
{
    [Inject] private BossManager _bossManager;
    [Inject] private DamageFloaterManager _floaterManager;
    private GamePresentationSettings _presentationSettings;

    /// <summary>두 피해 숫자 출력기에서 동일한 Player 설정을 사용한다.</summary>
    [Inject]
    public void ConfigurePresentation(GamePresentationSettings settings)
    {
        if (_presentationSettings != null) _presentationSettings.OnChanged -= OnPresentationChanged;
        _presentationSettings = settings;
        _presentationSettings.OnChanged += OnPresentationChanged;
        OnPresentationChanged();
    }

    private void OnPresentationChanged()
    {
        if (_presentationSettings?.ShowDamageNumbers == false) _view?.Clear();
    }

    [SerializeField] private DamageStyleLabSettings _settings;
    [Tooltip("Settings.Fonts 중 사용할 폰트 번호.")]
    [SerializeField, Min(0)] private int _fontIndex;

    private MobinogiDamageView _view;
    private BossBase _boss;
    private bool _subscribed;
    private bool _previousSuppressOutput;

    private void Start()
    {
        if (_settings == null) { Debug.LogError("[BossDamageNumbers] Settings 미연결", this); enabled = false; return; }

        GetComponent<Canvas>().sortingOrder = _settings.SortingOrder;
        var root = DamageStyleLabUtil.CreateRoot((RectTransform)transform, "BossDamageNumbers");
        _view = new MobinogiDamageView(_settings, root);
        if (_settings.Fonts != null && _settings.Fonts.Length > 0)
        {
            var entry = _settings.Fonts[Mathf.Clamp(_fontIndex, 0, _settings.Fonts.Length - 1)];
            _view.SetFont(entry.Font, entry.Material);
        }

        Subscribe();
    }

    private void OnEnable() => Subscribe();

    private void Subscribe()
    {
        // OnEnable runs before Start has built the view on the first activation.
        if (_view == null || _subscribed || !isActiveAndEnabled) return;
        _subscribed = true;
        if (_floaterManager != null)
        {
            _previousSuppressOutput = _floaterManager.SuppressOutput;
            _floaterManager.SuppressOutput = true;
        }
        if (_bossManager != null)
        {
            _bossManager.OnBossEntryed += OnBossEntered;
            OnBossEntered(null, null);
        }
    }

    private void OnDisable() => Release();

    private void OnDestroy()
    {
        Release();
        if (_presentationSettings != null) _presentationSettings.OnChanged -= OnPresentationChanged;
    }

    private void Release()
    {
        if (_subscribed)
        {
            if (_floaterManager != null) _floaterManager.SuppressOutput = _previousSuppressOutput;
            if (_bossManager != null) _bossManager.OnBossEntryed -= OnBossEntered;
            _subscribed = false;
        }
        Unsubscribe();
        _view?.SetAnchor(null);
        _view?.Clear();
    }

    private void LateUpdate()
    {
        _view?.Tick(Time.unscaledTime, Time.unscaledDeltaTime);
    }

    private void OnBossEntered(BossEntry _, BossEntry _1)
    {
        Unsubscribe();
        _boss = _bossManager != null ? _bossManager.CurrentBoss : null;
        if (_boss != null) _boss.OnDamageDealt += OnBossDamaged;
        _view?.SetAnchor(_boss != null ? _boss.transform : null);
    }

    private void Unsubscribe()
    {
        if (_boss != null) _boss.OnDamageDealt -= OnBossDamaged;
        _boss = null;
    }

    private void OnBossDamaged(decimal damage, Vector3? hitPos, BossDamageKind kind)
    {
        if (_presentationSettings?.ShowDamageNumbers == false) return;
        _view?.Add(damage, kind);
    }
}
