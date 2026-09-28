using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>
/// TotemSelectTest 씬 전용 데미지 표시 비교 테스트.
/// 보스 피해를 받아 모비노기형/쿠키런형 중 고른 방식으로 띄우고, 폰트를 바꿔 가며 비교한다.
/// 켜져 있는 동안 기존 DamageFloaterManager 출력은 끈다 (SuppressOutput). 본 게임 씬에는 두지 않는다.
/// </summary>
public sealed class DamageStyleLab : MonoBehaviour
{
    /// <summary>표시 방식.</summary>
    public enum Style { Mobinogi, CookieRun }

    [Inject] private BossManager _bossManager;
    [Inject] private DamageFloaterManager _floaterManager;

    [SerializeField] private DamageStyleLabSettings _settings;

    private RectTransform _container;
    private IDamageStyleView[] _views;
    private BossBase _boss;
    private CancellationTokenSource _sampleCts;

    /// <summary>현재 방식.</summary>
    public Style CurrentStyle { get; private set; } = Style.Mobinogi;
    /// <summary>현재 폰트 번호 (Settings.Fonts 기준).</summary>
    public int FontIndex { get; private set; }
    /// <summary>현재 폰트 표시 이름.</summary>
    public string FontName => HasFonts ? _settings.Fonts[FontIndex].DisplayName : "-";
    /// <summary>현재 방식 표시 이름.</summary>
    public string StyleName => CurrentStyle == Style.Mobinogi ? "모비노기형" : "쿠키런형";

    private bool HasFonts => _settings != null && _settings.Fonts != null && _settings.Fonts.Length > 0;
    private IDamageStyleView Active => _views[(int)CurrentStyle];
    private bool LoopSample => Application.isEditor && _settings.SampleLoopInEditor;

    private void Start()
    {
        if (_settings == null) { Debug.LogError("[DamageStyleLab] Settings 미연결", this); enabled = false; return; }

        // 기존 DamageCanvas는 월드 공간이라 화면 픽셀 기준 배치가 맞지 않는다.
        // 이 오브젝트(화면 공간 테스트 캔버스) 아래에 정렬 순서만 낮춘 하위 캔버스를 만들어 띄운다.
        _container = DamageStyleLabUtil.CreateRoot((RectTransform)transform, "DamageStyleLab");
        _container.SetAsFirstSibling();
        var canvas = _container.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = _settings.SortingOrder;
        _views = new IDamageStyleView[]
        {
            new MobinogiDamageView(_settings, _container),
            new CookieDamageView(_settings, _container),
        };
        ApplyFont();

        if (_floaterManager != null) _floaterManager.SuppressOutput = true;
        if (_bossManager != null)
        {
            _bossManager.OnBossEntryed += OnBossEntered;
            OnBossEntered(null, null);
        }
    }

    private void OnDestroy()
    {
        CancelSample();
        if (_floaterManager != null) _floaterManager.SuppressOutput = false;
        if (_bossManager != null) _bossManager.OnBossEntryed -= OnBossEntered;
        Unsubscribe();
    }

    private void LateUpdate()
    {
        if (_views == null) return;
        Active.Tick(Time.unscaledTime, Time.unscaledDeltaTime);
    }

    /// <summary>모비노기형 ↔ 쿠키런형.</summary>
    public void NextStyle()
    {
        Active.Clear();
        CurrentStyle = CurrentStyle == Style.Mobinogi ? Style.CookieRun : Style.Mobinogi;
    }

    /// <summary>다음 폰트로.</summary>
    public void NextFont()
    {
        if (!HasFonts) return;
        FontIndex = (FontIndex + 1) % _settings.Fonts.Length;
        ApplyFont();
    }

    /// <summary>보스를 기다리지 않고 가짜 피해를 SampleSeconds 동안 넣는다. 다시 누르면 처음부터.</summary>
    public void PlaySample()
    {
        CancelSample();
        _sampleCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        SampleLoop(_sampleCts.Token).Forget();
    }

    private async UniTaskVoid SampleLoop(CancellationToken token)
    {
        float end = Time.unscaledTime + _settings.SampleSeconds;
        int intervalMs = Mathf.Max(1, Mathf.RoundToInt(_settings.SampleHitInterval * 1000f));
        while ((Time.unscaledTime < end || LoopSample) && !token.IsCancellationRequested)
        {
            float roll = Random.value;
            float spread = 1f + Random.Range(-_settings.SampleDamageSpread, _settings.SampleDamageSpread);
            if (roll < _settings.SampleBurnChance)
                Active.Add((decimal)(_settings.SampleBurnDamage * spread), BossDamageKind.Burn);
            else if (roll < _settings.SampleBurnChance + _settings.SampleCriticalChance)
                Active.Add((decimal)(_settings.SampleNormalDamage * _settings.SampleCriticalMultiplier * spread), BossDamageKind.Critical);
            else
                Active.Add((decimal)(_settings.SampleNormalDamage * spread), BossDamageKind.Normal);
            await UniTask.Delay(intervalMs, DelayType.UnscaledDeltaTime, cancellationToken: token).SuppressCancellationThrow();
        }
    }

    private void CancelSample()
    {
        if (_sampleCts == null) return;
        _sampleCts.Cancel();
        _sampleCts.Dispose();
        _sampleCts = null;
    }

    private void ApplyFont()
    {
        if (!HasFonts || _views == null) return;
        var entry = _settings.Fonts[FontIndex];
        foreach (var view in _views) view.SetFont(entry.Font, entry.Material);
    }

    private void OnBossEntered(BossEntry _, BossEntry _1)
    {
        Unsubscribe();
        _boss = _bossManager != null ? _bossManager.CurrentBoss : null;
        if (_boss != null) _boss.OnDamageDealt += OnBossDamaged;
        if (_views == null) return;
        foreach (var view in _views) view.SetAnchor(_boss != null ? _boss.transform : null);
    }

    private void Unsubscribe()
    {
        if (_boss != null) _boss.OnDamageDealt -= OnBossDamaged;
        _boss = null;
    }

    private void OnBossDamaged(decimal damage, Vector3? hitPos, BossDamageKind kind) => Active.Add(damage, kind);
}
