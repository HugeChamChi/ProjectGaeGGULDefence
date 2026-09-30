using UnityEngine;

/// <summary>
/// 패널티 연출 실험실(FxLab_Penalty) 드라이버 — 버튼으로 패널티별 재생, 같은 패널티를 다시 누르면 누적 횟수(×N)가 오른다.
/// FxLabCapture 캡처 대상 (Play() = _captureIndex 패널티). 게임 코드와 무관한 실험실 전용.
/// </summary>
public class PenaltyFxLab : MonoBehaviour, IFxLabPlayable
{
    [SerializeField] private PenaltyRevealFx _fx;
    [SerializeField] private RunPenaltyData[] _penalties = new RunPenaltyData[0];
    [Tooltip("캡처(Play) 때 재생할 패널티 인덱스")]
    [SerializeField] private int _captureIndex = 1;

    private int[] _stacks = new int[0];

    /// <inheritdoc />
    public bool UseUnscaledTime
    {
        get => _fx == null || _fx.UseUnscaledTime;
        set { if (_fx != null) _fx.UseUnscaledTime = value; }
    }

    /// <summary>캡처용 재생 (누적 1회로).</summary>
    public void Play()
    {
        if (_fx == null || _penalties.Length == 0) return;
        _fx.Play(_penalties[Mathf.Clamp(_captureIndex, 0, _penalties.Length - 1)], 1);
    }

    /// <summary>index번 패널티를 재생한다. 누를 때마다 그 패널티의 누적 횟수가 1씩 오른다.</summary>
    public void PlayIndex(int index)
    {
        if (_fx == null || index < 0 || index >= _penalties.Length) return;
        if (_stacks.Length != _penalties.Length) _stacks = new int[_penalties.Length];
        _stacks[index]++;
        _fx.Play(_penalties[index], _stacks[index]);
    }

    /// <summary>무작위 패널티 재생.</summary>
    public void PlayRandom() => PlayIndex(Random.Range(0, _penalties.Length));

    /// <summary>누적 횟수 초기화.</summary>
    public void ResetStacks() => _stacks = new int[_penalties.Length];
}
