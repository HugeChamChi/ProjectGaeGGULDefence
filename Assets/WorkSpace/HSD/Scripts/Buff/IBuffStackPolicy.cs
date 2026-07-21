/// <summary>
/// 동일 버프가 재적용(Reapply)될 때 스택 수와 지속시간을 어떻게 갱신할지 정의하는 정책.
/// </summary>
public interface IBuffStackPolicy
{
    /// <summary>재적용 시 현재 스택 수를 바탕으로 새 스택 수를 계산한다 (Clamp 적용 전 값).</summary>
    int OnReapply(int currentStack);

    /// <summary>재적용 시 만료 시각(지속시간)을 처음부터 다시 갱신할지 여부.</summary>
    bool RefreshDurationOnReapply { get; }

    /// <summary>정책이 허용하는 범위로 스택 수를 제한한다.</summary>
    int Clamp(int stack);
}
