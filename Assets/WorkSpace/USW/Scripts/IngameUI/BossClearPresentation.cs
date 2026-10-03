using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>Owns cancellable timer absorption, committed bonus and delayed clear notice; WaveManager awaits it before rewards.</summary>
public sealed class BossClearPresentation : MonoBehaviour
{
    [SerializeField] private TimerBonusLab _fusion;
    [SerializeField] private RectTransform _stage;
    private TimerController _timer;
    private WaveManager _waves;
    private BossManager _bosses;
    private UIManager _ui;
    private GameConfig _config;
    private BossClearNoticeLab _notice;
    private CancellationTokenSource _presentationCts;

    /// <summary>Uses existing scene references passed by the injected installer; no runtime manager searches.</summary>
    public void Configure(TimerController timer, WaveManager waves, BossManager bosses, UIManager ui, GameConfig config)
    {
        Release();
        _timer = timer; _waves = waves; _bosses = bosses; _ui = ui; _config = config;
        _notice = _fusion != null ? _fusion.GetComponent<BossClearNoticeLab>() : null;
        if (_timer != null) _timer.OnTimeAdded += OnAdditionalTimeAdded;
        if (_waves != null) { _waves.OnRunStopped += Cancel; _waves.OnWaveChanged += OnWaveChanged; }
    }

    /// <summary>Absorbs from the captured dying boss position, adds time once at impact, then shows the notice after the SO delay.</summary>
    public UniTask PlayBossClearAsync(float seconds, Vector3? bossPosition, GameConfig config, CancellationToken token) =>
        PresentAsync(seconds, bossPosition, config, true, token);

    private void OnAdditionalTimeAdded(float seconds)
    {
        // The committed addition inside PresentAsync must not start a second sequence.
        if (_presentationCts != null || seconds <= 0f || !isActiveAndEnabled) return;
        PresentAdditionalAsync(seconds).Forget();
    }

    private async UniTaskVoid PresentAdditionalAsync(float seconds)
    {
        try
        {
            var boss = _bosses != null ? _bosses.CurrentBoss : null;
            await PresentAsync(seconds, boss != null ? (Vector3?)boss.transform.position : null, _config, false,
                this.GetCancellationTokenOnDestroy());
        }
        catch (OperationCanceledException) { }
    }

    private async UniTask PresentAsync(float seconds, Vector3? bossPosition, GameConfig config, bool pending, CancellationToken token)
    {
        if (!isActiveAndEnabled || _fusion == null || _stage == null || _timer == null)
        {
            token.ThrowIfCancellationRequested();
            if (pending && seconds > 0f) _timer?.AddTime(seconds);
            return;
        }
        if (seconds <= 0f) return;
        Cancel();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        _presentationCts = cts;
        try
        {
            Vector2 point = bossPosition.HasValue && Camera.main != null
                ? DamageStyleLabUtil.ScreenToLocal(_stage, RectTransformUtility.WorldToScreenPoint(Camera.main, bossPosition.Value)) : Vector2.zero;
            if (_ui != null) _ui.TimerPresentationActive = true;
            _fusion.Present(_timer, seconds, point, pending);
            if (pending)
            {
                await UniTask.WaitUntil(() => _fusion.HasReachedImpact || !_fusion.IsPresenting, cancellationToken: cts.Token);
                cts.Token.ThrowIfCancellationRequested();
                _timer.AddTime(seconds);
                _fusion.NotifyBonusApplied();
            }
            await UniTask.WaitUntil(() => _fusion.IsBonusCountComplete || !_fusion.IsPresenting, cancellationToken: cts.Token);
            if (config != null)
                await UniTask.Delay(TimeSpan.FromSeconds(config.BossClearNoticeDelaySeconds), ignoreTimeScale: true, cancellationToken: cts.Token);
            if (_notice != null)
            {
                _notice.PresentRuntimeNotice();
                await UniTask.WaitUntil(() => !_notice.IsPresenting, cancellationToken: cts.Token);
            }
            await UniTask.WaitUntil(() => !_fusion.IsPresenting, cancellationToken: cts.Token);
        }
        finally
        {
            if (ReferenceEquals(_presentationCts, cts))
            {
                _presentationCts = null;
                ClearVisuals();
            }
        }
    }

    private void LateUpdate()
    {
        if (_ui != null) _ui.TimerPresentationActive = _fusion != null && _fusion.IsPresenting;
    }

    private void OnWaveChanged(int _) => Cancel();
    private void ClearVisuals()
    {
        if (_fusion != null) _fusion.CancelPresentation();
        if (_notice != null) _notice.CancelRuntimeNotice();
        if (_ui != null) _ui.TimerPresentationActive = false;
    }
    private void Cancel()
    {
        _presentationCts?.Cancel();
        ClearVisuals();
    }
    private void Release()
    {
        if (_timer != null) _timer.OnTimeAdded -= OnAdditionalTimeAdded;
        if (_waves != null) { _waves.OnRunStopped -= Cancel; _waves.OnWaveChanged -= OnWaveChanged; }
        Cancel();
    }
    private void OnDisable() => Cancel();
    private void OnDestroy() => Release();
}
