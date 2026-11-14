using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>촬영 전용: 실제 선택 UI와 승천 연출을 반복한다. 계정·전투 수치는 변경하지 않는다.</summary>
public sealed class TestAscensionCapture : MonoBehaviour
{
    [SerializeField] private PenaltyRevealFx _fx;
    [SerializeField] private RunPenaltyData[] _penalties;
    [SerializeField, Min(0)] private float _repeatDelay = 2f;
    private void Start() => RunAsync(this.GetCancellationTokenOnDestroy()).Forget();
    private async UniTaskVoid RunAsync(CancellationToken token)
    {
        try
        {
            var choice = gameObject.AddComponent<PenaltyChoiceUI>();
            int index = 0;
            while (!token.IsCancellationRequested)
            {
                var offered = new[] { _penalties[index % _penalties.Length], _penalties[(index + 1) % _penalties.Length] };
                var selected = await choice.ChooseAsync(offered, token);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                _fx.gameObject.SetActive(true);
                await _fx.PlayAsync(selected, 1, token);
                await UniTask.Delay(TimeSpan.FromSeconds(_repeatDelay), ignoreTimeScale: true, cancellationToken: token);
                index = (index + 2) % _penalties.Length;
            }
        }
        catch (OperationCanceledException) { }
    }
}
