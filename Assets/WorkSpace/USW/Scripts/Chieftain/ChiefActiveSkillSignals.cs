using System;

/// <summary>
/// 족장 액티브 스킬 발동 알림. 스킬 구현(알팡/유닛 족장)과 보스 패턴 카운터를 서로 모르게 연결한다.
/// 구독자는 씬 수명 동안만 구독하고 파괴 시 해제한다.
/// </summary>
public static class ChiefActiveSkillSignals
{
    /// <summary>족장 액티브 스킬이 실제로 발동된 순간 (TryActivate 성공).</summary>
    public static event Action OnActivated;

    /// <summary>스킬 구현이 발동 성공 직후 호출한다.</summary>
    public static void RaiseActivated() => OnActivated?.Invoke();
}
