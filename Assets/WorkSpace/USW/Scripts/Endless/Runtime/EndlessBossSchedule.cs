using System;
using System.Collections.Generic;

/// <summary>Run-local boss selection. The first cycle is fixed; subsequent category turns consume independent pools.</summary>
internal sealed class EndlessBossSchedule
{
    private readonly EndlessModeData _mode;
    private readonly IEndlessRandom _random;
    private readonly Dictionary<int, EndlessBossSlot> _slots = new Dictionary<int, EndlessBossSlot>();
    private readonly Dictionary<EndlessBossRole, BossData[]> _candidates = new Dictionary<EndlessBossRole, BossData[]>();
    private readonly Dictionary<EndlessBossRole, EndlessShuffleBag<BossData>> _bags = new Dictionary<EndlessBossRole, EndlessShuffleBag<BossData>>();
    private readonly Dictionary<EndlessBossRole, BossData> _previous = new Dictionary<EndlessBossRole, BossData>();
    private int _selectedRound;
    private BossData _selectedBoss;

    internal EndlessBossSchedule(EndlessModeData mode, IEndlessRandom random)
    {
        _mode = mode;
        _random = random;
        foreach (var slot in mode.Slots) _slots.Add(slot.Position, slot);
        if (mode.BossSelection != EndlessBossSelection.ShuffleAfterOpening) return;
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
        var slot = _slots[(round - 1) % _mode.CycleLength + 1];
        if (_mode.BossSelection == EndlessBossSelection.FixedCycle || round <= _mode.CycleLength)
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
        return true;
    }
}
