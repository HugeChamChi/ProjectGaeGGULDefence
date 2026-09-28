using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>명시적인 계정/트리 주소를 사용하는 교체 가능한 저장 경계. 실패는 빈 저장과 구분한다.</summary>
public interface IResearchSaveStore
{
    /// <summary>저장이 없으면 빈 v1 데이터. 손상/미지원 버전은 예외로 보존한다.</summary>
    UniTask<ResearchSaveData> LoadAsync(ResearchSaveAddress address, CancellationToken cancellationToken);
    /// <summary>독립 스냅샷을 지정 계정에 저장한다. 현재 로그인 계정을 다시 조회하지 않는다.</summary>
    UniTask SaveAsync(ResearchSaveAddress address, ResearchSaveData data, CancellationToken cancellationToken);
}
