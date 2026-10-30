using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using LitJson;

public enum UnitBonusValueType { StatModifierBonus,EveryNthCycleBonus,HackingAttackSpeedBonus,ChiefAttackSpeedBonus,RallyVulnerabilityBonus,HackingProductionBonus,HackingConsumptionBonus }
public static class UnitBonusValueContracts
{
 static string Text(JsonData r,string k)=>r.ContainsKey(k)&&r[k]!=null?r[k].ToString():"";
 public static Dictionary<string,decimal> Arguments(JsonData row)
 {
  string type=Text(row,"EffectType");
  if(!Enum.TryParse<UnitBonusValueType>(type,out var kind)||kind.ToString()!=type)throw new FormatException("Unknown Value contract: "+type);
  string[] names;
  switch(kind){ case UnitBonusValueType.StatModifierBonus: names = new[] {"Value"}; break;
case UnitBonusValueType.EveryNthCycleBonus: names = new[] {"Every","DamageBonusPercent","ChargeReductionPercent"}; break;
case UnitBonusValueType.HackingAttackSpeedBonus: names = new[] {"Value","Duration"}; break;
case UnitBonusValueType.ChiefAttackSpeedBonus: names = new[] {"Value","Duration"}; break;
case UnitBonusValueType.RallyVulnerabilityBonus: names = new[] {"Value","Duration"}; break;
case UnitBonusValueType.HackingProductionBonus: names = new[] {"Normal","Rare","Epic","Legend"}; break;
case UnitBonusValueType.HackingConsumptionBonus: names = new[] {"Normal","Rare","Epic","Legend"}; break; default: throw new FormatException("Value contract"); }
  string[] raw=kind==UnitBonusValueType.EveryNthCycleBonus && Text(row,"Every")!="" ? new[]{Text(row,"Every"),Text(row,"DamageBonusPercent"),Text(row,"ChargeReductionPercent")} : Text(row,"Value").Split(',');
  if(raw.Length!=names.Length)throw new FormatException(type+": expected "+names.Length+" values ("+string.Join(",",names)+")");
  var result=new Dictionary<string,decimal>();
  for(int i=0;i<names.Length;i++){
   string input=raw[i].Trim();
   if(!Regex.IsMatch(input,@"^\+?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?$")||!decimal.TryParse(input,NumberStyles.Float,CultureInfo.InvariantCulture,out var v)||v<0||v>9007199254740991m)throw new FormatException("Invalid Value: "+names[i]);
   if(kind==UnitBonusValueType.EveryNthCycleBonus && (i==0&&(v<1||v!=decimal.Truncate(v))||i==2&&v>=100))throw new FormatException("Cycle Value range");
   if((kind==UnitBonusValueType.HackingProductionBonus||kind==UnitBonusValueType.HackingConsumptionBonus)&&(v>10000||v!=decimal.Truncate(v)))throw new FormatException("Tier Value range");
   result.Add(names[i],v);
  }return result;
 }
 public static List<JsonData> Project(JsonData effect)
 {
  var args=Arguments(effect);string type=Text(effect,"EffectType");var result=new List<JsonData>();
  if(type=="EveryNthCycleBonus"){
   var row=JsonMapper.ToObject(effect.ToJson());row["Value"]="0";foreach(var arg in args)row[arg.Key]=arg.Value.ToString(CultureInfo.InvariantCulture);
   if(Text(row,"CycleId")=="")row["CycleId"]="basic_skill";result.Add(row);return result;
  }
  if(type=="StatModifierBonus"){var row=JsonMapper.ToObject(effect.ToJson());row["Value"]=args["Value"].ToString(CultureInfo.InvariantCulture);result.Add(row);return result;}
  switch(type){ case "HackingAttackSpeedBonus":Add(result,"HackingPostSpeed","Percent",args["Value"]);Add(result,"HackingPostDuration","Flat",args["Duration"]);break;
case "ChiefAttackSpeedBonus":Add(result,"ChiefSpeed","Percent",args["Value"]);Add(result,"ChiefSpeedDuration","Flat",args["Duration"]);break;
case "RallyVulnerabilityBonus":Add(result,"ChiefDamageTaken","Percent",args["Value"]);Add(result,"ChiefDamageTakenDuration","Flat",args["Duration"]);break;
case "HackingProductionBonus":Add(result,"HackingProducedNormal","Flat",args["Normal"]);Add(result,"HackingProducedRare","Flat",args["Rare"]);Add(result,"HackingProducedEpic","Flat",args["Epic"]);Add(result,"HackingProducedLegend","Flat",args["Legend"]);break;
case "HackingConsumptionBonus":Add(result,"HackingConsumedNormal","Flat",args["Normal"]);Add(result,"HackingConsumedRare","Flat",args["Rare"]);Add(result,"HackingConsumedEpic","Flat",args["Epic"]);Add(result,"HackingConsumedLegend","Flat",args["Legend"]);break; default:throw new FormatException("Value projection"); }
  return result;
 }
 static void Add(List<JsonData> list,string stat,string mode,decimal value){var row=new JsonData();row["EffectType"]="StatModifierBonus";row["Stat"]=stat;row["ValueMode"]=mode;row["Value"]=value.ToString(CultureInfo.InvariantCulture);list.Add(row);}
}
