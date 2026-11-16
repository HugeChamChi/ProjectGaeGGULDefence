using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 결과에서 로비로 넘어갈 때 받은 보상을 로비 연출(LobbyRewardFlight)에 한 번 넘긴다.
/// 지급은 이미 끝난 값이며 이 목록은 화면 연출만 한다.
/// </summary>
public static class BattleRewardHandoff
{
    /// <summary>로비 상단의 도착 자리.</summary>
    public enum Target { Experience, Gold, Essence }

    /// <summary>날아갈 보상 한 종류.</summary>
    public readonly struct Entry
    {
        public readonly Target Target;
        public readonly Sprite Icon;
        public readonly int Amount;
        public Entry(Target target, Sprite icon, int amount) { Target = target; Icon = icon; Amount = amount; }
    }

    private static readonly List<Entry> _pending = new();

    /// <summary>로비로 가기 직전에 이번 판 보상을 맡긴다(이전 값은 버린다).</summary>
    public static void Set(IEnumerable<Entry> entries)
    {
        _pending.Clear();
        if (entries == null) return;
        foreach (var entry in entries)
            if (entry.Amount > 0 && entry.Icon != null) _pending.Add(entry);
    }

    /// <summary>맡긴 보상을 꺼내고 비운다.</summary>
    public static Entry[] Take()
    {
        var result = _pending.ToArray();
        _pending.Clear();
        return result;
    }

    /// <summary>맡긴 보상이 있는지.</summary>
    public static bool HasPending => _pending.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetDomain() => _pending.Clear();
}
