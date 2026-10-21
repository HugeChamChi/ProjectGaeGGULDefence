using System;
using System.Collections.Generic;

/// <summary>Run-local boss selection with explicit legacy opening and first-round shuffle policies.</summary>
internal sealed class EndlessBossSchedule
{
    private readonly int _cycleLength;
    private readonly EndlessBossSelection _selection;
    private readonly IEndlessRandom _random;
    private readonly Dictionary<int, (EndlessBossRole Role, BossData Boss)> _slots = new Dictionary<int, (EndlessBossRole, BossData)>();
    private readonly Dictionary<EndlessBossRole, BossData[]> _candidates = new Dictionary<EndlessBossRole, BossData[]>();
    private readonly Dictionary<EndlessBossRole, EndlessShuffleBag<BossData>> _bags = new Dictionary<EndlessBossRole, EndlessShuffleBag<BossData>>();
    private readonly Dictionary<EndlessBossRole, BossData> _previous = new Dictionary<EndlessBossRole, BossData>();
    private int _selectedRound;
    private BossData _selectedBoss;
    internal EndlessBossRole CurrentRole { get; private set; }

    internal EndlessBossSchedule(EndlessModeData mode, IEndlessRandom random, EndlessBalanceSnapshot balance = null)
    {
        _cycleLength = mode.CycleLength;
        _selection = mode.BossSelection;
        _random = random;
        foreach (var slot in mode.Slots) _slots.Add(slot.Position, (slot.Role, slot.Boss));
        if (balance != null)
        {
            var registry = new Dictionary<string, BossData>(StringComparer.Ordinal);
            foreach (var pool in new[] { mode.BreatherBossPool, mode.HardBossPool })
                foreach (var entry in pool.Entries) registry[entry.BossKey] = entry.Boss;
            foreach (var role in new[] { EndlessBossRole.Breather, EndlessBossRole.Hard })
            {
                var candidates = new List<BossData>();
                foreach (string key in balance.BossKeys(role)) candidates.Add(registry[key]);
                _candidates.Add(role, candidates.ToArray());
            }
            return;
        }
        if (mode.BossSelection == EndlessBossSelection.FixedCycle) return;
        var roles = new HashSet<EndlessBossRole>();
        foreach (var slot in mode.Slots)
            if (roles.Add(slot.Role))
                Capture(slot.Role == EndlessBossRole.Breather ? mode.BreatherBossPool : mode.HardBossPool);
    }

    private void Capture(EndlessBossPoolData pool)
    {
        if (pool == null) return;
        var candidates = new List<BossData>();
        foreach (var entry in pool.Entries) if (entry.Enabled) candidates.Add(entry.Boss);
        _candidates.Add(pool.Role, candidates.ToArray());
    }

    internal bool TrySelect(int round, out BossData boss, out string error)
    {
        boss = null;
        error = "";
        if (round == _selectedRound && round > 0) { boss = _selectedBoss; return true; }
        if (round < 1 || (long)round != (long)_selectedRound + 1)
        { error = "Boss selection must advance one round at a time."; return false; }
        var slot = _slots[(round - 1) % _cycleLength + 1];
        if (_selection == EndlessBossSelection.FixedCycle
            || (_selection == EndlessBossSelection.ShuffleAfterOpening && round <= _cycleLength))
            boss = slot.Boss;
        else
        {
            if (!_bags.TryGetValue(slot.Role, out var bag))
            {
                _previous.TryGetValue(slot.Role, out var lastFixedBoss);
                bag = new EndlessShuffleBag<BossData>(_candidates[slot.Role], _random, lastFixedBoss);
                _bags.Add(slot.Role, bag);
            }
            if (!bag.TryTake(out boss, out error)) return false;
        }
        _previous[slot.Role] = boss;
        _selectedRound = round;
        _selectedBoss = boss;
        CurrentRole = slot.Role;
        return true;
    }
}
