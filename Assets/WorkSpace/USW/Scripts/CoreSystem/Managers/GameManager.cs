using UnityEngine;
using System;

// ════════════════════════════════════════════════════════
// GameManager — InGameSingleton 교체
// ════════════════════════════════════════════════════════
public class GameManager : InGameSingleton<GameManager>
{
    public enum GameState { Idle, Playing, LevelUp, Win, Lose }
    public GameState CurrentState { get; private set; } = GameState.Idle;

    [SerializeField] private GameConfig config;

    public void OnStartButtonPressed()
    {
        if (CurrentState != GameState.Idle) return;
        if (config == null) { Debug.LogError("GameManager: config 미연결"); return; }

        CurrentState = GameState.Playing;

        Manager.UI.HideStartButton();
        Manager.Wave.StartWave();

        Manager.Timer.OnTimeUp += HandleTimeUp;
        Manager.Timer.StartTimer(config.countdownSeconds);

        Manager.Currency.AddCurrency(config.startingFood);
        Manager.Exp.OnLevelUp += HandleLevelUp;
    }

    private void HandleLevelUp()
    {
        if (CurrentState != GameState.Playing) return;
        CurrentState = GameState.LevelUp;
        Manager.LevelUpUI.Show();
    }

    public void OnLevelUpChoiceMade()
    {
        CurrentState = GameState.Playing;
        Manager.Exp.FlushPendingLevelUp();
    }

    public void OnAllWavesCleared()
    {
        if (CurrentState != GameState.Playing) return;
        CurrentState = GameState.Win;
        EndGame(true);
    }

    private void HandleTimeUp()
    {
        if (CurrentState != GameState.Playing) return;
        CurrentState = GameState.Lose;
        EndGame(false);
    }

    private void EndGame(bool isWin)
    {
        Manager.Timer.StopTimer();
        StopAllUnits();
        Manager.UI.ShowResult(isWin);
    }

    private void StopAllUnits()
    {
        foreach (var cell in Manager.Grid.GetOccupiedCells())
            cell.OccupyingUnit?.OnRemoved();
    }
}
