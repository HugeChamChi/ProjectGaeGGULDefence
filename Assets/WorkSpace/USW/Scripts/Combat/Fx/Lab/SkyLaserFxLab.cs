using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 이펙트 실험실(FxLab_SkyLaser 씬) 전용 — 하늘 레이저 Pulse/Sustain을 번갈아 반복 재생한다.
/// FxLabCapture 대상이 되도록 IFxLabPlayable을 구현한다 (Play = 순서 처음부터).
/// 인스펙터 _show로 한 종류만 반복해서 볼 수도 있다.
/// </summary>
public class SkyLaserFxLab : MonoBehaviour, IFxLabPlayable
{
    /// <summary>실험실에서 반복할 연출.</summary>
    public enum Show { Both, PulseOnly, SustainOnly }

    [SerializeField] private SkyLaserFx _pulse;
    [SerializeField] private SkyLaserFx _sustain;
    [SerializeField] private Show _show = Show.Both;
    [Tooltip("한 연출이 끝난 뒤 다음 연출까지 쉬는 시간")]
    [SerializeField] private float _rest = 0.8f;
    [SerializeField] private bool _playOnStart = true;

    private CancellationTokenSource _cts;
    private bool _unscaled;

    /// <inheritdoc/>
    public bool UseUnscaledTime
    {
        get => _unscaled;
        set
        {
            _unscaled = value;
            if (_pulse != null) _pulse.UseUnscaledTime = value;
            if (_sustain != null) _sustain.UseUnscaledTime = value;
        }
    }

    private void Start()
    {
        if (_playOnStart) Play();
    }

    private void OnDestroy()
    {
        CancelRun();
    }

    /// <summary>순서를 처음부터 다시 재생한다.</summary>
    [ContextMenu("Play")]
    public void Play()
    {
        CancelRun();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        LoopAsync(_cts).Forget();
    }

    [ContextMenu("Show Pulse Only")]
    private void ShowPulse() { _show = Show.PulseOnly; Play(); }

    [ContextMenu("Show Sustain Only")]
    private void ShowSustain() { _show = Show.SustainOnly; Play(); }

    [ContextMenu("Show Both")]
    private void ShowBoth() { _show = Show.Both; Play(); }

    // 이전 순서의 finally는 다음 프레임에 돌기 때문에, 연출 정지는 여기서 즉시 한다 (새 순서의 재생을 끊지 않도록).
    private void CancelRun()
    {
        if (_cts == null) return;
        var cts = _cts;
        _cts = null;
        cts.Cancel();
        if (_pulse != null) _pulse.Stop();
        if (_sustain != null) _sustain.Stop();
    }

    private async UniTaskVoid LoopAsync(CancellationTokenSource cts)
    {
        var token = cts.Token;
        try
        {
            if (_sustain != null) _sustain.Loop = false;
            while (true)
            {
                if (_show != Show.SustainOnly) await PlayOnceAsync(_pulse, token);
                if (_show != Show.PulseOnly) await PlayOnceAsync(_sustain, token);
            }
        }
        catch (System.OperationCanceledException)
        {
        }
        finally
        {
            if (_cts == cts) _cts = null;
            cts.Dispose();
        }
    }

    private async UniTask PlayOnceAsync(SkyLaserFx fx, CancellationToken token)
    {
        if (fx == null) return;
        fx.Play();
        await UniTask.WaitWhile(() => fx.IsPlaying, cancellationToken: token);
        await UniTask.Delay((int)(_rest * 1000f), _unscaled, cancellationToken: token);
    }
}
