/// <summary>툴팁·검증 도구·런타임이 공유하는 읽기 전용 디버프 설정 조회.</summary>
public interface IDebuffSource
{
    /// <summary>현재 조회 등급/대상의 설정. 설정이 없으면 false.</summary>
    bool TryGetDebuffBinding(out DebuffBinding binding);
}
