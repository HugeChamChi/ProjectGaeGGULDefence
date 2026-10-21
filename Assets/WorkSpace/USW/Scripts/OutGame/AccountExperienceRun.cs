using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

/// <summary>Scene-owned highest HP reduction ledger. Callback identity is captured once per encounter.</summary>
public sealed class AccountExperienceRun
{
    private sealed class BossProgress
    {
        public decimal InitialHp;
        public decimal HighestRatio;
    }

    private readonly PlayerDataController _player;
    private readonly AccountProgressionConfig _config;
    private readonly Dictionary<string, BossProgress> _bosses = new();
    private readonly HashSet<BossBase> _tracked = new();
    private AccountExperienceTicket _ticket;
    private bool _closed = true;
    private int _winExperience;
    private int _loseExperience;
    private bool _grantWin;
    private bool _grantLose;
    private AccountExperienceReceipt _receipt;

    /// <summary>Uses root account data and an authored SO, with no workbook access at runtime.</summary>
    public AccountExperienceRun(PlayerDataController player, AccountProgressionConfig config)
    { _player = player; _config = config; }

    /// <summary>Starts only production play; tutorial/labs never reserve a reward.</summary>
    public void Begin(bool production)
    {
        Cancel(); _bosses.Clear(); _tracked.Clear(); _receipt = null;
        if (!production) return;
        _config.Validate();
        _ticket = _player.BeginAccountExperienceRun();
        _closed = _ticket == null;
        _winExperience = _config.WinBossExperience; _loseExperience = _config.LoseBossExperience;
        _grantWin = _config.GrantOnWin; _grantLose = _config.GrantOnLose;
    }

    /// <summary>Wires the existing boss events before UI/death cleanup; remembers the initial quantized max HP.</summary>
    public void Track(BossBase boss)
    {
        if (!OwnsRun || boss == null || boss.MaxHp <= 0 || !_tracked.Add(boss)) return;
        var ticket = _ticket;
        string encounter = Guid.NewGuid().ToString("N");
        _bosses.Add(encounter, new BossProgress { InitialHp = boss.MaxHp });
        boss.OnHpChanged += (current, _) => Observe(ticket.RunId, encounter, current);
        boss.OnDeath += () => Observe(ticket.RunId, encounter, 0);
        Observe(ticket.RunId, encounter, boss.CurrentHp);
    }

    private bool OwnsRun => !_closed && _ticket != null && _player.OwnsAccountExperienceRun(_ticket);

    private void Observe(string runId, string encounter, decimal currentHp)
    {
        if (!OwnsRun || _ticket.RunId != runId || !_bosses.TryGetValue(encounter, out var progress)) return;
        decimal ratio = currentHp <= 0 ? 1m : currentHp >= progress.InitialHp ? 0m : 1m - currentHp / progress.InitialHp;
        progress.HighestRatio = Math.Max(progress.HighestRatio, ratio);
    }

    /// <summary>Freezes before StopRun/cleanup; floors the sum once and journals the actual retained grant.</summary>
    public AccountExperienceReceipt Finish(bool win)
    {
        if (_closed) return _receipt;
        bool owned = OwnsRun;
        _closed = true;
        if (!owned) return null;
        decimal sum = 0;
        if (win ? _grantWin : _grantLose)
            foreach (var boss in _bosses.Values) sum += boss.HighestRatio * (win ? _winExperience : _loseExperience);
        int grant = (int)Math.Min(int.MaxValue, decimal.Floor(sum));
        if (_player.TryApplyAccountExperience(_ticket, _config, grant, out var receipt)) _receipt = receipt;
        return _receipt;
    }

    /// <summary>Retries the owning controller's absolute snapshot, never recalculating a reward.</summary>
    public UniTask<bool> SaveAsync() => _player.TrySaveAsync();

    /// <summary>Voluntary exit, scene destruction or configuration faults grant nothing.</summary>
    public void Cancel() { _closed = true; _ticket = null; }
}
