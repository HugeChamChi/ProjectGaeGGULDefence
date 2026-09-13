using System.Collections.Generic;

/// <summary>SO 또는 향후 시트 공급 방식과 독립적인 카드 조회 경계.</summary>
public interface ILevelUpCatalog
{
    /// <summary>해당 풀의 중복 없는 카드 ID 목록. 없는 풀은 빈 목록을 반환한다.</summary>
    IReadOnlyList<int> GetCardIds(int poolId);

    /// <summary>카드 ID에 해당하는 정의를 조회한다.</summary>
    bool TryGetCard(int cardId, out LevelUpData card);
}
