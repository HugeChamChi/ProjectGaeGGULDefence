using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>TD1004: 주기적으로 인접 빈 셀부터 노말 조커를 생성한다. 인구수 제한은 없다.</summary>
public sealed class TotemWildcardSpawner : RangedBuffTotemBase
{
    [Inject] private UnitFactory _factory;
    [Inject] private UnitSpawner _spawner;
    [Inject] private AssetLifecycleManager _assets;
    [Inject] private GameManager _game;
    private readonly TotemSpawnCharge _charge = new TotemSpawnCharge();
    private bool _loading;
    private bool _ready;
    /// <summary>게이지 표시용 변경 이벤트(0~1).</summary>
    public event Action<float> OnGaugeChanged;
    /// <summary>완충 후 빈 셀이 생길 때까지 1을 유지한다.</summary>
    public float GaugeProgress => _charge.Progress(Data?.WildcardSpawn?.IntervalSeconds ?? 0);

    protected override void ApplyBuff()
    {
        if (!_loading && !_ready)
            PrepareUnitAsync().Forget(e => { if (e is not OperationCanceledException) Debug.LogException(e); });
        OnGaugeChanged?.Invoke(GaugeProgress);
    }

    private async UniTask PrepareUnitAsync()
    {
        _loading = true;
        try
        {
            var settings = Data?.WildcardSpawn;
            if (settings?.Unit == null || settings.IntervalSeconds <= 0 || _assets == null)
            { Debug.LogError("[TotemWildcardSpawner] 조커 UnitData/주기/리소스 서비스 설정을 확인하세요."); return; }
            await _assets.LoadAsync(settings.Unit).AttachExternalCancellation(this.GetCancellationTokenOnDestroy());
            _ready = settings.Unit.prefab != null && settings.Unit.prefab.GetComponent<WildcardUnit>() != null;
            if (!_ready) Debug.LogError("[TotemWildcardSpawner] 조커 프리팹에는 WildcardUnit이 필요합니다.");
        }
        finally { _loading = false; }
    }

    private void Update()
    {
        if (!IsActive || !_ready || _game == null || _game.CurrentState != GameManager.GameState.Playing ||
            CurrentCell == null || CurrentCell.Model.IsSealed) return;
        double interval = Data.WildcardSpawn.IntervalSeconds;
        _charge.Advance(Time.deltaTime, interval);
        if (_charge.IsReady(interval) && TrySpawn()) _charge.Consume();
        OnGaugeChanged?.Invoke(GaugeProgress);
    }

    private bool TrySpawn()
    {
        if (_factory == null || _spawner == null || _gridManager == null) return false;
        var cell = FindNearestAvailableCell(_gridManager, CurrentCell.GridPosition);
        if (cell == null) return false;
        var unit = _factory.CreateUnitFromData(Data.WildcardSpawn.Unit, Tier.Normal);
        if (unit == null) return false;
        _spawner.PlaceUnitWithEffect(unit, cell, transform.position);
        return true;
    }

    /// <summary>대각선을 포함한 인접 링부터 탐색한다. 같은 거리에서는 그리드 순서를 유지한다.</summary>
    public static GridCell FindNearestAvailableCell(GridManager grid, Vector2Int origin)
    {
        GridCell best = null;
        int distance = int.MaxValue;
        foreach (var cell in grid.AllCells())
        {
            if (cell == null || !cell.IsAvailable) continue;
            int candidate = Mathf.Max(Mathf.Abs(cell.GridPosition.x - origin.x), Mathf.Abs(cell.GridPosition.y - origin.y));
            if (candidate >= distance) continue;
            best = cell;
            distance = candidate;
        }
        return best;
    }

    protected override void PaintRangeBuffs(GridCell cell)
    {
        foreach (var function in Data.functions)
            function?.Apply(this, cell, _totemBuffManager);
    }
}
