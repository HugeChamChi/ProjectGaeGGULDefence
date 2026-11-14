using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Single Deertree encounter clock. A host maps cells to real units or lab stand-ins.</summary>
public sealed class DeertreeEncounter
{
    public enum Pattern { None, Root, Fruit, Curse }
    private readonly DeertreeBossSettings _settings;
    private readonly System.Random _random;
    private readonly List<int> _candidates = new();
    private float _rootClock;
    private float _windup;
    private bool _fruitUsed, _curseUsed;
    private bool _fruitPending, _cursePending;
    /// <summary>Current normalized HP, including capped fruit healing.</summary>
    public float Hp { get; private set; }
    public Pattern Casting { get; private set; }
    public DeertreeCellState[] Cells { get; }
    public int[] Apples { get; } = new int[4];
    public bool FruitActive { get; private set; }
    public bool FruitEntering { get; private set; }
    public float FruitEntranceRemaining { get; private set; }
    public bool Returning { get; private set; }
    public float FruitRemaining { get; private set; }
    public float ReturnRemaining { get; private set; }
    public int HealedApples { get; private set; }
    /// <summary>Fruit and curse each own damage immunity and the attack gate for every combat actor, including totems,
    /// from their windup until the boss returns (user decision 2026-11-13: attacks also stop during the curse).</summary>
    public bool IsInvulnerable => FruitLocked || CurseLocked;
    private bool FruitLocked => Casting == Pattern.Fruit || FruitEntering || FruitActive || Returning;
    private bool CurseLocked => Casting == Pattern.Curse || CurseActive || CurseRecovering;
    /// <summary>The lower-threshold one-shot is the climax and wins when both are pending (currently fruit at 10%).</summary>
    private bool FruitFirst => _settings.FruitThreshold <= _settings.CurseThreshold;
    /// <summary>Host-wide gate, not just a unit-specific status.</summary>
    public bool CombatantsCanAttack => Hp > 0 && !IsInvulnerable;
    /// <summary>Curse owns the pattern scheduler until every timed binding resolves.</summary>
    public bool CurseActive { get; private set; }
    public bool CurseRecovering { get; private set; }
    private float _curseRecoveryRemaining;
    public event Action<Pattern> OnCast;
    public event Action<Pattern> OnImpact;
    public event Action OnChanged;

    /// <summary>Creates an encounter with deterministic randomness for a supplied seed.</summary>
    public DeertreeEncounter(DeertreeBossSettings settings, int cellCount, int seed)
    {
        _settings = settings;
        _random = new System.Random(seed);
        Cells = new DeertreeCellState[cellCount];
        for (int i = 0; i < cellCount; i++) Cells[i] = new DeertreeCellState();
        Reset();
    }

    /// <summary>Clears one-shots and all pending effects, including unfinished returns.</summary>
    public void Reset()
    {
        Hp = 1f; Casting = Pattern.None; _windup = 0; _rootClock = 0;
        _fruitUsed = _curseUsed = FruitActive = Returning = false;
        _fruitPending = _cursePending = false;
        CurseRecovering = CurseActive = FruitEntering = false; FruitEntranceRemaining = _curseRecoveryRemaining = 0;
        FruitRemaining = ReturnRemaining = 0; HealedApples = 0;
        Array.Clear(Apples, 0, Apples.Length);
        foreach (var cell in Cells) { cell.Tier = 0; cell.Blocked = false; cell.HasRoot = false; cell.RootBindTapsLeft = 0; cell.CurseTapsLeft = 0; cell.CurseRemaining = 0; }
        OnChanged?.Invoke();
    }

    /// <summary>Host placement path. Occupied or rooted cells reject placement.</summary>
    public bool PlaceUnit(int index, int tier)
    {
        var cell = Cells[index];
        if (cell.Tier != 0 || cell.HasRoot || tier < 1) return false;
        cell.Tier = tier; OnChanged?.Invoke(); return true;
    }

    /// <summary>Sets preview HP or applies the host's latest normalized HP observation.</summary>
    public void SetHp(float fraction)
    {
        if (Hp <= 0) return;
        if (IsInvulnerable) return;
        Hp = Mathf.Clamp01(fraction);
        if (!_settings.CurseOnlyPractice && Hp <= _settings.FruitThreshold && !_fruitUsed) _fruitPending = true;
        if (Hp <= _settings.CurseThreshold && !_curseUsed) _cursePending = true;
        if (Hp <= 0) ClearEffects();
        else if (Casting == Pattern.Root)
        {
            // The climax one-shot replaces a recurring root telegraph that is still winding up.
            if (FruitFirst && _fruitPending && !_fruitUsed) { _fruitUsed = true; Begin(Pattern.Fruit); }
            else if (!FruitFirst && _cursePending && !_curseUsed) { _curseUsed = true; Begin(Pattern.Curse); }
        }
        OnChanged?.Invoke();
    }

    private void ClearEffects()
    {
        Casting = Pattern.None; FruitActive = Returning = false;
        CurseRecovering = CurseActive = FruitEntering = false; FruitEntranceRemaining = _curseRecoveryRemaining = 0;
        FruitRemaining = ReturnRemaining = 0;
        Array.Clear(Apples, 0, Apples.Length);
        foreach (var cell in Cells) { cell.HasRoot = false; cell.RootBindTapsLeft = 0; cell.CurseTapsLeft = 0; cell.CurseRemaining = 0; }
    }

    /// <summary>Advances on the host combat clock; zero or paused time makes no progress.</summary>
    public void Advance(float delta)
    {
        if (delta <= 0 || Hp <= 0) return;
        // Substeps preserve trigger ordering when the host advances several seconds for verification.
        while (delta > 0f)
        {
            float step = Mathf.Min(delta, 1f / 60f); delta -= step;
            Tick(step);
        }
        OnChanged?.Invoke();
    }

    private void Tick(float delta)
    {
        // These two mechanics own the entire scheduler, including recurring cooldown progress.
        bool exclusive = IsInvulnerable || CurseActive || CurseRecovering || Casting == Pattern.Curse;
        if (CurseRecovering)
        {
            _curseRecoveryRemaining = Mathf.Max(0, _curseRecoveryRemaining - delta);
            if (_curseRecoveryRemaining <= 0) CurseRecovering = false;
        }
        foreach (var cell in Cells)
        {
            if (cell.CurseTapsLeft <= 0) continue;
            cell.CurseRemaining = Mathf.Max(0, cell.CurseRemaining - delta);
            if (cell.CurseRemaining > 0) continue;
            if (_settings.CurseOnlyPractice)
            {
                cell.CurseTapsLeft = _settings.CurseTaps;
                cell.CurseRemaining = _settings.CurseDuration;
                continue;
            }
            cell.CurseTapsLeft = 0; cell.Tier = Mathf.Max(0, cell.Tier - 1);
            if (cell.Tier == 0) cell.RootBindTapsLeft = 0;
        }
        if (CurseActive)
        {
            bool remaining = false;
            foreach (var cell in Cells) if (cell.CurseTapsLeft > 0) remaining = true;
            if (!remaining) StartCurseRecovery();
        }
        if (Returning)
        {
            ReturnRemaining = Mathf.Max(0, ReturnRemaining - delta);
            if (ReturnRemaining <= 0)
            {
                Returning = false; HealedApples = 0;
                foreach (int taps in Apples) if (taps > 0) HealedApples++;
                Hp = Mathf.Min(1, Hp + HealedApples * _settings.HealPerApple);
            }
        }
        if (FruitActive)
        {
            FruitRemaining = Mathf.Max(0, FruitRemaining - delta);
            if (FruitRemaining <= 0) { FruitActive = false; Returning = true; ReturnRemaining = _settings.BranchReturnDuration; }
        }
        if (FruitEntering)
        {
            FruitEntranceRemaining = Mathf.Max(0, FruitEntranceRemaining - delta);
            if (FruitEntranceRemaining <= 0) { FruitEntering = false; FruitActive = true; FruitRemaining = _settings.FruitDuration; }
        }
        if (!exclusive) _rootClock = Mathf.Min(_settings.RootCooldown, _rootClock + delta);
        if (Casting != Pattern.None)
        {
            _windup -= delta;
            if (_windup <= 0) Impact();
            return;
        }
        if (IsInvulnerable || CurseActive || CurseRecovering || exclusive) return;
        bool fruitReady = !_fruitUsed && _fruitPending, curseReady = !_curseUsed && _cursePending;
        if (fruitReady && (FruitFirst || !curseReady)) { _fruitUsed = true; Begin(Pattern.Fruit); }
        else if (curseReady) { _curseUsed = true; Begin(Pattern.Curse); }
        else if (!_settings.CurseOnlyPractice && _rootClock >= _settings.RootCooldown) { _rootClock = 0; Begin(Pattern.Root); }
    }

    private void Begin(Pattern pattern)
    {
        Casting = pattern;
        _windup = pattern == Pattern.Root ? _settings.RootWindup : pattern == Pattern.Fruit ? _settings.FruitWindup : _settings.CurseWindup;
        OnCast?.Invoke(pattern);
    }

    private void Impact()
    {
        var pattern = Casting; Casting = Pattern.None;
        if (pattern == Pattern.Root)
        {
            _candidates.Clear();
            for (int i = 0; i < Cells.Length; i++) if (Cells[i].Tier == 0 && !Cells[i].HasRoot && !Cells[i].Blocked) _candidates.Add(i);
            if (_candidates.Count > 0) Cells[_candidates[_random.Next(_candidates.Count)]].HasRoot = true;
            else
            {
                for (int i = 0; i < Cells.Length; i++) if (Cells[i].Tier > 0 && Cells[i].RootBindTapsLeft == 0) _candidates.Add(i);
                if (_candidates.Count > 0) Cells[_candidates[_random.Next(_candidates.Count)]].RootBindTapsLeft = _settings.CurseTaps;
            }
        }
        else if (pattern == Pattern.Fruit)
        {
            FruitEntering = true; FruitEntranceRemaining = _settings.BranchGrowDuration; FruitRemaining = _settings.FruitDuration;
            for (int i = 0; i < Apples.Length; i++) Apples[i] = _settings.AppleTaps;
        }
        else if (pattern == Pattern.Curse)
        {
            CurseActive = true;
            _candidates.Clear();
            for (int i = 0; i < Cells.Length; i++) if (Cells[i].Tier > 0) _candidates.Add(i);
            int count = Mathf.Min(_settings.CurseTargets, _candidates.Count);
            for (int i = 0; i < count; i++)
            {
                int pick = _random.Next(_candidates.Count); var cell = Cells[_candidates[pick]]; _candidates.RemoveAt(pick);
                cell.CurseTapsLeft = _settings.CurseTaps; cell.CurseRemaining = _settings.CurseDuration;
            }
        }
        OnImpact?.Invoke(pattern);
    }

    /// <summary>One pointer-down on one apple; taps after its return begins are rejected.</summary>
    public bool TapApple(int index)
    {
        if (!FruitActive || Apples[index] <= 0) return false;
        Apples[index]--; OnChanged?.Invoke(); return true;
    }

    /// <summary>One pointer-down on one bound unit.</summary>
    public bool TapCurse(int index)
    {
        var cell = Cells[index];
        if (cell.CurseTapsLeft <= 0 || cell.CurseRemaining <= 0) return false;
        cell.CurseTapsLeft--;
        if (cell.CurseTapsLeft == 0) cell.CurseRemaining = 0;
        if (CurseActive)
        {
            bool remaining = false;
            foreach (var other in Cells) if (other.CurseTapsLeft > 0) remaining = true;
            if (!remaining) StartCurseRecovery();
        }
        OnChanged?.Invoke(); return true;
    }

    /// <summary>One pointer-down releases both kinds of binding on this unit toward their own tap counts.</summary>
    public bool TapUnit(int index)
    {
        bool changed = TapCurse(index);
        if (Cells[index].RootBindTapsLeft > 0) { Cells[index].RootBindTapsLeft--; changed = true; OnChanged?.Invoke(); }
        return changed;
    }

    /// <summary>Root-only sale for zero currency. A unit sharing the cell is retained.</summary>
    public bool SellRoot(int index)
    {
        if (!Cells[index].HasRoot) return false;
        Cells[index].HasRoot = false; OnChanged?.Invoke(); return true;
    }

    /// <summary>Active-skill counter releases every binding and cancels a pending curse windup.</summary>
    public void UseActiveSkill()
    {
        if (Casting == Pattern.Curse || CurseActive)
        {
            Casting = Pattern.None;
            StartCurseRecovery();
        }
        foreach (var cell in Cells) { cell.RootBindTapsLeft = 0; cell.CurseTapsLeft = 0; cell.CurseRemaining = 0; }
        OnChanged?.Invoke();
    }

    private void StartCurseRecovery()
    {
        CurseActive = false; CurseRecovering = true;
        _curseRecoveryRemaining = _settings.CurseRecoveryDuration;
    }
}
