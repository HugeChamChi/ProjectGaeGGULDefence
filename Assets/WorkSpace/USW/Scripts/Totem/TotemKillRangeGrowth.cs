using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

/// <summary>TD1002 뿌리내림: 살아 있는 보스와 전투 중 성장하며 만개 뒤 주기적으로 수확한다.</summary>
public sealed class TotemKillRangeGrowth : RangedBuffTotemBase
{
    private const double MinimumGaugeDurationSeconds = 0.1;

    [Inject] private GameManager _game;
    [Inject] private BossManager _bosses;
    [Inject] private CurrencyManager _currency;
    [Inject] private TimeScaleService _timeScale;
    [Inject] private FieldPauseVisuals _pause;
    private double _seconds;
    private double _harvest;
    /// <summary>만개까지 누적한 전투 시간.</summary>
    public double CombatSeconds => _seconds;
    /// <summary>현재 수확 주기 진행 시간.</summary>
    public double HarvestSeconds => _harvest;
    /// <summary>현재 단계. 순서는 SO가 정의한다.</summary>
    public int StageIndex
    {
        get
        {
            int best=-1;float threshold=-1;
            if(Data?.GrowthStages==null)return best;
            for(int i=0;i<Data.GrowthStages.Count;i++)
            {var stage=Data.GrowthStages[i];if(stage!=null && stage.RequiredSeconds<=_seconds && stage.RequiredSeconds>threshold){best=i;threshold=stage.RequiredSeconds;}}
            return best;
        }
    }
    /// <summary>현재 단계 정의.</summary>
    public TotemGrowthStage CurrentStage => StageIndex>=0?Data.GrowthStages[StageIndex]:null;
    /// <summary>만개까지의 전투 시간.</summary>
    public double BloomSeconds
    {get {double max=0;if(Data?.GrowthStages!=null)foreach(var stage in Data.GrowthStages)if(stage!=null)max=Math.Max(max,stage.RequiredSeconds);return max;}}
    /// <summary>다음 성장 단계 또는 만개 후 수확 진행 비율.</summary>
    public float GaugeProgress
    {
        get
        {
            if(Data==null)return 0;
            if(_seconds>=BloomSeconds)return Mathf.Clamp01((float)(_harvest/Math.Max(MinimumGaugeDurationSeconds,Data.GrowthHarvestSeconds)));
            double start=CurrentStage?.RequiredSeconds??0,next=BloomSeconds;
            foreach(var stage in Data.GrowthStages)if(stage!=null&&stage.RequiredSeconds>_seconds)next=Math.Min(next,stage.RequiredSeconds);
            return Mathf.Clamp01((float)((_seconds-start)/Math.Max(MinimumGaugeDurationSeconds,next-start)));
        }
    }
    /// <summary>게이지 변경. 수치는 SO/현재 상태에서 읽는다.</summary>
    public event Action<float> OnGaugeChanged;
    /// <summary>현재 성장/수확 진행 설명.</summary>
    public string ProgressDescription => CurrentStage==null?string.Empty:
        _seconds>=BloomSeconds ? $"현재 {CurrentStage.Name} · 다음 수확 {Math.Max(0,Data.GrowthHarvestSeconds-_harvest):0}초" : $"현재 {CurrentStage.Name} · 전투 {_seconds:0}초 / 만개 {BloomSeconds:0}초";
    protected override void OnPlacementStarted() { _seconds=_harvest=0;OnGaugeChanged?.Invoke(GaugeProgress); }
    protected override void OnPlacementEnded() { _seconds=_harvest=0;OnGaugeChanged?.Invoke(0); }
    private void Update() => TickCombat(Time.deltaTime);
    /// <summary>실제 전투 조건을 만족한 시간만 누적한다. 정지/봉인/보스 공백은 진행하지 않는다.</summary>
    public void TickCombat(float deltaTime)
    {
        if(!IsActive || Data==null || deltaTime<=0 || float.IsInfinity(deltaTime) || float.IsNaN(deltaTime)
            || _game==null || _game.CurrentState!=GameManager.GameState.Playing || _timeScale?.IsPaused==true || _pause?.AttacksHeld==true) return;
        var boss=_bosses?.CurrentBoss;if(boss==null || boss.IsDead)return;
        int before=StageIndex;double bloom=BloomSeconds;
        double excess=Math.Max(0,_seconds+deltaTime-bloom);_seconds=Math.Min(bloom,_seconds+deltaTime);
        if(before!=StageIndex)
        {
            _totemBuffManager?.RebuildCellBuffFlags();
            if(_gridManager!=null && _gridManager.IsPreviewingTotem(this))_gridManager.ShowTotemRangePreview(this);
        }
        if(_seconds>=bloom && Data.GrowthHarvestSeconds>0)
        {
            _harvest+=excess;
            int ticks=(int)Math.Floor(_harvest/Data.GrowthHarvestSeconds);
            if(ticks>0){_harvest-=ticks*Data.GrowthHarvestSeconds;_currency?.AddCurrency(ticks*Data.GrowthHarvestFood);}
        }
        OnGaugeChanged?.Invoke(GaugeProgress);
    }
    /// <inheritdoc />
    public override List<GridCell> GetAffectedCells()
    {
        var cells=new List<GridCell>();var stage=CurrentStage;
        if(!IsActive || stage==null || CurrentCell==null || _gridManager==null)return cells;
        foreach(var offset in stage.Offsets){var p=CurrentCell.GridPosition+RotateOffset(offset);var cell=_gridManager.GetCell(p.x,p.y);if(cell!=null&&!cells.Contains(cell))cells.Add(cell);}
        return cells;
    }
    /// <inheritdoc />
    public override void PaintAffectedCells()
    {if(!IsActive)return;foreach(var cell in GetAffectedCells())PaintRangeBuffs(cell);}
    protected override void PaintRangeBuffs(GridCell cell)
    {
        var stage=CurrentStage;if(stage==null)return;
        cell.AddTotemCellBonus(StatKind.AttackPercent,stage.AttackBonus);
        cell.SetBuffFlags(true,cell.HasSpeedBuff);
    }
}
