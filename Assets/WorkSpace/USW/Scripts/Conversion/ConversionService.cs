using GGD.BackendCharts;
using UnityEngine;

/// <summary>
/// Converts a placed unit into a random different kind of the same tier for conversion stones.
/// Removal and placement use the same paths as selling and merging (OnRemoved, RemoveUnit, PlaceUnitWithEffect).
/// </summary>
public sealed class ConversionService
{
    private readonly ConversionStoneWallet _wallet;
    private readonly UnitFactory _factory;
    private readonly UnitSpawner _spawner;
    private readonly MergeManager _merge;
    private readonly GameManager _game;

    public ConversionService(ConversionStoneWallet wallet, UnitFactory factory, UnitSpawner spawner, MergeManager merge, GameManager game)
    { _wallet = wallet; _factory = factory; _spawner = spawner; _merge = merge; _game = game; }

    /// <summary>Whether the unit can show the conversion button at all (placed combat unit outside the tutorial).</summary>
    public bool Offers(UnitBase unit) =>
        unit != null && !unit.IsWildcardMergeUnit && unit.currentCell != null && unit.currentCell.OccupyingUnit == unit &&
        !BalanceChart.TutorialRunning && Cost(unit) > 0;

    /// <summary>Stones this unit costs (tier number times the per-tier value).</summary>
    public int Cost(UnitBase unit) => unit == null ? -1 : ConversionRules.Cost(_game != null ? _game.Config : null, unit.OriginalTier);

    /// <summary>Checks every condition; <paramref name="reason"/> is a short player-facing message when false.</summary>
    public bool CanConvert(UnitBase unit, out string reason)
    {
        reason = "";
        if (!Offers(unit)) { reason = "변환할 수 없는 유닛입니다."; return false; }
        if (_game == null || _game.CurrentState != GameManager.GameState.Playing || _game.IsTimeVaultActive)
        { reason = "지금은 변환할 수 없습니다."; return false; }
        if (unit.IsStunned) { reason = "묶인 유닛은 변환할 수 없습니다."; return false; }
        if (unit.currentCell.Model.IsSealed) { reason = "봉인된 칸의 유닛은 변환할 수 없습니다."; return false; }
        if (!_factory.HasOtherKind(unit.OriginalData)) { reason = "바꿀 수 있는 유닛이 없습니다."; return false; }
        if (_wallet.Stones < Cost(unit)) { reason = "변환석이 부족합니다."; return false; }
        return true;
    }

    /// <summary>Spends the stones and replaces the unit in its cell. Nothing is spent when creation fails.</summary>
    public bool TryConvert(UnitBase unit, out string reason)
    {
        if (!CanConvert(unit, out reason)) return false;
        var cell = unit.currentCell;
        var created = _factory.CreateRandomUnitOfTierExcluding(unit.OriginalTier, unit.OriginalData);
        if (created == null) { reason = "바꿀 수 있는 유닛이 없습니다."; return false; }
        if (!_wallet.TrySpend(Cost(unit)))
        {
            Object.Destroy(created.gameObject);
            reason = "변환석이 부족합니다.";
            return false;
        }

        _merge.ClearSelection();
        unit.RestoreOriginalTier();
        unit.OnRemoved();
        cell.RemoveUnit();
        Object.Destroy(unit.gameObject);
        _spawner.PlaceUnitWithEffect(created, cell, cell.transform.position);
        return true;
    }
}
