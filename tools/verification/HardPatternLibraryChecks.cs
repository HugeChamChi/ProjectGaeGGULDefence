using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using Object=UnityEngine.Object;

public static class HardPatternLibraryChecks
{
    private static readonly List<string> Results=new();
    private static readonly Stack<(UnitBase Unit,GridCell Origin)> Moves=new();
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static PatternLabDriver _driver;
    private static GridCell[] _cells;
    private static PatternLabCatalog _catalog;
    private static void Check(bool condition,string description)
    {
        Results.Add((condition?"PASS ":"FAIL ")+description);
        if(!condition)throw new InvalidOperationException(description);
    }
    private static T Field<T>(string name)=>(T)typeof(PatternLabDriver).GetField(name,Private).GetValue(_driver);
    private static void Move(UnitBase unit,GridCell target)
    {
        if(unit==null||target==null||!target.IsAvailable)throw new InvalidOperationException("Movement test requires an empty cell.");
        var origin=unit.currentCell;Moves.Push((unit,origin));origin.RemoveUnit();unit.GetComponent<DragHandler>().PlaceSelfAt(target);
    }
    private static void RestoreMoves()
    {
        while(Moves.Count>0)
        {
            var move=Moves.Pop();if(move.Unit==null)continue;
            move.Unit.ClearStun();move.Unit.currentCell?.RemoveUnit();move.Unit.GetComponent<DragHandler>().PlaceSelfAt(move.Origin);
        }
    }
    private static PatternLabData Begin(int index)
    {
        _driver.ResetAll();RestoreMoves();
        var pattern=_catalog.Patterns[index];Check(_driver.TryStart(pattern),"start "+pattern.DisplayName);return pattern;
    }
    private static void Release(PatternLabData pattern,float? advance=null)
    {
        _driver.Advance((advance??pattern.Windup)+.001f);
        Check(!Field<bool>("_released"),"first impact waits for later rendered frame");
        typeof(PatternLabDriver).GetField("_pendingFrame",Private).SetValue(_driver,Time.frameCount-1);
        _driver.Advance(0f);
        Check(Field<bool>("_released"),"first impact released after frame boundary");
    }
    public static string Run()
    {
        Results.Clear();Moves.Clear();
        var scope=Object.FindFirstObjectByType<InGameLifetimeScope>();
        var bosses=scope.Container.Resolve<BossManager>();
        var mode=scope.Container.Resolve<WaveManager>().EndlessMode;
        bosses.SpawnSingleBoss(new BossEntry{Data=mode.Slots[0].Boss},null);
        var boss=bosses.CurrentBoss;boss.ExpMultiplier=0f;
        scope.Container.Resolve<BossPatternController>().UnregisterBoss(boss);
        _driver=boss.GetComponent<PatternLabDriver>();
        _catalog=UnityEditor.AssetDatabase.LoadAssetAtPath<PatternLabCatalog>("Assets/WorkSpace/USW/Data/BossPatterns/Library/PatternCatalog.asset");
        var grid=scope.Container.Resolve<GridManager>();_cells=grid.AllCells().ToArray();
        var time=scope.Container.Resolve<TimeScaleService>();var pause=scope.Container.Resolve<FieldPauseVisuals>();
        var owner=new object();bool priorEnabled=_driver.enabled;
        time.Pause(owner);_driver.enabled=false;
        try
        {
            Check(_catalog.Patterns.Length==8,"eight authored independent mechanics");
            Check(_catalog.Patterns.Select(p=>p.Mechanic).Distinct().Count()==8,"eight different interaction rules");
            Check(_catalog.Patterns.Count(p=>p.AllowChiefCounter)==3,"three interruptible mechanics, five positional mechanics");
            var p=Begin(0);var a=_driver.SourceUnit;var b=_driver.PartnerUnit;
            Check(a!=null&&b!=null&&a!=b,"tether chooses two distinct placed units");
            Check(!_driver.TryCounter(),"tether requires separation rather than chief counter");
            var far=_cells.Where(c=>c.IsAvailable&&Vector2Int.Distance(c.GridPosition,b.currentCell.GridPosition)>=p.TetherDistance).First();
            Move(a,far);_driver.Advance(.01f);
            Check(_driver.Status.Contains("절단 성공"),"moving linked units apart actually breaks tether");
            Check(_driver.LastHitCount==0&&!a.IsStunned&&!b.IsStunned,"successful tether separation hurts neither unit");
            p=Begin(0);a=_driver.SourceUnit;b=_driver.PartnerUnit;Release(p);
            Check(a.IsStunned&&b.IsStunned&&_driver.LastHitCount==2,"unbroken tether stuns exactly the linked pair");
            Check(_cells.Where(c=>c.OccupyingUnit!=a&&c.OccupyingUnit!=b).All(c=>c.OccupyingUnit==null||!c.OccupyingUnit.IsStunned),"tether does not hit unrelated units");

            p=Begin(1);Check(_driver.IsCounterable&&_driver.TryCounter(),"wall can be interrupted before release");
            Check(_driver.LastHitCount==0,"countered wall has no impacts");
            p=Begin(1);int gap=Field<int>("_gapRow");Release(p);
            Check(_driver.LastHitCount==0,"wall does not hit before reaching first cell center");
            _driver.Advance(p.ActiveDuration/(grid.Columns*2f)+.01f);
            Check(_cells.Where(c=>c.GridPosition.x==0&&c.GridPosition.y!=gap&&c.OccupyingUnit!=null).All(c=>c.OccupyingUnit.IsStunned),"moving wall hits crossed unsafe column");
            Check(_cells.Where(c=>c.GridPosition.x==grid.Columns-1).All(c=>c.OccupyingUnit==null||!c.OccupyingUnit.IsStunned),"future columns remain unharmed");
            _driver.Advance(p.ActiveDuration);
            Check(_cells.Where(c=>c.GridPosition.y==gap).All(c=>c.OccupyingUnit==null||!c.OccupyingUnit.IsStunned),"wall gap remains safe through whole sweep");
            Check(Field<int>("_nextColumn")==grid.Columns,"wall resolves each column once");

            p=Begin(2);Check(_driver.SafeCells.Count==p.SafeColumns*grid.Rows,"shelter has authored safe capacity");
            Release(p);
            Check(_driver.SafeCells.All(c=>c.OccupyingUnit==null||!c.OccupyingUnit.IsStunned),"shelter protects occupants");
            Check(_driver.MarkedCells.Where(c=>c.OccupyingUnit!=null).All(c=>c.OccupyingUnit.IsStunned),"storm actually hits outside shelter");

            p=Begin(3);a=_driver.SourceUnit;var oldOrigin=a.currentCell;
            var bombDestination=_cells.Where(c=>c.IsAvailable&&Vector2Int.Distance(c.GridPosition,oldOrigin.GridPosition)>2).First();
            Move(a,bombDestination);_driver.Advance(.1f);
            Check(_driver.MarkedCells.Contains(bombDestination),"bomb follows carrier's new committed cell");
            Check(!_driver.MarkedCells.Contains(oldOrigin),"old bomb footprint is no longer targeted");
            var marked=_driver.MarkedCells.ToArray();Release(p,p.Windup-.1f);
            Check(!a.IsStunned,"bomb carrier is not punished for carrying");
            Check(marked.Where(c=>c.OccupyingUnit!=null&&c.OccupyingUnit!=a).All(c=>c.OccupyingUnit.IsStunned),"carrier bomb hits current neighboring allies");
            Check(_cells.Where(c=>!marked.Contains(c)).All(c=>c.OccupyingUnit==null||!c.OccupyingUnit.IsStunned),"carrier bomb leaves distant allies safe");

            p=Begin(4);a=_driver.SourceUnit;var beamBefore=Field<Vector3>("_beamEnd");
            var bait=_cells.Where(c=>c.IsAvailable&&Vector2Int.Distance(c.GridPosition,a.currentCell.GridPosition)>2).First();
            Move(a,bait);_driver.Advance(.2f);
            Check(Vector3.Distance(beamBefore,Field<Vector3>("_beamEnd"))>.1f,"laser follows bait before locking");
            _driver.Advance(p.LockTime);Check(Field<bool>("_aimLocked"),"laser locks at authored time");
            var locked=Field<Vector3>("_beamEnd");var beamCells=_driver.MarkedCells.ToArray();
            var escape=_cells.First(c=>c.IsAvailable&&!beamCells.Contains(c));Move(a,escape);_driver.Advance(.1f);
            Check(Vector3.Distance(locked,Field<Vector3>("_beamEnd"))<.0001f,"laser stops tracking after lock");
            Release(p,p.Windup-_driver.Elapsed);
            Check(!a.IsStunned,"bait unit escapes after aim lock");
            Check(beamCells.Where(c=>c.OccupyingUnit!=null).All(c=>c.OccupyingUnit.IsStunned),"locked beam hits remaining line occupants");

            p=Begin(5);var remembered=_driver.SafeCells.ToArray();
            Check(remembered.Length==p.MemorySafeCount&&remembered.Distinct().Count()==remembered.Length,"memory tiles are unique authored safe cells");
            _driver.Advance(p.LockTime+.01f);Check(Field<bool>("_memoryHidden"),"memory clue disappears before impact");
            Check(remembered.SequenceEqual(_driver.SafeCells),"hidden safe tiles remain unchanged");
            Release(p,p.Windup-_driver.Elapsed);
            Check(remembered.All(c=>Mathf.Approximately(c.Model.SpeedModifier,1f)),"remembered tiles protect attack speed");
            Check(_driver.MarkedCells.All(c=>Mathf.Approximately(c.Model.SpeedModifier,1f/(1f-p.SlowRate))),"forgotten tiles get exact authored slow");
            _driver.Advance(p.EffectDuration+.1f);Check(_cells.All(c=>Mathf.Approximately(c.Model.SpeedModifier,1f)),"memory slow expires exactly to base multiplier");

            p=Begin(6);var relay=_driver.RelayCell;
            Check(relay!=null&&relay.IsAvailable,"relay chooses an actually empty placeable cell");
            Check(!_driver.TryCounter(),"relay requires occupation rather than chief counter");
            a=_cells.First(c=>c.OccupyingUnit!=null).OccupyingUnit;Move(a,relay);_driver.Advance(.01f);
            Check(_driver.Status.Contains("점령 성공")&&_driver.LastHitCount==0,"occupying relay cancels whole attack");
            p=Begin(6);Release(p);
            Check(_driver.LastHitCount==_cells.Count(c=>c.OccupyingUnit!=null),"unclaimed relay releases board-wide punishment");

            p=Begin(7);Release(p);Check(_driver.LastHitCount==0,"red light does not punish stationary units");
            a=_cells.First(c=>c.OccupyingUnit!=null).OccupyingUnit;
            var redDestination=_cells.First(c=>c.IsAvailable);Move(a,redDestination);_driver.Advance(.01f);
            Check(a.IsStunned&&_driver.LastHitCount==1,"red light detects actual cell movement");
            Check(_cells.Where(c=>c.OccupyingUnit!=a).All(c=>c.OccupyingUnit==null||!c.OccupyingUnit.IsStunned),"red light leaves stationary allies alone");
            _driver.Advance(p.ActiveDuration);
            Check(_driver.Status.Contains("신호 종료"),"movement restriction ends after authored active duration");

            p=Begin(4);float priorElapsed=_driver.Elapsed;
            var holds=(HashSet<object>)typeof(FieldPauseVisuals).GetField("_holdOwners",Private).GetValue(pause);holds.Add(owner);
            try{_driver.Advance(1f);}finally{holds.Remove(owner);}
            Check(Mathf.Approximately(priorElapsed,_driver.Elapsed),"selection hold freezes new mechanic clocks");
            Check(_driver.TryCounter(),"laser still counterable after paused time");
            _driver.ResetAll();
            Check(!_driver.IsBusy&&_driver.MarkedCells.Count==0&&_driver.SafeCells.Count==0,"reset removes all telegraph and target state");
            Check(_cells.All(c=>c.OccupyingUnit==null||!c.OccupyingUnit.IsStunned),"reset clears mechanic-owned stun");
            var material=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/WorkSpace/USW/Data/BossPatterns/Hard/Telegraph.mat");
            Check(material!=null&&!UnityEditor.ShaderUtil.ShaderHasError(material.shader),"shared URP geometry shader compiles");
            return Results.Count+" checks PASS";
        }
        finally
        {
            _driver.ResetAll();RestoreMoves();_driver.enabled=priorEnabled;time.Release(owner);
            _driver.ResetAll();
            Directory.CreateDirectory("outputs/hard-develop-20261022/library-regression");File.WriteAllLines("outputs/hard-develop-20261022/library-regression/live-checks.txt",Results);
            Debug.Log("[PatternLibraryChecks] "+Results.Count+" checks recorded.");
        }
    }
}
