using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using LitJson;
using UnityEditor;
using UnityEngine;
public static class UnitBonusTableChecks {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Near(float value,float expected,string message)=>Check(Math.Abs(value-expected)<0.001f,message+": "+value);
 public static string Run(){
  var rows=JsonMapper.ToObject(File.ReadAllText("outputs/readable-UnitLevelBonuses.json"));
  var parsed=UnitBonusChart.Parse(rows);int count=0;foreach(var list in parsed.Values)count+=list.Count;Check(parsed.Count==9&&count==45,"Definition counts");
  var betan=new UnitLevelBonusController(parsed["UnitData_Betan"],15);Near(betan.GetStat(UnitBonusStat.AttackPercent),.05f,"Betan attack");Near(betan.GetStat(UnitBonusStat.CriticalDamagePoints),.5f,"Betan critical");
  Near(betan.CompleteCycle("basic_skill").DamageMultiplier,1,"First cycle");var second=betan.CompleteCycle("basic_skill");Near(second.DamageMultiplier,1.4f,"Second cycle");Near(second.ChargeTimeMultiplier,.7f,"Charge");
  var locked=new UnitLevelBonusController(parsed["UnitData_Betan"],2);Near(locked.GetStat(UnitBonusStat.AttackPercent),0,"Level2 locked");
  var go=new GameObject("IsolatedBonusCheck");go.SetActive(false);var data=ScriptableObject.CreateInstance<UnitData>();
  try {
   data.atk=new PerTierFloat();data.atk.normal=100;data.attackSpeed=new PerTierFloat();data.attackSpeed.normal=2;data.skillCooldown=new PerTierFloat();data.skillCooldown.normal=10;data.foodProduction=new PerTierFloat();data.foodProduction.normal=4;
   var unit=go.AddComponent<FrogWarrior>();unit.unitData=data;unit.currentTier=Tier.Normal;
   var property=typeof(UnitBase).GetProperty("LevelBonuses",BindingFlags.Public|BindingFlags.Instance);
   property.SetValue(unit,betan);var stats=go.AddComponent<UnitStatsModifier>();stats.Init(unit,new UnitDependencies());Near(stats.GetNonCriticalAttackDamage(),105,"Combat attack");Near(stats.GetCurrentAttackInterval(),1f/2.1f,"Combat speed");
   property.SetValue(unit,new UnitLevelBonusController(parsed["UnitData_Zeltan"],15));Near(unit.GetBaseFoodPerSecond(),7.8f,"Food (4+2)*1.3");
   property.SetValue(unit,new UnitLevelBonusController(parsed["UnitData_Disigman"],15));Near(stats.GetCurrentSkillInterval(),9.5f,"Skill cooldown");
   var deltan=new UnitLevelBonusController(parsed["UnitData_Deltan"],15);Near(deltan.GetStat(UnitBonusStat.DroneCountFlat),2,"Drone capacity increase");
  }finally{UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(data);}
  var bad=JsonMapper.ToObject(rows.ToJson());bad[0]["EffectType"]="ArbitraryClass";bool rejected=false;try{UnitBonusChart.Parse(bad);}catch(FormatException){rejected=true;}Check(rejected,"Unknown effect accepted");
  return "PASS 9 units/45 milestones; level2 lock; Betan cycle1/2; combat attack105 and interval1/2.1; food7.8; cooldown9.5; drone bonus2; unknown effect rejected. Isolated levels, no account mutation.";
 }
}
