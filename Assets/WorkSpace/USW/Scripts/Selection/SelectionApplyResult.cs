/// <summary>카드 선택 요청 결과. 대기 요청은 재진입 처리 후 확정되며 UI 재추첨을 즉시 시작하지 않는다.</summary>
public enum SelectionApplyResult
{
    /// <summary>무효/중복/런 종료로 거절됨.</summary>
    Rejected,
    /// <summary>현재 트랜잭션 뒤에 처리 예약됨.</summary>
    Queued,
    /// <summary>획득 확정됨.</summary>
    Applied,
    /// <summary>획득 확정 후 새 선택지를 요청함.</summary>
    Reroll
}
