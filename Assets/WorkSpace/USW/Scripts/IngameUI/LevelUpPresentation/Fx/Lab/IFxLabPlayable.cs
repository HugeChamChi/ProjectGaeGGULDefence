/// <summary>
/// 이펙트 실험실(FxLab 씬) 캡처 대상 — FxLabCapture가 Play()로 재생을 시작한다.
/// UseUnscaledTime은 고정 프레임 캡처(captureFramerate) 중에만 false로 바뀐다.
/// </summary>
public interface IFxLabPlayable
{
    /// <summary>처음부터 재생한다.</summary>
    void Play();

    /// <summary>timeScale 무시 여부. 고정 프레임 캡처는 scaled 시간만 고정하므로 캡처 중 false로 바꾼다.</summary>
    bool UseUnscaledTime { get; set; }
}
