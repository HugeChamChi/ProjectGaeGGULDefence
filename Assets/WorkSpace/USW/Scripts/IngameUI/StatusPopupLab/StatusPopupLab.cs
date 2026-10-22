using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>
/// 버프·디버프 상태 팝업 실험실(FxLab_StatusPopup) 드라이버 (사용자 요청 2026-10-22).
/// 가짜 유닛 그림 위에 StatusPopupSpawner로 1~3줄 묶음, 연달아 걸기, 같은 효과 반복을 띄워 본다.
/// 유닛마다 스포너(=스타일)가 달라, 버튼 하나로 세 유닛에 같은 효과를 동시에 걸어 퇴장 타이밍 시안을 나란히 비교한다.
/// </summary>
public class StatusPopupLab : MonoBehaviour
{
    private const float ChainInterval = 0.3f;
    private const float AutoInterval = 1.6f;
    private const string AttackSpeed = "공격속도";
    private const string Attack = "공격력";
    private const string Range = "사거리";

    [Tooltip("유닛과 같은 순서. 유닛 i는 스포너 i의 스타일로 뜬다")]
    [SerializeField] private StatusPopupSpawner[] _spawners;
    [SerializeField] private RectTransform[] _units;
    [SerializeField] private TMP_Text _autoLabel;

    private CancellationTokenSource _autoCts;

    /// <summary>버프 한 줄.</summary>
    public void OneBuff() => Show((AttackSpeed, true));

    /// <summary>버프 두 줄.</summary>
    public void TwoBuffs() => Show((AttackSpeed, true), (Attack, true));

    /// <summary>버프 + 디버프.</summary>
    public void BuffAndDebuff() => Show((AttackSpeed, true), (Attack, false));

    /// <summary>세 줄(최대).</summary>
    public void ThreeLines() => Show((AttackSpeed, true), (Attack, true), (Range, false));

    /// <summary>같은 버프를 두 번 — 줄이 늘지 않고 통 튀기만 해야 한다.</summary>
    public void RepeatSame() => Show((AttackSpeed, true), (AttackSpeed, true));

    /// <summary>범위 버프처럼 모든 유닛에 동시에.</summary>
    public void AllUnits() => Show((AttackSpeed, true), (Attack, true));

    /// <summary>0.3초 간격으로 하나씩 — 묶음에 줄이 붙고 유지 시간이 연장되는지 본다.</summary>
    public void Chain() => ChainAsync(this.GetCancellationTokenOnDestroy()).Forget(LogUnlessCanceled);

    /// <summary>임의 조합 자동 반복을 켜고 끈다.</summary>
    public void ToggleAuto()
    {
        if (_autoCts != null)
        {
            StopAuto();
        }
        else
        {
            _autoCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            AutoAsync(_autoCts.Token).Forget(LogUnlessCanceled);
        }
        if (_autoLabel != null) _autoLabel.text = _autoCts != null ? "자동 끄기" : "자동 켜기";
    }

    private void OnDestroy() => StopAuto();

    private void StopAuto()
    {
        _autoCts?.Cancel();
        _autoCts?.Dispose();
        _autoCts = null;
    }

    private async UniTask ChainAsync(CancellationToken token)
    {
        Show((AttackSpeed, true));
        await UniTask.Delay(TimeSpan.FromSeconds(ChainInterval), cancellationToken: token);
        Show((Attack, true));
        await UniTask.Delay(TimeSpan.FromSeconds(ChainInterval), cancellationToken: token);
        Show((Range, false));
    }

    private async UniTask AutoAsync(CancellationToken token)
    {
        var presets = new Action[] { OneBuff, TwoBuffs, BuffAndDebuff, ThreeLines, AllUnits };
        while (true)
        {
            presets[UnityEngine.Random.Range(0, presets.Length)]();
            await UniTask.Delay(TimeSpan.FromSeconds(AutoInterval), cancellationToken: token);
        }
    }

    // 세 유닛 모두에 같은 효과를 건다. 유닛 i는 스포너 i(없으면 마지막 스포너)로 띄운다.
    private void Show(params (string text, bool isBuff)[] effects)
    {
        if (_spawners == null || _spawners.Length == 0 || _units == null) return;
        for (int i = 0; i < _units.Length; i++)
        {
            var spawner = _spawners[Mathf.Min(i, _spawners.Length - 1)];
            if (spawner == null) continue;
            foreach (var (text, isBuff) in effects) spawner.Show(_units[i], text, isBuff);
        }
    }

    private static void LogUnlessCanceled(Exception e)
    {
        if (e is not OperationCanceledException) Debug.LogException(e);
    }
}
