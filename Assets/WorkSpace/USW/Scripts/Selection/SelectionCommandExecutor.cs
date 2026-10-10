using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>확정된 새 획득의 즉시 명령을 실행한다. 지속 효과 재계산에는 참여하지 않는다.</summary>
public sealed class SelectionCommandExecutor
{
    private readonly CurrencyManager _currency;
    private readonly UpgradeManager _upgrade;
    private readonly GridManager _grid;
    private readonly UnitFactory _factory;
    private readonly UnitSpawner _spawner;
    private readonly GameManager _game;
    private readonly ISelectionRandom _random;
    /// <summary>기존 씬 소유 시스템과 난수 공급자를 받는다.</summary>
    public SelectionCommandExecutor(CurrencyManager currency, UpgradeManager upgrade, GridManager grid,
        UnitFactory factory, UnitSpawner spawner, GameManager game, ISelectionRandom random)
    {
        _currency=currency; _upgrade=upgrade; _grid=grid; _factory=factory; _spawner=spawner; _game=game; _random=random;
    }
    /// <summary>명령별 실패를 기록하되 자동 재실행하지 않는다. 보장/재추첨은 상태·선택 결과에서 처리한다.</summary>
    public void Execute(SelectionCardSnapshot card, Action requestTotem, CancellationToken token)
    {
        foreach (var command in card.CopyDefinition().Commands)
        {
            if (token.IsCancellationRequested || _game?.IsFinished == true) return;
            try
            {
                switch (command)
                {
                    case GiveFoodCommandDefinition d: _currency?.AddCurrency(d.Amount); break;
                    case RefundUpgradeCommandDefinition d: _upgrade?.RefundSelectionOnce(card.CardId, d.Ratio); break;
                    case RequestTotemCommandDefinition _: requestTotem?.Invoke(); break;
                    case GainRandomUnitCommandDefinition _: SpawnAsync(token).Forget(); break;
                    case GuaranteeLegendCommandDefinition _: break;
                    case RerollChoicesCommandDefinition _: break;
                    default: throw new ArgumentException("Unsupported selection command.");
                }
            }
            catch (Exception error) { Debug.LogException(error); }
        }
    }
    private async UniTaskVoid SpawnAsync(CancellationToken token)
    {
        try
        {
            await UniTask.Yield(token);
            if (_game?.IsFinished == true) return;
            var cells = _grid?.GetEmptyCells();
            if (cells == null || cells.Count == 0) { Debug.LogWarning("[LevelUp] 빈 셀 없음 — 기물 획득 취소"); return; }
            if (_factory == null || _spawner == null) return;
            var cell = cells[_random.Range(0, cells.Count)];
            var tier = (Tier)_random.Range((int)Tier.Normal, (int)Tier.Rare + 1);
            var unit = _factory.CreateRandomUnitOfTier(tier);
            if (unit != null) _spawner.PlaceUnitWithEffect(unit, cell);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { Debug.LogException(error); }
    }
}
