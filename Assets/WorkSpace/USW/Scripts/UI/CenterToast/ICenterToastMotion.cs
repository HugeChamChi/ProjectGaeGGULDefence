/// <summary>중앙 알림 연출 방식 하나 (CenterToastStyle마다 구현).</summary>
public interface ICenterToastMotion
{
    /// <summary>같은 문구가 떠 있으면 새로 만들지 않고 합친다 (횟수 +1, 수명 다시 시작).</summary>
    bool MergeRepeats { get; }

    /// <summary>한 번에 하나만 — 새 알림이 오면 이전 것을 밀어낸다.</summary>
    bool SingleSlot { get; }

    /// <summary>모던 고딕 글꼴을 쓴다 (false면 게임 글꼴).</summary>
    bool UseModernFont { get; }

    /// <summary>사라지는 연출 길이 (초).</summary>
    float ExitSeconds { get; }

    /// <summary>방식에 맞게 부품을 켜고 모양을 잡는다 (알림이 새로 뜰 때, 글꼴·문구 폭이 정해진 뒤).</summary>
    void Prepare(CenterToastEntry entry, CenterToastSettings settings, CenterToastSprites sprites);

    /// <summary>한 프레임 배치. index = 0이 가장 새 것.</summary>
    void Render(CenterToastEntry entry, int index, CenterToastSettings settings);
}
