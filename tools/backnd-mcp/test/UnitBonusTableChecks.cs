using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using LitJson;
using UnityEditor;
using UnityEngine;
public static class UnitBonusTableChecks {
 static readonly List<UnityEngine.Object> owned=new();
 static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static void Near(float value,float expected,string message)=>Check(Math.Abs(value-expected)<0.002f,message+": "+value+" != "+expected);
 static void Set(object o,string field,object value){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(field,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);if(f!=null){f.SetValue(o,value);return;}}throw new Exception(field);}
 static object Call(object o,string method,params object[] args){for(var t=o.GetType();t!=null;t=t.BaseType){var m=t.GetMethod(method,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly);if(m!=null)return m.Invoke(o,args);}throw new Exception(method);}
 static T Component<T>() where T:Component {var go=new GameObject("IsolatedHtmlBonus_"+typeof(T).Name);go.SetActive(false);owned.Add(go);return go.AddComponent<T>();}
 static UnitLevelBonusController Bonuses(Dictionary<string,List<UnitLevelBonusDefinition>> parsed,string key,int level=15)=>new(parsed[key],level);
 static T Unit<T>(Dictionary<string,List<UnitLevelBonusDefinition>> parsed,string key,UnitDependencies deps) where T:UnitBase {
  var unit=Component<T>();unit.unitData=AssetDatabase.LoadAssetAtPath<UnitData>("Assets/WorkSpace/USW/Data/DroneUnits/"+key+".asset");Check(unit.unitData!=null,"SO "+key);
  Set(unit,"<LevelBonuses>k__BackingField",Bonuses(parsed,key));Set(unit,"_deps",deps);
  var stats=unit.gameObject.AddComponent<UnitStatsModifier>();var combat=unit.gameObject.AddComponent<UnitCombatComponent>();var resource=unit.gameObject.AddComponent<UnitResourceComponent>();
  stats.Init(unit,deps);resource.Init(unit,deps);combat.Init(unit,deps,stats,resource);Set(unit,"_stats",stats);Set(unit,"_combat",combat);Set(unit,"_resource",resource);
  return unit;
 }
 public static string Run()=>RunRows(JsonMapper.ToObject(File.ReadAllText("outputs/bonus-description-rows.json")));
 public static async System.Threading.Tasks.Task<string> RunRemote(){
  var rows=await GGD.BackendCharts.BackendChartReader.ReadAsync(UnitBonusChart.ChartName,System.Threading.CancellationToken.None);
  return "Downloaded server chart: "+RunRows(rows);
 }
 static string RunRows(JsonData rows){
  checks=0;var parsed=UnitBonusChart.Parse(rows);var texts=UnitBonusChart.ParsePresentations(rows);
  Check(parsed.Count==6&&texts.Count==30,"6 actors/30 descriptions");foreach(var list in parsed.Values){Check(list.Count==5,"Five milestones");foreach(int level in new[]{3,6,9,12,15})Check(list.Exists(x=>x.Level==level),"Milestone "+level);}
  var field=typeof(UnitBonusChart).GetField("definitions",BindingFlags.Static|BindingFlags.NonPublic);var previous=field.GetValue(null);field.SetValue(null,parsed);
  AlphanActiveSkill chief=null;float oldScale=Time.timeScale;Time.timeScale=1;
  try {
   var betan=Bonuses(parsed,"UnitData_Betan");Near(betan.GetStat(UnitBonusStat.AttackPercent),.05f,"Betan atk");Near(betan.GetStat(UnitBonusStat.CriticalDamagePoints),.5f,"Betan crit");Near(betan.CompleteCycle("basic_skill").DamageMultiplier,1,"Bomb1");var second=betan.CompleteCycle("basic_skill");Near(second.DamageMultiplier,1.4f,"Bomb2");Near(second.ChargeTimeMultiplier,.7f,"Charge2");Near(Bonuses(parsed,"UnitData_Betan",2).GetStat(UnitBonusStat.AttackPercent),0,"Locked");
   var replacement=new List<UnitLevelBonusDefinition>{new UnitLevelBonusDefinition{Level=15,Parts=new List<UnitLevelBonusPart>{new StatModifierBonus{Stat=UnitBonusStat.FoodFifthMultiplier,Value=1.5f}}},new UnitLevelBonusDefinition{Level=12,Parts=new List<UnitLevelBonusPart>{new StatModifierBonus{Stat=UnitBonusStat.FoodFifthMultiplier,Value=2f}}}};
   Near(new UnitLevelBonusController(replacement,15).GetStat(UnitBonusStat.FoodFifthMultiplier),1.5f,"Higher level replaces independently of row order");
   var currency=Component<CurrencyManager>();var timer=Component<TimerController>();timer.StartTimer(100);
   var deps=new UnitDependencies{CurrencyManager=currency,Timer=timer};
   var delta=Unit<Drone_Deltan>(parsed,"UnitData_Deltan",deps);
   var gamma=Unit<Drone_Gamman>(parsed,"UnitData_Gamman",deps);
   foreach(Tier tier in TierUtil.All){delta.currentTier=tier;gamma.currentTier=tier;Near(delta.RequiredAttacks,Math.Max(1,delta.unitData.Hacking.AttacksToCharge.Get(tier)-1),"Delta charge "+tier);Near(delta.LevelProductionBonus,new[]{1,1,2,3}[(int)tier],"Delta production "+tier);Near(gamma.LevelConsumptionBonus,new[]{1,1,2,4}[(int)tier],"Gamma consume "+tier);}
   Near(gamma.StackDamageMultiplier,1.2f,"Only stack damage");gamma.currentTier=Tier.Normal;Near(gamma.GetComponent<UnitStatsModifier>().GetCurrentSkillInterval(),gamma.unitData.skillCooldown.Get(Tier.Normal)*.9f,"Gamma cooldown");Near(gamma.LevelStat(UnitBonusStat.CriticalChancePoints),.1f,"Gamma crit chance");
   delta.currentTier=Tier.Normal;float before=delta.GetComponent<UnitStatsModifier>().GetCurrentAttackInterval();Set(delta,"_postHackingRemaining",2f);Near(delta.GetComponent<UnitStatsModifier>().GetCurrentAttackInterval(),before/1.15f,"Delta post-hack speed");Set(delta,"_postHackingRemaining",0f);Near(delta.GetComponent<UnitStatsModifier>().GetCurrentAttackInterval(),before,"Delta expired");
   var hacking=Component<DroneHackingRuntime>();hacking.Configure(delta.unitData.Hacking);hacking.SetCapacityBonus(delta,10);hacking.SetCapacityBonus(gamma,10);Near(hacking.Ledger.Capacity,delta.unitData.Hacking.Capacity+10,"Shared capacity does not stack");hacking.Ledger.Add(60);var reservation=hacking.Ledger.Reserve(20);hacking.SetCapacityBonus(delta,0);hacking.SetCapacityBonus(gamma,0);Check(hacking.Ledger.Commit(reservation),"Resize preserves in-flight reservation");Check(hacking.Ledger.Available+hacking.Ledger.Reserved<=hacking.Ledger.Capacity,"Capacity bounded");
   var shrinking=new HackingStackLedger();shrinking.Reset(60);shrinking.Add(60);var flying=shrinking.Reserve(60);shrinking.Resize(50);Check(shrinking.Add(10)==0,"Over-reserved capacity accepts no new stacks");Check(shrinking.Refund(flying)&&shrinking.Available==50&&shrinking.Reserved==0,"Capacity decrease refunds at most new cap");
   var boss=Component<BossNormal>();boss.Init(1000000);var dis=Unit<Disigman>(parsed,"UnitData_Disigman",deps);Call(dis,"OnInitialized",deps);
   var cell=Component<GridCell>();Set(cell,"<Model>k__BackingField",new GridCellModel());Set(dis,"<currentCell>k__BackingField",cell);Set(dis,"<Boss>k__BackingField",boss);dis.gameObject.SetActive(true);
   Set(dis,"_chargedAttacks",dis.RequiredAttacks);float start=timer.RemainingTime;Check(dis.TryCast(),"Real recovery cast accepted");Near(timer.RemainingTime-start,dis.unitData.Disigman.SecondsRecovered.Get(Tier.Normal)*1.1f,"Recovered time10%");Check(dis.RecoveryShotReady,"Shot armed");
   // A second recovery refreshes one stored shot; it does not accumulate two buffs.
   Set(dis,"_chargedAttacks",dis.RequiredAttacks);Check(dis.TryCast(),"Second recovery");float multiplier=dis.ConsumeRecoveryShot(boss);Near(multiplier,1.3f,"One recovery shot");Near(dis.ConsumeRecoveryShot(boss),1,"Only consumed once");
   var statsDis=dis.GetComponent<UnitStatsModifier>();int basic=0;var replay=new BasicAttackReplay(dis,boss){RegularDamageMultiplier=multiplier};
   Call(dis.Combat,"ApplyBasicHit",replay,(Action)(()=>basic=dis.ComputeAttackDamageFrom(dis.GetUpgradedAtk())));
   Near(basic,DamageCalculator.ApplyRounding(dis.GetUpgradedAtk()*1.15f*1.3f),"Delayed basic impact includes 15% and30%");
   int skill=dis.ComputeAttackDamageFrom(dis.GetUpgradedAtk()*3);Near(skill,DamageCalculator.ApplyRounding(dis.GetUpgradedAtk()*3),"Skill excludes basic-only bonuses");Check(!dis.Combat.IsExecutingRegularBasic,"Context restored after impact");
   Set(dis,"_chargedAttacks",dis.RequiredAttacks);Check(dis.TryCast(),"Third recovery");var nextBoss=Component<BossNormal>();nextBoss.Init(1000);Near(dis.ConsumeRecoveryShot(nextBoss),1,"No transfer to next boss");
   var zelta=Unit<Drone_Zeltan>(parsed,"UnitData_Zeltan",deps);Call(zelta,"OnInitialized",deps);Set(zelta,"<currentCell>k__BackingField",cell);var food=zelta.GetComponent<UnitResourceComponent>();
   float own=zelta.unitData.foodProduction.Get(Tier.Normal)*1.15f;Near(zelta.GetBaseFoodPerSecond(),own,"Zelta own amount15%");Near(food.CurrentFoodProductionPerSecond,own*1.1f,"Own speed10%");
   float money=currency.Currency;food.TickFoodProduction(5f/1.1f+.00001f);Near(currency.Currency-money,own*6.5f,"5 own ticks = 4 normal +2.5x, not5x");
   money=currency.Currency;Call(zelta,"OnBossKilled");Near(currency.Currency-money,own*1.1f*5f,"Boss reward5 seconds");Near(zelta.NextFoodPayoutMultiplier(),1,"Boss reward does not advance own counter");
   var selector=Component<ChieftainSelection>();var drones=Component<DroneManager>();chief=new AlphanActiveSkill(selector,new[]{drones},null,null,null);chief.Initialize();chief.ConfigureLevel(new UnitLevelSnapshot(new[]{new KeyValuePair<string,int>("AlphanChief",15)}));
   var chiefData=AssetDatabase.LoadAssetAtPath<AlphanSkillData>("Assets/WorkSpace/USW/Data/DroneUnits/AlphanActiveSkillData.asset");chief.Configure(chiefData,null);Near(chief.CooldownSeconds,chiefData.CooldownSeconds*.9f,"Chief cooldown10%");Check(chief.AvailableCharges==1,"Battle starts ready");Near(chief.GlobalAttackBonus,.05f,"Global atk5%");
   Set(chief,"_speedRemaining",10f);Near(chief.AttackSpeedBonus,.2f,"Chief speed20%");chief.Advance(9);Near(chief.AttackSpeedBonus,.2f,"9seconds active");chief.Advance(1);Near(chief.AttackSpeedBonus,0,"10seconds expired even with full charge");
   Call(chief,"OnRallyImpact",boss);Near((float)boss.Debuffs.DamageTakenMultiplier,1.1f,"Rally impact vulnerability");boss.Debuffs.Advance(4);Call(chief,"OnRallyImpact",boss);Near((float)boss.Debuffs.DamageTakenMultiplier,1.1f,"Rally refresh no stacking");boss.Debuffs.Advance(8);Near((float)boss.Debuffs.DamageTakenMultiplier,1.1f,"Refreshed duration");boss.Debuffs.Advance(9);Near((float)boss.Debuffs.DamageTakenMultiplier,1,"Vulnerability expires5seconds");
   boss.Debuffs.Apply(new DebuffDefinition(190016,"coexisting",DebuffKind.DamageTakenIncrease,"coexisting",10,1,damageMultiplier:1.2m),new DebuffApplyContext(0,1,0,defenseReduction:.25));Call(chief,"OnRallyImpact",boss);Near((float)boss.Debuffs.DamageTakenMultiplier,1.32f,"Existing vulnerability coexists");Near((float)boss.Debuffs.DefenseReduction,.25f,"Existing defense reduction retained");
   var bad=JsonMapper.ToObject(rows.ToJson());bad[0]["EffectType"]="ArbitraryClass";bool rejected=false;try{UnitBonusChart.Parse(bad);}catch(FormatException){rejected=true;}Check(rejected,"Unknown class rejected");
   return "PASS "+checks+" assertions: latest HTML 6 actors/30 milestones; Betan cycles; Deltan 4tiers/shared cap; Gamman stack-only bonus; real Disigman recovery and delayed basic/skill separation; real Zeltan tick and boss currency; chief ready/cooldown/speed/debuff expiry. Isolated components in current test scene, no account writes.";
  } finally {chief?.Dispose();field.SetValue(null,previous);Time.timeScale=oldScale;for(int i=owned.Count-1;i>=0;i--)if(owned[i]!=null)UnityEngine.Object.DestroyImmediate(owned[i]);owned.Clear();}
 }
}
