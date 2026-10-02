/// <summary>
/// 보스 처치 공지 시안 하나. BossClearNoticeLab이 타이머 실험실과 같은 시계로 매 프레임 그린다.
/// 같은 n에는 항상 같은 화면이 나와야 한다 (히트스톱·슬로모션 중 n이 멈추거나 느려진다).
/// </summary>
public interface IBossClearNotice
{
    /// <summary>버튼·라벨 이름.</summary>
    string Name { get; }

    /// <summary>시안 전용 UI를 만든다 (시작 시 한 번).</summary>
    void Setup(BossClearNoticeLab lab);

    /// <summary>선택되지 않은 시안을 숨긴다.</summary>
    void SetVisible(bool visible);

    /// <summary>한 프레임 그리기.</summary>
    /// <param name="n">공지 시작 기준 초 (시작 전 음수).</param>
    /// <param name="exitAt">사라지기 시작하는 시점 (n 기준).</param>
    /// <param name="exitScale">사라짐 길이 배율.</param>
    void Render(float n, float exitAt, float exitScale);
}
