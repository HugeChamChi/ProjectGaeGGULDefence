using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// In-game Deertree. Hosts the same <see cref="DeertreeEncounter"/> the lab verified and maps its cells to the real grid:
/// a root seals an empty cell, a binding stuns the unit until tapped away, an expired curse demotes the unit one tier.
/// Fruit and curse make the boss untargetable from windup to return, which stops every attacker (user decision 2026-11-13).
/// The encounter owns scheduling, so the boss registers no <see cref="BossPatternData"/>; the scene pattern controller only
/// supplies the combat clock. Fight numbers come from the BossPatterns chart through a runtime copy of the settings asset.
/// </summary>
[RequireComponent(typeof(DeertreeBossPresentation))]
public sealed class BossDeertree : BossBase, IBossPatternRuntime, IBossUnitSpawnerUser
{
    /// <summary>A binding has no timer of its own on the unit; the encounter releases it.</summary>
    private const float BindStunSeconds = 100000f;
    private const float CellEdgeTolerance = .05f;

    [SerializeField] private DeertreeBossSettings _settings;
    private DeertreeBossSettings _runtime;
    private DeertreeBossPresentation _presentation;
    private DeertreeEncounter _encounter;
    private GridManager _grid;
    private UnitSpawner _spawner;
    private readonly List<GridCell> _cells = new();
    private UnitBase[] _bound;
    private bool[] _rooted;
    private int[] _tiers;
    private int _selectedRoot = -1;
    private bool _fruitHealPending;
    private bool _chiefHooked;

    /// <summary>Fruit and curse exclude new targeting and all incoming damage until the boss has returned.</summary>
    public override bool IsTargetable => base.IsTargetable && (_encounter == null || !_encounter.IsInvulnerable);
    /// <summary>Current encounter state for checks and presentation; null before registration and after cleanup.</summary>
    public DeertreeEncounter Encounter => _encounter;
    /// <summary>Settings in use: the asset, or a copy carrying the chart numbers.</summary>
    public DeertreeBossSettings RuntimeSettings => _runtime;
    /// <summary>Grid cells in encounter index order.</summary>
    public IReadOnlyList<GridCell> Cells => _cells;
    /// <summary>Root cell whose sale badge is showing, or -1.</summary>
    public int SelectedRoot => _selectedRoot;

    /// <inheritdoc/>
    public void ConfigureUnitSpawner(UnitSpawner spawner) => _spawner = spawner;

    /// <inheritdoc/>
    public void ConfigurePatternContext(GridManager grid)
    {
        ClearPatternEffects();
        _grid = grid;
        _presentation = GetComponent<DeertreeBossPresentation>();
        if (_settings == null || grid == null) return;
        _runtime = GGD.BackendCharts.BossPatterns.RuntimeCopy(_settings);
        _cells.Clear();
        foreach (var cell in grid.AllCells()) if (cell != null) _cells.Add(cell);
        _bound = new UnitBase[_cells.Count];
        _rooted = new bool[_cells.Count];
        _tiers = new int[_cells.Count];
        _encounter = new DeertreeEncounter(_runtime, _cells.Count, Random.Range(int.MinValue, int.MaxValue));
        _encounter.OnCast += _presentation.Cast;
        _encounter.OnImpact += HandleImpact;
        OnHpChanged += HandleHp;
        ChiefActiveSkillSignals.OnActivated += HandleChiefSkill;
        _chiefHooked = true;
        _presentation.Begin(this, _runtime, _encounter);
    }

    /// <summary>The encounter schedules its own patterns; the controller never selects one for this boss.</summary>
    public override void ExecutePattern(BossPatternData patternData) { }

    private void HandleHp(decimal current, decimal maximum)
    {
        if (_encounter != null && maximum > 0) _encounter.SetHp((float)(current / maximum));
    }

    private void HandleImpact(DeertreeEncounter.Pattern pattern)
    {
        if (pattern == DeertreeEncounter.Pattern.Fruit) _fruitHealPending = true;
        _presentation.Impact(pattern);
    }

    private void HandleChiefSkill()
    {
        if (_encounter == null || IsDead) return;
        _encounter.UseActiveSkill();
        ApplyToGrid();
    }

    /// <inheritdoc/>
    public void AdvancePatternEffects(float deltaTime)
    {
        if (_encounter == null || deltaTime <= 0f) return;
        if (IsDead) { ClearPatternEffects(); return; }
        SyncFromGrid();
        _encounter.Advance(deltaTime);
        if (_fruitHealPending && !_encounter.FruitEntering && !_encounter.FruitActive && !_encounter.Returning)
        {
            // The encounter already counted this heal in its own HP; the HP event keeps both in step.
            _fruitHealPending = false;
            if (_encounter.HealedApples > 0) Heal(MaxHp * (decimal)(_encounter.HealedApples * _runtime.HealPerApple));
        }
        ApplyToGrid();
        _presentation.Sample(deltaTime);
    }

    // Grid -> encounter: who stands where. A unit that left its cell takes its bindings with it.
    private void SyncFromGrid()
    {
        for (int i = 0; i < _cells.Count; i++)
        {
            var cell = _cells[i];
            var state = _encounter.Cells[i];
            var unit = cell != null ? cell.OccupyingUnit : null;
            int tier = unit != null ? (int)unit.OriginalTier + 1 : 0;
            state.Blocked = cell == null || cell.OccupyingTotem != null || cell.Model.IsPermanentlySealed;
            if (tier == 0) { state.CurseTapsLeft = 0; state.CurseRemaining = 0f; state.RootBindTapsLeft = 0; }
            state.Tier = tier;
            _tiers[i] = tier;
        }
    }

    // Encounter -> grid: demotions, root seals and unit bindings.
    private void ApplyToGrid()
    {
        if (_encounter == null) return;
        for (int i = 0; i < _cells.Count; i++)
        {
            var cell = _cells[i];
            if (cell == null) continue;
            var state = _encounter.Cells[i];
            var unit = cell.OccupyingUnit;
            if (state.Tier < _tiers[i] && unit != null)
            {
                // The curse ran out on this unit: one tier down, a Normal unit is removed.
                ReleaseBinding(i);
                if (_spawner != null) _spawner.DemoteUnit(unit);
                else Debug.LogWarning("[BossDeertree] No UnitSpawner in this scene; curse demotion skipped.", this);
                _tiers[i] = state.Tier;
                unit = cell.OccupyingUnit;
            }
            if (state.HasRoot != _rooted[i])
            {
                _rooted[i] = state.HasRoot;
                cell.Model.SetSealed(state.HasRoot);
                if (!state.HasRoot && _selectedRoot == i) _selectedRoot = -1;
            }
            bool bound = state.CurseTapsLeft > 0 || state.RootBindTapsLeft > 0;
            if (!bound || unit == null) { ReleaseBinding(i); continue; }
            if (_bound[i] != unit) ReleaseBinding(i);
            // A replacement that is still playing its spawn effect cannot be stunned yet; retry next tick.
            if (!unit.IsStunned) unit.ApplyStun(BindStunSeconds);
            if (!unit.IsStunned) continue;
            _bound[i] = unit;
            unit.TapCaptured = true;
        }
    }

    private void ReleaseBinding(int index)
    {
        var unit = _bound[index];
        _bound[index] = null;
        if (unit == null) return;
        unit.TapCaptured = false;
        unit.ClearStun();
    }

    /// <summary>
    /// One finger down on the field while combat runs: an apple, a bound unit, a root (select) or its sale badge.
    /// Returns true when the tap did something.
    /// </summary>
    public bool HandlePointerDown(Vector2 screenPosition, Vector2 worldPosition)
    {
        if (_encounter == null || IsDead) return false;
        if (_encounter.FruitActive && _presentation.TryFindApple(screenPosition, out int apple) && _encounter.TapApple(apple))
        {
            _presentation.FlashApple(apple);
            return true;
        }
        if (_selectedRoot >= 0 && _presentation.SellBadgeContains(worldPosition))
        {
            _encounter.SellRoot(_selectedRoot);
            _selectedRoot = -1;
            ApplyToGrid();
            return true;
        }
        var cell = _grid != null ? _grid.FindInteractionCell(worldPosition, CellEdgeTolerance) : null;
        int index = cell != null ? _cells.IndexOf(cell) : -1;
        if (index < 0) { _selectedRoot = -1; return false; }
        if (_encounter.TapUnit(index))
        {
            _presentation.FlashBinding(index);
            ApplyToGrid();
            return true;
        }
        _selectedRoot = _encounter.Cells[index].HasRoot ? index : -1;
        return _selectedRoot >= 0;
    }

    /// <inheritdoc/>
    public void ClearPatternEffects()
    {
        if (_chiefHooked) ChiefActiveSkillSignals.OnActivated -= HandleChiefSkill;
        _chiefHooked = false;
        OnHpChanged -= HandleHp;
        if (_encounter != null)
            for (int i = 0; i < _cells.Count; i++)
            {
                ReleaseBinding(i);
                if (_rooted[i] && _cells[i] != null) _cells[i].Model.SetSealed(false);
                _rooted[i] = false;
            }
        _encounter = null;
        _selectedRoot = -1;
        _fruitHealPending = false;
        if (_presentation != null) _presentation.End();
    }

    private void OnDisable() => ClearPatternEffects();
}
