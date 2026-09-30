/// <summary>
/// 선택 화면 일시정지(timeScale 0) 중 대기 모션을 유지하는 외형 컴포넌트 (시각 전용, 전투 로직 무관).
/// FieldPauseVisuals가 유닛·보스 하위의 모든 구현체를 찾아 켜고 끈다 — 외형 방식(Animator / Spine / 트윈)이 바뀌어도
/// 새 외형 컴포넌트가 이 인터페이스만 구현하면 된다 (예: Unit/SpinePauseIdleVisual).
/// </summary>
public interface IPauseIdleVisual
{
    /// <summary>on = 실제 시간으로 대기 모션 재생, off = 원래 시간 설정으로 복구. 여러 번 불려도 안전해야 한다.</summary>
    void SetPauseIdle(bool on);
}
