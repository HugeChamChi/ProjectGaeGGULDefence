using System;
using System.Collections.Generic;

/// <summary>
/// 강화 진행 상황 (UI와 무관한 순수 로직). 트리 하나를 담당한다.
/// 노드 레벨, 해금 여부, 강화, 추천 노드, 택1 선택 바꾸기, 스탯 합계를 담당한다.
/// 인게임 연동 시 GetTotal(stat)로 보너스를 읽는다.
/// </summary>
public sealed class ResearchProgress
{
    private readonly ResearchTreeData _tree;
    private readonly Dictionary<string, int> _levels;

    /// <summary>어떤 노드의 레벨이 바뀌었을 때 (여러 노드가 한꺼번에 바뀌면 null).</summary>
    public event Action<ResearchNodeData> OnChanged;

    /// <summary>이미 로드한 진행을 복사한다. 저장/네트워크 수명은 ResearchSaveSession이 담당한다.</summary>
    public ResearchProgress(ResearchTreeData tree, IReadOnlyDictionary<string, int> levels)
    {
        _tree = tree;
        _levels = new Dictionary<string, int>();
        if (levels != null) foreach (var pair in levels) _levels.Add(pair.Key, pair.Value);
    }

    /// <summary>담당 트리.</summary>
    public ResearchTreeData Tree => _tree;

    /// <summary>현재 레벨 (0 = 아직 안 올림).</summary>
    public int GetLevel(ResearchNodeData node) =>
        node != null && _levels.TryGetValue(node.Id, out int level) ? Math.Min(level, node.MaxLevel) : 0;

    /// <summary>트리 전체 강화 횟수 (모든 노드 레벨 합). 관문 조건에 쓴다.</summary>
    public int TotalLevel
    {
        get
        {
            int total = 0;
            foreach (var node in _tree.Nodes) total += GetLevel(node);
            return total;
        }
    }

    /// <summary>노드 상태.</summary>
    public ResearchNodeState GetState(ResearchNodeData node)
    {
        if (node == null) return ResearchNodeState.Locked;
        if (GetLevel(node) >= node.MaxLevel) return ResearchNodeState.Maxed;
        if (IsBlockedByChoice(node)) return ResearchNodeState.Blocked;
        return IsUnlocked(node) ? ResearchNodeState.Available : ResearchNodeState.Locked;
    }

    /// <summary>부모 조건과 전체 강화 횟수 조건을 채웠는지 (택1 막힘은 따로 본다).</summary>
    public bool IsUnlocked(ResearchNodeData node)
    {
        if (node.RequiredTotalLevel > 0 && TotalLevel < node.RequiredTotalLevel) return false;
        if (node.Parents.Count == 0) return true;
        bool any = false, all = true;
        foreach (string parentId in node.Parents)
        {
            var parent = _tree.Find(parentId);
            bool maxed = parent != null && GetLevel(parent) >= parent.MaxLevel;
            any |= maxed;
            all &= maxed;
        }
        return node.RequireAnyParent ? any : all;
    }

    /// <summary>택1 그룹에서 다른 노드를 이미 골랐는지 (레벨 1 이상).</summary>
    public bool IsBlockedByChoice(ResearchNodeData node)
    {
        if (string.IsNullOrEmpty(node.ExclusiveGroup) || GetLevel(node) > 0) return false;
        foreach (var other in _tree.Nodes)
            if (other != null && other != node && other.ExclusiveGroup == node.ExclusiveGroup && GetLevel(other) > 0) return true;
        return false;
    }

    /// <summary>지금 한 단계 올릴 수 있는지.</summary>
    public bool CanUpgrade(ResearchNodeData node) => node != null && _tree.Find(node.Id) == node && GetState(node) == ResearchNodeState.Available;

    /// <summary>한 단계 올리고 저장한다. 못 올리면 false.</summary>
    public bool TryUpgrade(ResearchNodeData node)
    {
        if (!CanUpgrade(node)) return false;
        _levels[node.Id] = GetLevel(node) + 1;
        OnChanged?.Invoke(node);
        return true;
    }

    /// <summary>
    /// 추천 노드: 지금 올릴 수 있는 노드 중 RecommendOrder가 가장 작은 것.
    /// 추천 순서가 붙은 노드가 없으면 가장 아래(Row가 작은) 노드. 올릴 게 없으면 null.
    /// </summary>
    public ResearchNodeData GetRecommended()
    {
        ResearchNodeData best = null;
        foreach (var node in _tree.Nodes)
        {
            if (node == null || !CanUpgrade(node)) continue;
            if (best == null || Rank(node) < Rank(best)) best = node;
        }
        return best;

        static long Rank(ResearchNodeData n) => n.RecommendOrder > 0 ? n.RecommendOrder : 100_000L + n.Row * 10 + (n.Column + 1);
    }

    /// <summary>
    /// 택1 선택 바꾸기: 이 노드 그룹에서 이미 고른 노드와 그 뒤로 이어진 노드의 레벨을 모두 0으로 되돌린다.
    /// 되돌린 노드가 있으면 true.
    /// </summary>
    public bool ResetChoice(ResearchNodeData node)
    {
        if (node == null || string.IsNullOrEmpty(node.ExclusiveGroup)) return false;
        var toReset = new HashSet<string>();
        foreach (var other in _tree.Nodes)
            if (other != null && other.ExclusiveGroup == node.ExclusiveGroup && GetLevel(other) > 0)
                CollectDescendants(other.Id, toReset);
        if (toReset.Count == 0) return false;
        foreach (string id in toReset) _levels.Remove(id);
        OnChanged?.Invoke(null);
        return true;
    }

    // 자기 자신 + 이 노드를 부모로 둔 노드들을 끝까지 따라간다.
    private void CollectDescendants(string id, HashSet<string> result)
    {
        if (!result.Add(id)) return;
        foreach (var child in _tree.Nodes)
            if (child != null && child.Parents.Contains(id)) CollectDescendants(child.Id, result);
    }

    /// <summary>해당 레벨에서의 노드 수치.</summary>
    public static float GetValue(ResearchNodeData node, int level) => node.ValuePerLevel * level;

    /// <summary>현재 레벨 보너스 합계. 퍼센트 스탯은 비율(0.02=2%), 고정 스탯은 원 단위이다.</summary>
    public float GetTotal(ResearchStat stat)
    {
        float total = 0f;
        foreach (var node in _tree.Nodes)
            if (node != null && node.Stat == stat) total += GetValue(node, GetLevel(node));
        return total;
    }

    /// <summary>모든 레벨을 0으로 되돌리고 저장한다 (테스트용).</summary>
    public void ResetAll()
    {
        _levels.Clear();
        OnChanged?.Invoke(null);
    }

    /// <summary>현재 진행을 변경 가능한 메모리에서 분리하여 캡처한다.</summary>
    public ResearchSaveData Capture() => ResearchSaveData.Capture(_tree.TreeKey, _levels);
}
