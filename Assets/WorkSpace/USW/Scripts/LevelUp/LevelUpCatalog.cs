using System;
using System.Collections.Generic;

/// <summary>SO 목록으로 구성하는 조회 스냅샷. DI, 게임 매니저, 공유 SO 변경에 의존하지 않는다.</summary>
public sealed class LevelUpCatalog : ILevelUpCatalog
{
    private readonly Dictionary<int, LevelUpData> _cards = new();
    private readonly Dictionary<int, IReadOnlyList<int>> _pools = new();

    /// <summary>풀과 카드 참조를 검증한다. 같은 ID의 서로 다른 정의는 설정 오류로 거부한다.</summary>
    public LevelUpCatalog(IEnumerable<LevelUpPoolData> pools)
    {
        if (pools == null) throw new ArgumentNullException(nameof(pools));
        foreach (var pool in pools)
        {
            if (pool == null) throw new ArgumentException("족장 레벨업 풀이 지정되지 않았습니다.");
            if (_pools.ContainsKey(pool.PoolId))
                throw new ArgumentException($"레벨업 풀 ID 중복: {pool.PoolId}");

            var ids = new List<int>();
            var seen = new HashSet<int>();
            foreach (var card in pool.Cards ?? Array.Empty<LevelUpData>())
            {
                if (card == null) throw new ArgumentException($"풀 {pool.PoolId}: 누락된 카드 참조");
                if (_cards.TryGetValue(card.chooseId, out var existing) && existing != card)
                    throw new ArgumentException($"서로 다른 카드 SO의 chooseId 중복: {card.chooseId}");
                _cards[card.chooseId] = card;
                if (seen.Add(card.chooseId)) ids.Add(card.chooseId);
            }
            _pools.Add(pool.PoolId, ids.AsReadOnly());
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<int> GetCardIds(int poolId)
        => _pools.TryGetValue(poolId, out var ids) ? ids : Array.Empty<int>();

    /// <inheritdoc />
    public bool TryGetCard(int cardId, out LevelUpData card) => _cards.TryGetValue(cardId, out card);
}
