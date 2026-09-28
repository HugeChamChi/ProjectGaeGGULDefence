using System;
using System.Collections.Generic;

/// <summary>계정/트리별 세션을 앱 수명으로 보관한다. A의 저장 완료는 B 세션에 접근하지 않는다.</summary>
public sealed class ResearchSaveService : IDisposable
{
    private readonly IResearchSaveStore _store;
    private readonly Dictionary<string, ResearchSaveSession> _sessions = new(StringComparer.Ordinal);
    /// <summary>저장 구현을 생성자 주입한다.</summary>
    public ResearchSaveService(IResearchSaveStore store) => _store = store;
    /// <summary>호출 시 고정된 계정으로 진행을 얻는다. dirty 상태는 화면/계정 전환 후에도 유지한다.</summary>
    public ResearchSaveSession GetSession(string accountKey, ResearchTreeData tree, string legacyPrefix)
    {
        if (tree == null) throw new ArgumentNullException(nameof(tree));
        var address = new ResearchSaveAddress(accountKey, tree.TreeKey,
            string.IsNullOrEmpty(tree.LegacySaveName) ? null : legacyPrefix + tree.LegacySaveName);
        if (!_sessions.TryGetValue(address.StorageKey, out var session))
            _sessions.Add(address.StorageKey, session = new ResearchSaveSession(_store, address, tree));
        else if (session.Progress != null && session.Progress.Tree != tree)
            throw new InvalidOperationException("서로 다른 트리에 같은 TreeKey가 지정되었습니다: " + tree.TreeKey);
        return session;
    }
    /// <summary>루트 컨테이너가 종료될 때 모든 계정의 작업을 취소한다.</summary>
    public void Dispose()
    {
        foreach (var session in _sessions.Values) session.Dispose();
        _sessions.Clear();
    }
}
