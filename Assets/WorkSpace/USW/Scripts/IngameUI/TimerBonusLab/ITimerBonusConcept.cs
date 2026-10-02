/// <summary>
/// 보스 처치 시간 보너스 시안 하나. TimerBonusLab이 매 프레임 같은 시계로 그린다.
/// 시계가 멈추면(히트스톱·재생 끝) a가 그대로이므로, 같은 a에는 항상 같은 화면이 나와야 한다.
/// </summary>
public interface ITimerBonusConcept
{
    /// <summary>버튼·상태 줄에 쓰는 이름.</summary>
    string Name { get; }

    /// <summary>시안 전용 UI를 만든다 (실험실 시작 시 한 번).</summary>
    void Setup(TimerBonusLab lab);

    /// <summary>선택되지 않은 시안의 UI를 숨긴다.</summary>
    void SetVisible(bool visible);

    /// <summary>한 프레임 그리기.</summary>
    /// <param name="a">처치 순간 기준 초 (처치 전은 음수).</param>
    /// <param name="step">이번 프레임 진행 초 (히트스톱·정지 중 0) — 파티클 생성량 등에 쓴다.</param>
    void Render(float a, float step);
}
