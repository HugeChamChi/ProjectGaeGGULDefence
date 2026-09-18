/// <summary>실제 드론 공격/토템/등급 변경 경로를 Preview Scene에서 검증한다.</summary>
public static class DroneTotemIntegrationChecks
{
/// <summary>풀 생성과 비행 완료만 테스트 대역을 사용하며 실제 전투 콜백을 실행한다.</summary>
public static string Run()
{
if (UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Requires Edit Mode");
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var assets = new System.Collections.Generic.List<UnityEngine.Object>();
var totems = new System.Collections.Generic.List<TotemBase>();
var results = new System.Collections.Generic.List<string>();
void Check(bool ok, string message) { if (!ok) throw new System.Exception(message); results.Add("PASS " + message); }
var random = UnityEngine.Random.state;
T Comp<T>() where T : UnityEngine.Component {
    var go = new UnityEngine.GameObject(typeof(T).Name); go.SetActive(false);
    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    return go.AddComponent<T>();
}
T Data<T>() where T : UnityEngine.ScriptableObject {
    var a = UnityEngine.ScriptableObject.CreateInstance<T>(); assets.Add(a); return a;
}
System.Reflection.FieldInfo Field(object o, string name) {
    for (var t = o.GetType(); t != null; t = t.BaseType) {
        var f = t.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (f != null) return f;
    }
    throw new System.Exception("No field " + name);
}
void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
void Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(o, args);
try {
    var grid = Comp<GridManager>(); var config = Data<GameConfig>(); config.gridColumns = config.gridRows = 5; Set(grid, "config", config);
    var cells = new GridCell[5,5];
    for (int x=0;x<5;x++) for (int y=0;y<5;y++) {
        var c=Comp<GridCell>(); Set(c,"<Model>k__BackingField",new GridCellModel()); c.Init(new UnityEngine.Vector2Int(x,y)); cells[x,y]=c;
    }
    Set(grid,"_grid",cells);
    var buffs=Comp<TotemBuffManager>(); Set(buffs,"_gridManager",grid);
    var bosses=Comp<BossManager>(); var boss=Comp<BossNormal>(); boss.gameObject.AddComponent<BossAreaTarget>();
    var debuffs=UnityEngine.Resources.Load<DebuffSettings>("DebuffSettings");
    boss.ConfigureDebuffs(new DebuffCatalog(debuffs),debuffs,null,0,null); boss.Init(100000);
    ((System.Collections.Generic.List<BossBase>)Field(bosses,"_currentBosses").GetValue(bosses)).Add(boss);
    var pool=Comp<ProjectilePool>(); var shots=new System.Collections.Generic.List<TotemCheckProjectile>();
    Set(pool,"_pool",new UnityEngine.Pool.ObjectPool<Projectile>(()=> {var p=Comp<TotemCheckProjectile>();shots.Add(p);return p;}));
    int Launches() {int n=0;foreach(var s in shots)n+=s.LaunchCount;return n;}
    var owner=Comp<Drone_Betan>(); owner.unitData=Data<UnitData>(); owner.unitData.atk.normal=100;owner.unitData.atk.rare=200;
    owner.unitData.attackSpeed.normal=1;owner.unitData.attackSpeed.rare=0.5f;
    owner.unitData.maxDroneCount.normal=1;owner.unitData.maxDroneCount.rare=2;
    owner.onAttack=new UnityEngine.Events.UnityEvent(); int attempts=0; owner.onAttack.AddListener(()=>attempts++);
    owner.Init(new UnitDependencies {BossManager=bosses,ProjectileManager=pool,TotemBuffManager=buffs});
    cells[3,2].TryPlaceUnit(owner);Set(owner,"<currentCell>k__BackingField",cells[3,2]);
    var drone=Comp<DroneUnit>();Set(drone,"_owner",owner);Set(drone,"_bossManager",bosses);Set(drone,"_projectileManager",pool);
    ((System.Collections.Generic.List<DroneUnit>)Field(owner,"_ownedDrones").GetValue(owner)).Add(drone);
    T Place<T>(TotemData data) where T:TotemBase {
        var t=Comp<T>();Set(t,"totemData",data);Set(t,"_gridManager",grid);Set(t,"_totemBuffManager",buffs);totems.Add(t);t.OnPlaced(cells[2,2]);return t;
    }
    TotemData RangeData() {var d=Data<TotemData>();d.UseSheetData=false;d.effectRanges.Add(new TotemRelativeOffsetRange {offsets=new System.Collections.Generic.List<UnityEngine.Vector2Int>{UnityEngine.Vector2Int.right}});return d;}
    var bonusData=RangeData();bonusData.BonusProjectile.Chance=1f;
    var bonus=Place<TotemBonusProjectile>(bonusData);Set(bonus,"_bosses",bosses);Set(bonus,"_projectiles",pool);
    int before=Launches();Call(drone,"LaunchProjectile",100);
    Check(Launches()-before==2 && attempts==1,"TD1001 one bonus per drone basic attack");
    results.Add("TD1001 drone attack (chance=100%): launches="+(Launches()-before)+", owner attack events="+attempts+"; expected 2 launches and 1 event");
    before=Launches();owner.InvokeOnAttack();results.Add("TD1001 control: explicitly invoking owner attack generates "+(Launches()-before)+" bonus projectile");
    bonus.OnRemoved();
    var armorData=RangeData();int armorId=0;foreach(var d in debuffs.CreateDefinitions())if(d.Kind==DebuffKind.ArmorBreak)armorId=d.Id;
    Set(armorData,"_debuffBinding",new DebuffBinding(armorId,DebuffTrigger.AffectedUnitBasicAttackAttempt,1));
    var armor=Place<GenericBuffTotem>(armorData);Call(drone,"LaunchProjectile",100);
    Check(boss.Debuffs.Active.Count==1 && boss.Debuffs.Active[0].Stacks==1,"TD1006 one armor stack per drone attack");
    results.Add("TD1006 drone attack: active debuffs="+boss.Debuffs.Active.Count+"; expected armor stack 1");
    buffs.ApplyAttackDebuff(cells[3,2],boss);results.Add("TD1006 control: direct range routing gives stacks="+boss.Debuffs.Active[0].Stacks);armor.OnRemoved();
    var shadow=Place<TotemShadowAttack>(RangeData());before=Launches();Call(drone,"LaunchProjectile",100);Call(shadow,"Advance",0.3f);
    Check(Launches()-before==2,"TD1007 original and delayed shadow");
    var visual=shadow.GetComponentInChildren<ShadowAttackVisual>(true);
    Check(visual!=null && visual.VisualSource==drone.transform,"Shadow copies drone instead of owner");
    // Complete old shots first so only this original/shadow pair affects the following comparison.
    for(int i=0;i<shots.Count-2;i++)shots[i].Complete();
    decimal hp=boss.CurrentHp; shots[shots.Count-2].Complete(); decimal originalDamage=hp-boss.CurrentHp;
    Set(boss,"_defense",10000d); hp=boss.CurrentHp;shots[shots.Count-1].Complete();
    Check(boss.CurrentHp==hp-originalDamage && originalDamage>0,"Shadow preserves original final damage after defense changes");
    Set(boss,"_defense",0d);
    void ResetShots() { shots.Clear(); Set(pool,"_pool",new UnityEngine.Pool.ObjectPool<Projectile>(()=> {var p=Comp<TotemCheckProjectile>();shots.Add(p);return p;})); }
    ResetShots();int eventsBefore=attempts;
    Call(drone,"LaunchProjectile",100);Call(shadow,"Advance",0.3f);
    hp=boss.CurrentHp;shots[1].Complete();Check(boss.CurrentHp==hp,"Early shadow waits for original hit");
    shots[0].Complete();Check(boss.CurrentHp==hp-200m && attempts==eventsBefore+1,"Early shadow applies twice without recursive attack events");
    ResetShots();Call(drone,"LaunchProjectile",100);Call(drone,"StopAll");Call(shadow,"Advance",0.3f);
    Check(Launches()==1,"Recalled drone cancels pending shadow even if object is pooled");
    ResetShots();eventsBefore=attempts;drone.FireRallyShot();Call(shadow,"Advance",0.3f);
    Check(Launches()==1 && attempts==eventsBefore,"Rally visual does not trigger shadow or basic attack events");
    ResetShots();var overlapping=Place<TotemShadowAttack>(RangeData());Call(drone,"LaunchProjectile",100);
    Call(shadow,"Advance",0.3f);Call(overlapping,"Advance",0.3f);
    Check(Launches()==2,"Overlapping shadow sources replay only once");overlapping.OnRemoved();
    ResetShots();Call(drone,"LaunchProjectile",100);shadow.OnRemoved();Call(shadow,"Advance",0.3f);
    Check(Launches()==1,"Removing shadow totem cancels scheduled shots");
    bonus.OnPlaced(cells[2,2]);armor.OnPlaced(cells[2,2]);ResetShots();eventsBefore=attempts;
    int stacksBefore=boss.Debuffs.Active[0].Stacks;drone.FireRallyShot();
    Check(Launches()==1 && attempts==eventsBefore && boss.Debuffs.Active[0].Stacks==stacksBefore,"Rally triggers neither bonus projectile nor armor");
    cells[3,2].RemoveUnit();cells[4,2].TryPlaceUnit(owner);Set(owner,"<currentCell>k__BackingField",cells[4,2]);buffs.RebuildCellBuffFlags();
    ResetShots();Call(drone,"LaunchProjectile",100);
    Check(Launches()==1 && boss.Debuffs.Active[0].Stacks==stacksBefore,"Owner leaving range stops bonus and armor");
    cells[4,2].RemoveUnit();cells[3,2].TryPlaceUnit(owner);Set(owner,"<currentCell>k__BackingField",cells[3,2]);buffs.RebuildCellBuffFlags();
    var secondDrone=Comp<DroneUnit>();Set(secondDrone,"_owner",owner);Set(secondDrone,"_bossManager",bosses);Set(secondDrone,"_projectileManager",pool);
    ResetShots();Call(drone,"LaunchProjectile",100);Call(secondDrone,"LaunchProjectile",100);
    Check(Launches()==4 && boss.Debuffs.Active[0].Stacks==stacksBefore+2,"Each real drone attack triggers one bonus and one armor stack");
    bonus.OnRemoved();armor.OnRemoved();
    var statData=RangeData();statData.functions.Add(new SimpleBuffFunction {kind=StatKind.AttackPercent,amount=0.5f});statData.functions.Add(new SimpleBuffFunction {kind=StatKind.Speed,amount=-0.2f});
    var stat=Place<GenericBuffTotem>(statData);
    Check(drone.NonCriticalAttackDamage==150 && UnityEngine.Mathf.Approximately(drone.EffectiveAttackInterval,1.25f),"Drone attack and speed buffs still apply");
    results.Add("TD1009 drone actual stats: damage="+drone.NonCriticalAttackDamage+", interval="+drone.EffectiveAttackInterval+"; expected 150 / 1.25");stat.OnRemoved();
    results.Add("Stats after removal: damage="+drone.NonCriticalAttackDamage+", interval="+drone.EffectiveAttackInterval+"; expected 100 / 1");
    Check(drone.NonCriticalAttackDamage==100 && drone.EffectiveAttackInterval==1f,"Removing stat totem restores drone stats");
    var beta=Comp<CombatDroneCheckProbe>();beta.unitData=owner.unitData;beta.Init(new UnitDependencies());
    cells[3,2].RemoveUnit();cells[3,2].TryPlaceUnit(beta);Set(beta,"<currentCell>k__BackingField",cells[3,2]);
    beta.PlaceForCheck();
    Check(beta.OwnedDroneCount==1,"Betan starts with one real spawned drone");
    var tier=Place<TotemTemporaryTierBoost>(RangeData());
    Check(beta.currentTier==Tier.Rare && beta.OwnedDroneCount==2,"Temporary tier immediately spawns second Betan drone");
    tier.PaintAffectedCells();Check(beta.OwnedDroneCount==2,"Repaint does not spawn duplicate drones");
    tier.OnRemoved();Check(beta.currentTier==Tier.Normal && beta.OwnedDroneCount==1,"Removal restores Betan tier and retracts excess drone");
    tier.OnPlaced(cells[2,2]);Check(beta.OwnedDroneCount==2,"Reentry restores second drone");
    beta.OnRemoved();Check(beta.OwnedDroneCount==0,"Removing boosted owner never respawns drones during tier restoration");
    tier.OnRemoved();
    var selections=Comp<LevelUpManager>();var production=Data<LevelUpData>();production.chooseId=99120;
    DroneSelectionPresets.Configure(production,DroneSelectionKind.ExtraCombatDrone);selections.ApplyEffect(production);
    var enhanced=Comp<CombatDroneCheckProbe>();enhanced.unitData=Data<UnitData>();enhanced.unitData.maxDroneCount.rare=2;enhanced.unitData.maxDroneCount.epic=3;enhanced.unitData.maxDroneCount.legend=4;
    enhanced.currentTier=Tier.Rare;enhanced.Init(new UnitDependencies {LevelUpManager=selections});
    cells[3,2].RemoveUnit();cells[3,2].TryPlaceUnit(enhanced);Set(enhanced,"<currentCell>k__BackingField",cells[3,2]);enhanced.PlaceForCheck();
    tier.OnPlaced(cells[2,2]);Check(enhanced.currentTier==Tier.Epic && enhanced.OriginalTier==Tier.Rare && enhanced.OwnedDroneCount==3,"Temporary Epic does not unlock production card bonus");
    cells[3,2].RemoveUnit();cells[4,2].TryPlaceUnit(enhanced);Set(enhanced,"<currentCell>k__BackingField",cells[4,2]);tier.PaintAffectedCells();
    Check(enhanced.currentTier==Tier.Rare && enhanced.OwnedDroneCount==2,"Moving outside tier range immediately retracts extra drone");
    enhanced.RemoveForCheck();enhanced.currentTier=Tier.Epic;enhanced.PlaceForCheck();
    Check(enhanced.OwnedDroneCount==4,"Owned Epic retains production card extra drone");
    enhanced.TryApplyTemporaryTier(tier);Check(enhanced.OwnedDroneCount==5,"Owned Epic promoted to Legend has four plus production drone");
    enhanced.RemoveTemporaryTier(tier);Check(enhanced.OwnedDroneCount==4,"Restoring Epic preserves production card drone");
    enhanced.OnRemoved();
} finally {
    foreach(var t in totems)if(t!=null && t.IsActive)t.OnRemoved();
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
    foreach(var a in assets)if(a!=null)UnityEngine.Object.DestroyImmediate(a);
    UnityEngine.Random.state=random;
}
System.IO.File.WriteAllLines("Temp/drone-totem-integration-checks.txt",results);
return string.Join("\n",results);
}
}
