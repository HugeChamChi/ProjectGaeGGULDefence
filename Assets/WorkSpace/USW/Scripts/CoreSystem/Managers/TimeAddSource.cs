/// <summary>
/// 남은 시간이 늘어난 이유 — 표시 연출을 고르는 데만 쓴다 (실제 시간 계산은 출처와 무관).
/// 새 값은 뒤에 추가하고 기존 번호를 바꾸지 않는다.
/// </summary>
public enum TimeAddSource
{
    /// <summary>일반 (보스 처치 보너스 등) — 보스 처치 합체 연출.</summary>
    Default = 0,
    /// <summary>유닛이 타이머를 해킹해 되돌림 (디시그망 스킬) — 해킹 글리치 연출.</summary>
    Hack = 1,
}
