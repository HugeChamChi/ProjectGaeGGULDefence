using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>보스 외형에 의존하지 않는 8개 배치 기믹의 전투 판정과 수명. Configure로 씬 의존성을 명시적으로 받는다.</summary>
[DefaultExecutionOrder(31990)]
public sealed class PatternLabDriver : MonoBehaviour
{
    private const float MinimumLength = .001f;
    private static readonly Color Safe = new Color(.13f,1f,.76f,.95f);
    private static readonly Color Warning = new Color(1f,.77f,.19f,.95f);
    private readonly List<GridCell> _cells = new();
    private readonly List<GridCell> _marked = new();
    private readonly List<GridCell> _safe = new();
    private readonly List<UnitBase> _units = new();
    private readonly HashSet<UnitBase> _hitUnits = new();
    private readonly Dictionary<UnitBase,GridCell> _stationary = new();
    private readonly List<SlowEffect> _slows = new();
    private readonly List<UnitBase> _stunned = new();
    private sealed class SlowEffect { public GridCell Cell; public float Remaining; }
    private BossBase _boss;
    private GridManager _grid;
    private GameManager _game;
    private FieldPauseVisuals _pause;
    private PatternLabGeometry _geometry;
    private PatternLabData _pattern;
    private Bounds _board;
    private float _cellWidth, _elapsed, _resolvedAt;
    private bool _resolved, _released, _success, _aimLocked, _memoryHidden;
    private int _pendingFrame = -1, _gapRow, _nextColumn;
    private Vector3 _beamStart, _beamEnd;
    private UnitBase _source, _partner;
    private GridCell _relay;
    private bool _controllerClock;

    /// <summary>현재 시전 중인 기믹. 결과 표시가 끝나면 null.</summary>
    public PatternLabData CurrentPattern => _pattern;
    /// <summary>시전 또는 결과 표시 중인지 여부.</summary>
    public bool IsBusy => _pattern != null;
    /// <summary>현재 패턴에서 실제 적중한 서로 다른 유닛 수.</summary>
    public int LastHitCount { get; private set; }
    /// <summary>최근 결과 또는 대응 안내.</summary>
    public string Status { get; private set; }
    /// <summary>현재 패턴의 전투 경과 시간.</summary>
    public float Elapsed => _elapsed;
    /// <summary>조준 또는 운반 표식 대상.</summary>
    public UnitBase SourceUnit => _source;
    /// <summary>사슬로 연결한 두 번째 유닛.</summary>
    public UnitBase PartnerUnit => _partner;
    /// <summary>점령하여 시전을 취소할 빈 봉인석 위치.</summary>
    public GridCell RelayCell => _relay;
    /// <summary>고정된 안전 발판 목록.</summary>
    public IReadOnlyList<GridCell> SafeCells => _safe;
    /// <summary>현재 표시/판정할 위험 칸. 유도 포격은 조준 고정 전까지만 바뀐다.</summary>
    public IReadOnlyList<GridCell> MarkedCells => _marked;
    /// <summary>첫 판정 전이며 족장 취소를 허용한 패턴인지 여부.</summary>
    public bool IsCounterable => _pattern != null && !_resolved && !_released && _pendingFrame < 0 && _pattern.AllowChiefCounter;
    /// <summary>표시 문구가 달라졌을 때 발생한다.</summary>
    public event Action<string> OnStatusChanged;

    /// <summary>소환한 보스와 현재 씬의 서비스·공용 머티리얼을 연결한다. 싱글턴이나 오브젝트 검색은 하지 않는다.</summary>
    public void Configure(BossBase boss,GridManager grid,GameManager game,FieldPauseVisuals pause,Material material,bool controllerClock=false)
    {
        _controllerClock=controllerClock;
        ResetAll();
        if(_boss!=null)_boss.OnDeath-=OnBossDied;
        _boss=boss;_grid=grid;_game=game;_pause=pause;
        if(_boss!=null)_boss.OnDeath+=OnBossDied;
        _cells.Clear();
        if(grid!=null)foreach(var cell in grid.AllCells())if(cell!=null)_cells.Add(cell);
        if(_cells.Count==0)throw new InvalidOperationException("Pattern driver requires an initialized grid.");
        _board=PatternLabGeometry.BoundsOf(_cells[0]);
        foreach(var cell in _cells)_board.Encapsulate(PatternLabGeometry.BoundsOf(cell));
        _cellWidth=PatternLabGeometry.BoundsOf(_cells[0]).size.x;
        if(_geometry==null)
        {
            var go=new GameObject("Pattern mechanic geometry");go.transform.SetParent(transform,false);
            _geometry=go.AddComponent<PatternLabGeometry>();_geometry.Configure(material);
        }
    }

    /// <summary>비어 있을 때 새 SO 기믹을 시작한다. 기존 시전을 덮어쓰지 않는다.</summary>
    public bool TryStart(PatternLabData pattern)
    {
        if(pattern==null||IsBusy||_boss==null||_boss.IsDead||_grid==null)return false;
        if(_game!=null&&_game.CurrentState!=GameManager.GameState.Playing)return false;
        _units.Clear();_marked.Clear();_safe.Clear();_hitUnits.Clear();_stationary.Clear();
        foreach(var cell in _cells)if(Placed(cell.OccupyingUnit))_units.Add(cell.OccupyingUnit);
        if(_units.Count==0)return false;
        _source=_units[UnityEngine.Random.Range(0,_units.Count)];_partner=null;_relay=null;
        _elapsed=0f;_resolvedAt=0f;_pendingFrame=-1;_nextColumn=0;LastHitCount=0;
        _resolved=false;_released=false;_success=false;_aimLocked=false;_memoryHidden=false;
        if(pattern.Mechanic==PatternLabMechanic.BreakTether)
        {
            float nearest=float.MaxValue;
            foreach(var unit in _units)
            {
                if(unit==_source)continue;
                float distance=Vector2Int.Distance(unit.currentCell.GridPosition,_source.currentCell.GridPosition);
                if(distance<nearest){nearest=distance;_partner=unit;}
            }
            if(_partner==null)return false;
        }
        if(pattern.Mechanic==PatternLabMechanic.StormShelter)
        {
            int width=Mathf.Clamp(pattern.SafeColumns,1,Mathf.Max(1,_grid.Columns-1));
            int first=UnityEngine.Random.value<.5f?0:_grid.Columns-width;
            foreach(var cell in _cells)if(cell.GridPosition.x>=first&&cell.GridPosition.x<first+width)_safe.Add(cell);else _marked.Add(cell);
        }
        if(pattern.Mechanic==PatternLabMechanic.MemoryTiles)
        {
            var choices=new List<GridCell>(_cells);
            for(int i=0;i<Mathf.Min(pattern.MemorySafeCount,choices.Count);i++)
            {
                int index=UnityEngine.Random.Range(i,choices.Count);(choices[i],choices[index])=(choices[index],choices[i]);_safe.Add(choices[i]);
            }
            foreach(var cell in _cells)if(!_safe.Contains(cell))_marked.Add(cell);
        }
        if(pattern.Mechanic==PatternLabMechanic.OccupyRelay)
        {
            var empty=_cells.FindAll(c=>c.IsAvailable);
            if(empty.Count==0)return false;
            _relay=empty[UnityEngine.Random.Range(0,empty.Count)];
        }
        _gapRow=UnityEngine.Random.Range(0,Mathf.Max(1,_grid.Rows));
        _pattern=pattern;
        UpdateDynamicTargets();
        SetStatus(pattern.DisplayName+" · "+pattern.Instruction);
        Draw();
        return true;
    }

    private void OnEnable()=>ChiefActiveSkillSignals.OnActivated+=HandleChief;
    private void HandleChief(){if(!_controllerClock&&IsCounterable)TryCounter();}
    /// <summary>허용한 기믹만 첫 판정 전에 족장으로 취소한다. 배치로 해제하는 기믹에는 적용하지 않는다.</summary>
    public bool TryCounter()
    {
        if(!IsCounterable)return false;
        Complete(true,"족장 카운터 · 시전 취소");return true;
    }

    private void LateUpdate()
    {
        if(_controllerClock)return;
        if(_boss==null||_boss.IsDead||(_game!=null&&_game.IsFinished)){if(IsBusy||_slows.Count>0||_stunned.Count>0)ResetAll();return;}
        if(Time.deltaTime<=0f)return;
        Advance(Time.deltaTime);
    }

    /// <summary>전투 시간만큼 진행한다. 선택 유예·게임 정지 상태에서는 진행하지 않는다.</summary>
    public void Advance(float deltaTime)
    {
        if(deltaTime<0f||(_game!=null&&_game.CurrentState!=GameManager.GameState.Playing)||_pause?.AttacksHeld==true)return;
        TickEffects(deltaTime);
        if(_pattern==null)return;
        if(_boss==null||_boss.IsDead){ResetAll();return;}
        _elapsed+=deltaTime;
        if(_resolved)
        {
            if(_elapsed>=_resolvedAt+_pattern.Recovery){_pattern=null;_geometry.Begin();_geometry.Flush();}
            else Draw();
            return;
        }
        UpdateDynamicTargets();
        if(_pattern.Mechanic==PatternLabMechanic.BreakTether)
        {
            if(!Placed(_source)||!Placed(_partner)){Complete(true,"사슬 대상이 사라져 시전 해제");Draw();return;}
            if(Vector2Int.Distance(_source.currentCell.GridPosition,_partner.currentCell.GridPosition)>=_pattern.TetherDistance)
            {Complete(true,"사슬 절단 성공 · 두 유닛을 분리했습니다");Draw();return;}
        }
        if(_pattern.Mechanic==PatternLabMechanic.OccupyRelay&&_relay!=null&&Placed(_relay.OccupyingUnit))
        {Complete(true,"봉인석 점령 성공 · 전체 공격 차단");Draw();return;}
        if((_pattern.Mechanic==PatternLabMechanic.CarrierBomb||_pattern.Mechanic==PatternLabMechanic.BaitLaser)&&!Placed(_source))
        {Complete(true,"표식 대상이 사라져 시전 해제");Draw();return;}
        if(_pattern.Mechanic==PatternLabMechanic.MemoryTiles&&!_memoryHidden&&_elapsed>=_pattern.LockTime)
        {_memoryHidden=true;SetStatus("발판 표시가 사라졌습니다 · 기억한 칸으로 이동");}
        if(!_released&&_elapsed>=_pattern.Windup)
        {
            if(_pendingFrame<0)_pendingFrame=Time.frameCount;
            else if(Time.frameCount>_pendingFrame){_released=true;Release();}
        }
        if(!_resolved&&_released)
        {
            if(_pattern.Mechanic==PatternLabMechanic.GapWall)UpdateWall();
            if(_pattern.Mechanic==PatternLabMechanic.RedLight)UpdateRedLight();
        }
        Draw();
    }

    private void UpdateDynamicTargets()
    {
        if(_pattern==null)return;
        if(_pattern.Mechanic==PatternLabMechanic.CarrierBomb)
        {
            _marked.Clear();
            if(!Placed(_source))return;
            foreach(var cell in _cells)
            {
                var delta=cell.GridPosition-_source.currentCell.GridPosition;
                if(Mathf.Max(Mathf.Abs(delta.x),Mathf.Abs(delta.y))<=_pattern.BlastRadius)_marked.Add(cell);
            }
        }
        if(_pattern.Mechanic==PatternLabMechanic.BaitLaser&&!_aimLocked)
        {
            if(!Placed(_source))return;
            _beamStart=_boss.transform.position+Vector3.up*_cellWidth;
            Vector3 direction=(Center(_source.currentCell)-_beamStart).normalized;
            _beamEnd=_beamStart+direction*_board.size.magnitude*2f;
            _marked.Clear();
            foreach(var cell in _cells)if(DistanceToBeam(Center(cell))<=_cellWidth*_pattern.BeamWidthCells*.5f)_marked.Add(cell);
            if(_elapsed>=_pattern.LockTime){_aimLocked=true;SetStatus("포격 방향 고정! · 붉은 선에서 벗어나세요");}
        }
    }

    private void Release()
    {
        switch(_pattern.Mechanic)
        {
            case PatternLabMechanic.BreakTether: Hit(_source);Hit(_partner);break;
            case PatternLabMechanic.CarrierBomb:
                foreach(var cell in _marked)if(cell.OccupyingUnit!=_source)Hit(cell.OccupyingUnit);break;
            case PatternLabMechanic.StormShelter:
            case PatternLabMechanic.BaitLaser:
                foreach(var cell in _marked)Hit(cell.OccupyingUnit);break;
            case PatternLabMechanic.MemoryTiles:
                foreach(var cell in _marked)ApplySlow(cell);break;
            case PatternLabMechanic.OccupyRelay:
                foreach(var cell in _cells)Hit(cell.OccupyingUnit);break;
            case PatternLabMechanic.RedLight:
                foreach(var cell in _cells)if(Placed(cell.OccupyingUnit))_stationary[cell.OccupyingUnit]=cell;
                SetStatus("빨간불! · 유닛을 옮기지 마세요");return;
            case PatternLabMechanic.GapWall:SetStatus("장벽 이동 중 · 청록색 틈으로 피하세요");return;
        }
        Complete(LastHitCount==0,LastHitCount==0?"회피 성공 · 아군 피해 없음":"패턴 판정 · "+LastHitCount+"기 적중");
    }

    private void UpdateWall()
    {
        float progress=Mathf.Clamp01((_elapsed-_pattern.Windup)/Mathf.Max(MinimumLength,_pattern.ActiveDuration));
        int reached=Mathf.Min(_grid.Columns-1,Mathf.FloorToInt(progress*_grid.Columns-.5f));
        while(_nextColumn<=reached)
        {
            foreach(var cell in _cells)if(cell.GridPosition.x==_nextColumn&&cell.GridPosition.y!=_gapRow)Hit(cell.OccupyingUnit);
            _nextColumn++;
        }
        if(progress>=1f)Complete(LastHitCount==0,"장벽 통과 · "+LastHitCount+"기 적중");
    }

    private void UpdateRedLight()
    {
        foreach(var cell in _cells)
        {
            var unit=cell.OccupyingUnit;
            if(!Placed(unit))continue;
            if(!_stationary.TryGetValue(unit,out var previous))_stationary[unit]=unit.currentCell;
            else if(unit.currentCell!=previous){Hit(unit);_stationary[unit]=unit.currentCell;}
        }
        if(_elapsed>=_pattern.Windup+_pattern.ActiveDuration)Complete(LastHitCount==0,LastHitCount==0?"정지 성공 · 다시 움직여도 됩니다":"신호 종료 · "+LastHitCount+"기 이동 감지");
    }

    private void Hit(UnitBase unit)
    {
        if(!Placed(unit)||unit.StunImmune||!_hitUnits.Add(unit))return;
        unit.ApplyStun(_pattern.StunDuration,_pattern.StunVisual);
        if(unit.IsStunned&&!_stunned.Contains(unit))_stunned.Add(unit);
        LastHitCount++;
    }
    private void ApplySlow(GridCell cell)
    {
        cell.Model.SetSpeedModifier(1f/Mathf.Max(.1f,1f-_pattern.SlowRate));
        var effect=_slows.Find(e=>e.Cell==cell);
        if(effect==null){effect=new SlowEffect{Cell=cell};_slows.Add(effect);}
        effect.Remaining=_pattern.EffectDuration;
        if(Placed(cell.OccupyingUnit)&&_hitUnits.Add(cell.OccupyingUnit))LastHitCount++;
    }
    private void TickEffects(float deltaTime)
    {
        for(int i=_slows.Count-1;i>=0;i--)
        {
            var effect=_slows[i];effect.Remaining-=deltaTime;
            if(effect.Cell!=null&&effect.Remaining>0f)continue;
            if(effect.Cell!=null)effect.Cell.Model.SetSpeedModifier(1f);_slows.RemoveAt(i);
        }
        for(int i=_stunned.Count-1;i>=0;i--)if(_stunned[i]==null||!_stunned[i].IsStunned)_stunned.RemoveAt(i);
    }
    private void Complete(bool success,string message){_resolved=true;_success=success;_resolvedAt=_elapsed;SetStatus(message);}
    private void SetStatus(string message){Status=message;OnStatusChanged?.Invoke(message);}

    /// <summary>교체·제거 시 진행 중인 시전, 소유한 기절/셀 효과, 메시를 모두 해제한다.</summary>
    public void ResetAll()
    {
        _pattern=null;_resolved=false;_released=false;_pendingFrame=-1;
        foreach(var effect in _slows)if(effect.Cell!=null)effect.Cell.Model.SetSpeedModifier(1f);_slows.Clear();
        foreach(var unit in _stunned)if(unit!=null)unit.ClearStun();_stunned.Clear();
        _marked.Clear();_safe.Clear();_stationary.Clear();
        if(_geometry!=null){_geometry.Begin();_geometry.Flush();}
    }
    private void OnBossDied()=>ResetAll();
    private static bool Placed(UnitBase unit)=>unit!=null&&unit.isActiveAndEnabled&&unit.currentCell!=null;
    private static Vector3 Center(GridCell cell)=>PatternLabGeometry.BoundsOf(cell).center;
    private float DistanceToBeam(Vector3 point)
    {
        Vector3 ab=_beamEnd-_beamStart;
        float t=Mathf.Clamp01(Vector3.Dot(point-_beamStart,ab)/Mathf.Max(MinimumLength,ab.sqrMagnitude));
        return Vector3.Distance(point,_beamStart+ab*t);
    }

    private void Draw()
    {
        if(_geometry==null||_pattern==null)return;
        _geometry.Begin();
        Color accent=_resolved?(_success?Safe:_pattern.Accent):_pattern.Accent;
        float progress=Mathf.Clamp01(_elapsed/Mathf.Max(MinimumLength,_pattern.Windup));
        switch(_pattern.Mechanic)
        {
            case PatternLabMechanic.BreakTether:DrawTether(accent,progress);break;
            case PatternLabMechanic.GapWall:DrawWall(accent,progress);break;
            case PatternLabMechanic.StormShelter:
                foreach(var cell in _marked)_geometry.Tile(cell,new Color(accent.r,accent.g,accent.b,.35f+progress*.5f),true);
                foreach(var cell in _safe)_geometry.Tile(cell,Safe);
                if(_safe.Count>0)DrawShelterSymbol(Center(_safe[_safe.Count/2]),Safe);break;
            case PatternLabMechanic.CarrierBomb:DrawBomb(accent,progress);break;
            case PatternLabMechanic.BaitLaser:
                _geometry.Line(_beamStart,_beamEnd,_cellWidth*_pattern.BeamWidthCells*(_aimLocked?1f:.07f),new Color(accent.r,accent.g,accent.b,_aimLocked?.5f:.9f));
                foreach(var cell in _marked)_geometry.Frame(Center(cell),Vector2.one*_cellWidth*.76f,accent);
                if(Placed(_source))_geometry.Diamond(Center(_source.currentCell)+Vector3.up*_cellWidth*.38f,_cellWidth*.16f,Warning);break;
            case PatternLabMechanic.MemoryTiles:
                if(!_memoryHidden||_resolved)foreach(var cell in _safe)_geometry.Tile(cell,Safe);
                if(_memoryHidden&&!_resolved)
                    foreach(var cell in _cells)_geometry.Frame(Center(cell),Vector2.one*_cellWidth*.82f,new Color(.7f,.72f,.9f,.22f));
                if(_resolved)foreach(var cell in _marked)_geometry.Tile(cell,accent,true);break;
            case PatternLabMechanic.OccupyRelay:DrawRelay(progress,accent);break;
            case PatternLabMechanic.RedLight:DrawSignal(progress,accent);break;
        }
        _geometry.Flush();
    }

    private void DrawTether(Color color,float progress)
    {
        if(!Placed(_source)||!Placed(_partner))return;
        Vector3 a=Center(_source.currentCell),b=Center(_partner.currentCell),side=new Vector3(-(b-a).y,(b-a).x).normalized;
        _geometry.Line(a,b,_cellWidth*.045f,Warning);
        const int links=9;
        for(int i=1;i<links;i++)
        {
            Vector3 center=Vector3.Lerp(a,b,i/(float)links)+side*Mathf.Sin(_elapsed*7f+i)*_cellWidth*.035f;
            _geometry.Frame(center,Vector2.one*_cellWidth*.16f,color,.035f);
        }
        _geometry.Arrow(a,a+(a-b).normalized*_cellWidth*.7f,.13f,Safe);
        _geometry.Arrow(b,b+(b-a).normalized*_cellWidth*.7f,.13f,Safe);
        _geometry.Diamond(a,_cellWidth*(.09f+progress*.07f),color);_geometry.Diamond(b,_cellWidth*(.09f+progress*.07f),color);
    }
    private void DrawWall(Color accent,float windup)
    {
        float progress=_released?Mathf.Clamp01((_elapsed-_pattern.Windup)/Mathf.Max(MinimumLength,_pattern.ActiveDuration)):0f;
        float x=Mathf.Lerp(_board.min.x,_board.max.x,progress);
        foreach(var cell in _cells)if(cell.GridPosition.y==_gapRow)_geometry.Frame(Center(cell),Vector2.one*_cellWidth*.8f,Safe);
        foreach(var cell in _cells)
        {
            if(cell.GridPosition.x!=0||cell.GridPosition.y==_gapRow)continue;
            var b=PatternLabGeometry.BoundsOf(cell);
            var center=new Vector3(x,b.center.y,b.center.z);
            _geometry.Box(center,new Vector2(_cellWidth*.24f,b.size.y),new Color(accent.r,accent.g,accent.b,_released?.8f:.2f+windup*.5f));
            _geometry.Line(center+Vector3.up*b.extents.y,center-Vector3.up*b.extents.y,.055f,accent);
        }
        _geometry.Arrow(new Vector3(_board.min.x,_board.max.y+_cellWidth*.18f),new Vector3(_board.max.x,_board.max.y+_cellWidth*.18f),.13f,Warning);
    }
    private void DrawBomb(Color accent,float progress)
    {
        if(!Placed(_source))return;
        foreach(var cell in _marked)_geometry.Tile(cell,accent,cell!=_source.currentCell);
        var c=Center(_source.currentCell);
        _geometry.Diamond(c+Vector3.up*_cellWidth*.38f,_cellWidth*.24f,accent);
        _geometry.Line(c+new Vector3(-.32f,-.32f)*_cellWidth,c+new Vector3(.32f,-.32f)*_cellWidth,.07f,Warning);
        _geometry.Line(c+new Vector3(-.32f,-.32f)*_cellWidth,c+new Vector3(-.32f+.64f*progress,-.32f)*_cellWidth,.035f,Color.white);
        _geometry.Arrow(c,c+Vector3.right*_cellWidth*.75f,.12f,Safe);
    }
    private void DrawRelay(float progress,Color accent)
    {
        if(_relay==null)return;
        Vector3 c=Center(_relay);_geometry.Tile(_relay,Safe);
        _geometry.Diamond(c,_cellWidth*.29f,Safe);
        _geometry.Diamond(c,_cellWidth*.18f,new Color(.08f,.17f,.25f,1f));
        for(int i=0;i<4;i++)
        {
            Vector3 direction=i switch{0=>Vector3.up,1=>Vector3.right,2=>Vector3.down,_=>Vector3.left};
            _geometry.Arrow(c+direction*_cellWidth*.95f,c+direction*_cellWidth*.38f,.12f,Safe);
        }
        _geometry.Box(c+Vector3.down*_cellWidth*.42f,new Vector2(_cellWidth*.8f,_cellWidth*.07f),accent);
        _geometry.Box(c+new Vector3((progress-1f)*.4f,-.42f)*_cellWidth,new Vector2(_cellWidth*.8f*progress,_cellWidth*.035f),Warning);
    }
    private void DrawSignal(float progress,Color accent)
    {
        Vector3 center=new Vector3(_board.center.x,_board.max.y+_cellWidth*.35f);
        for(int i=0;i<3;i++)
        {
            var position=center+Vector3.right*(i-1)*_cellWidth*.55f;
            bool on=_released||progress>i/3f;
            _geometry.Diamond(position,_cellWidth*.17f,on?(_released?accent:Warning):new Color(.22f,.3f,.37f,.8f));
        }
        foreach(var cell in _cells)_geometry.Frame(Center(cell),Vector2.one*_cellWidth*.88f,_released?new Color(accent.r,accent.g,accent.b,.65f):new Color(Warning.r,Warning.g,Warning.b,.18f));
    }
    private void DrawShelterSymbol(Vector3 c,Color color)
    {
        c+=Vector3.up*_cellWidth*.2f;
        _geometry.Line(c+Vector3.left*_cellWidth*.35f,c+Vector3.up*_cellWidth*.35f,.07f,color);
        _geometry.Line(c+Vector3.up*_cellWidth*.35f,c+Vector3.right*_cellWidth*.35f,.07f,color);
        _geometry.Frame(c+Vector3.down*_cellWidth*.15f,new Vector2(_cellWidth*.45f,_cellWidth*.4f),color);
    }
    private void OnDisable(){ChiefActiveSkillSignals.OnActivated-=HandleChief;ResetAll();}
    private void OnDestroy(){if(_boss!=null)_boss.OnDeath-=OnBossDied;ResetAll();}
}
