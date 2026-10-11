using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

/// <summary>TD1002 뿌리내림: 배치 후 게임 진행 시간에 따라 성장하고 최대 성장 뒤 주기적으로 수확한다.</summary>
public sealed class TotemKillRangeGrowth : RangedBuffTotemBase
{
    private const double MinimumGaugeDurationSeconds = 0.1;

    [Inject] private GameManager _game;
    [Inject] private CurrencyManager _currency;
    [Inject] private TimeScaleService _timeScale;
    [Inject] private FieldPauseVisuals _pause;
    private double _seconds;
    private double _harvest;
    /// <summary>최대 성장까지 누적한 진행 시간. 기존 API 이름을 유지한다.</summary>
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
    /// <summary>최대 성장까지 필요한 진행 시간.</summary>
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
    public string ProgressDescription
    {
        get
        {
            if (CurrentStage == null) return string.Empty;
            if (_seconds >= BloomSeconds)
                return $"다음 식량까지 {Math.Ceiling(Math.Max(0, Data.GrowthHarvestSeconds - _harvest)):0}초";
            double next = BloomSeconds;
            foreach (var stage in Data.GrowthStages)
                if (stage != null && stage.RequiredSeconds > _seconds)
                    next = Math.Min(next, stage.RequiredSeconds);
            return $"다음 강화까지 {Math.Ceiling(Math.Max(0, next - _seconds)):0}초";
        }
    }
    protected override void OnPlacementStarted() { _seconds=_harvest=0;OnGaugeChanged?.Invoke(GaugeProgress); }
    protected override void OnPlacementEnded() { _seconds=_harvest=0;OnGaugeChanged?.Invoke(0); }
    private void Update() => TickCombat(Time.deltaTime);
    /// <summary>Playing 중 보스 사이 대기도 누적한다. 선택/보상 정지와 봉인 중에는 진행하지 않는다.</summary>
    public void TickCombat(float deltaTime)
    {
        if(!IsActive || Data==null || deltaTime<=0 || float.IsInfinity(deltaTime) || float.IsNaN(deltaTime)
            || _game==null || _game.CurrentState!=GameManager.GameState.Playing || _timeScale?.IsPaused==true || _pause?.AttacksHeld==true) return;
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
