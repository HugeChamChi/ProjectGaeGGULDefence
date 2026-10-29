using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>
/// 실험실 전용(FxLab_StatusPopupIngame, 사용자 요청 2026-10-23): 본게임 씬 복사본에서 버튼 한 번으로
/// 상태 팝업이 실제 경로로 뜨는 모습을 한 차례 보여 준다.
/// 순서: 게임 시작·보스 대기 → 유닛 4 소환 → 토템(공격력▲·공격속도▼) → 선택지(공격속도▲, 배치 유닛 전체)
/// → 보스 화상·방어력▼ → 토템(공격력▲·공격속도▲). 끝나면 버튼은 꺼진다(다시 보려면 Play 재시작).
/// 복사본 씬은 이름이 IngameScene이 아니라 GameInitializer가 에디터 미리보기(로그인 없이 시작)를 켜지 않으므로,
/// 스코프가 만들어진 뒤(LifetimeScope -5000 이후) 초기화 전에 같은 준비를 여기서 한다. 게임 코드는 건드리지 않는다.
/// </summary>
[DefaultExecutionOrder(1000)]
public class StatusPopupIngameDemo : MonoBehaviour
{
    private const int UnitCount = 4;
    private const float DemoFood = 100000f;
    private const float SummonInterval = 0.3f;
    private const float SettleSeconds = 1.2f;
    private const float StepSeconds = 2f;
    private const float BootTimeoutSeconds = 20f;
    private const int DemoSourceId = 9901;

    [SerializeField] private TotemData _mixedTotem;
    [SerializeField] private TotemData _attackSpeedTotem;
    [SerializeField] private LevelUpData _choice;
    [SerializeField] private Button _button;
    [SerializeField] private TMP_Text _label;

    private bool _running;

    private void Awake()
    {
#if UNITY_EDITOR
        var scope = FindFirstObjectByType<InGameLifetimeScope>();
        var container = scope != null ? scope.Container : null;
        var game = Resolve<GameManager>(container);
        if (game == null || game.IsEditorPreview) return;
        game.gameObject.AddComponent<IngameEditorPreview>().Configure(Resolve<WaveManager>(container));
        game.EnableEditorPreview();
        Resolve<ResearchRunBonuses>(container)?.PrepareEditorPreview();
#endif
    }

    /// <summary>버튼에서 부른다. 한 번만 실행된다.</summary>
    public void Play()
    {
        if (_running) return;
        _running = true;
        if (_button != null) _button.interactable = false;
        RunAsync(this.GetCancellationTokenOnDestroy()).Forget(e =>
        {
            if (e is not OperationCanceledException) Debug.LogException(e, this);
        });
    }

    private async UniTask RunAsync(CancellationToken token)
    {
        SetLabel("시연 중...");
        var scope = FindFirstObjectByType<InGameLifetimeScope>();
        var container = scope != null ? scope.Container : null;
        var game = Resolve<GameManager>(container);
        var grid = Resolve<GridManager>(container);
        var spawner = Resolve<UnitSpawner>(container);
        var currency = Resolve<CurrencyManager>(container);
        var levelUp = Resolve<LevelUpManager>(container);
        var inventory = Resolve<TotemInventory>(container);
        var debuffSettings = Resolve<DebuffSettings>(container);
        if (game == null || grid == null || spawner == null || inventory == null)
        {
            SetLabel("본게임 구성요소를 찾지 못함");
            return;
        }

        // 1) 게임 시작 후 보스가 나올 때까지 기다린다.
        if (game.CurrentState != GameManager.GameState.Playing) game.OnStartButtonPressed();
        BossBase boss = null;
        float waited = 0f;
        while ((boss == null || game.CurrentState != GameManager.GameState.Playing) && waited < BootTimeoutSeconds)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: token);
            waited += 0.25f;
            boss = FindFirstObjectByType<BossBase>();
        }

        // 2) 유닛 소환.
        currency?.AddCurrency(DemoFood);
        for (int i = 0; i < UnitCount; i++)
        {
            spawner.OnSpawnButtonPressed();
            await UniTask.Delay(TimeSpan.FromSeconds(SummonInterval), cancellationToken: token);
        }
        await UniTask.Delay(TimeSpan.FromSeconds(SettleSeconds), cancellationToken: token);
        var cells = grid.GetOccupiedCells();
        if (cells.Count == 0)
        {
            SetLabel("유닛 소환 실패");
            return;
        }

        // 3) 토템: 인접 공격력▲ + 공격속도▼.
        await PlaceTotemNear(inventory, grid, _mixedTotem, cells[0], token);
        await UniTask.Delay(TimeSpan.FromSeconds(StepSeconds), cancellationToken: token);

        // 4) 선택지: 능력치 효과 → 배치 유닛 전체.
        if (levelUp != null && _choice != null) levelUp.ApplyEffect(_choice);
        await UniTask.Delay(TimeSpan.FromSeconds(StepSeconds), cancellationToken: token);

        // 5) 보스 디버프: 화상 + 방어력 감소(새로 걸림).
        if (boss != null && boss.Debuffs != null && debuffSettings != null)
        {
            foreach (var definition in debuffSettings.CreateDefinitions())
                if (definition.Kind == DebuffKind.Burn || definition.Kind == DebuffKind.ArmorBreak)
                    boss.Debuffs.Apply(definition, new DebuffApplyContext(DemoSourceId, 1, 0));
        }
        await UniTask.Delay(TimeSpan.FromSeconds(StepSeconds), cancellationToken: token);

        // 6) 토템: 안쪽 공격력▲ / 바깥 공격속도▲.
        await PlaceTotemNear(inventory, grid, _attackSpeedTotem, cells[Mathf.Min(2, cells.Count - 1)], token);
        await UniTask.Delay(TimeSpan.FromSeconds(StepSeconds), cancellationToken: token);

        SetLabel("시연 끝 (다시 보려면 Play 재시작)");
    }

    private static async UniTask PlaceTotemNear(TotemInventory inventory, GridManager grid, TotemData data, GridCell anchor, CancellationToken token)
    {
        if (data == null || anchor == null || !inventory.TryAdd(data)) return;
        foreach (var cell in Neighbors(grid, anchor))
        {
            if (cell.OccupyingUnit != null || cell.OccupyingTotem != null) continue;
            if (await inventory.TryPlaceAsync(inventory.Items.Count - 1, cell, token)) return;
        }
    }

    private static IEnumerable<GridCell> Neighbors(GridManager grid, GridCell anchor)
    {
        var offsets = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        foreach (var offset in offsets)
        {
            var cell = grid.GetCell(anchor.GridPosition + offset);
            if (cell != null) yield return cell;
        }
    }

    private static T Resolve<T>(IObjectResolver container) where T : class
    {
        if (container != null && container.TryResolve<T>(out var resolved)) return resolved;
        return null;
    }

    private void SetLabel(string text)
    {
        if (_label != null) _label.text = text;
    }
}
