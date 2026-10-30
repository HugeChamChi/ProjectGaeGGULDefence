using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using GGD.BackendCharts;
using LitJson;
using UnityEngine;

public enum UnitBonusEffectType { StatModifierBonus, EveryNthCycleBonus }
public static class UnitBonusChart
{
    static Dictionary<string,List<UnitLevelBonusDefinition>> definitions;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public const string ChartName="DEV_UnitLevelBonuses";
#else
    public const string ChartName="LIVE_UnitLevelBonuses";
#endif
    public static bool IsLoaded=>definitions!=null;
    public static async UniTask InitializeAsync(CancellationToken token)
    {
        if(definitions!=null || !UnitBalanceRuntime.RequiresRemote)return;
        var rows=await BackendChartReader.ReadAsync(ChartName,token);
        var parsed=Parse(rows);token.ThrowIfCancellationRequested();definitions=parsed;
    }
    public static List<UnitLevelBonusDefinition> Resolve(string key,List<UnitLevelBonusDefinition> fallback)
    {
        if(definitions==null)return fallback;
        if(!definitions.TryGetValue(key,out var value))throw new InvalidOperationException("Missing bonus unit: "+key);
        return value;
    }
    public static void Clear()=>definitions=null;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()=>Clear();
    static string Text(JsonData r,string k)=>r.ContainsKey(k)&&r[k]!=null?r[k].ToString():"";
    static float Number(JsonData r,string k){if(!float.TryParse(Text(r,k),NumberStyles.Float,CultureInfo.InvariantCulture,out var v)||float.IsNaN(v)||float.IsInfinity(v)||v<0)throw new FormatException(k);return v;}
    static int Integer(JsonData r,string k){float v=Number(r,k);if(v>100000||v!=Math.Floor(v))throw new FormatException(k);return (int)v;}
    public static Dictionary<string,List<UnitLevelBonusDefinition>> Parse(JsonData rows)
    {
        if(rows==null||!rows.IsArray||rows.Count==0)throw new FormatException("Bonus rows missing");
        var result=new Dictionary<string,List<UnitLevelBonusDefinition>>();var counts=new Dictionary<string,SortedDictionary<int,UnitLevelBonusPart>>();
        foreach(JsonData row in rows){
            string key=Text(row,"UnitKey"),type=Text(row,"EffectType");int level=Integer(row,"Level"),index=Integer(row,"PartIndex");
            if(string.IsNullOrWhiteSpace(key)||level<1||level>1000)throw new FormatException("Bonus key/level");
            if(!Enum.TryParse<UnitBonusEffectType>(type,out var effect)||!Enum.IsDefined(typeof(UnitBonusEffectType),effect)||effect.ToString()!=type)throw new FormatException("EffectType");
            UnitLevelBonusPart part;
            if(effect==UnitBonusEffectType.StatModifierBonus){
                string stat=Text(row,"Stat"),mode=Text(row,"ValueMode");float value=Number(row,"Value");
                var mapped=(stat,mode) switch {
                    ("Attack","Flat")=>UnitBonusStat.AttackFlat,("Attack","Percent")=>UnitBonusStat.AttackPercent,
                    ("BasicAttackFrequency","Flat")=>UnitBonusStat.BasicAttackFrequencyFlat,("BasicAttackFrequency","Percent")=>UnitBonusStat.BasicAttackFrequencyPercent,
                    ("CriticalChance","PercentagePoints")=>UnitBonusStat.CriticalChancePoints,("CriticalDamage","PercentagePoints")=>UnitBonusStat.CriticalDamagePoints,
                    ("SkillCooldownReduction","Flat")=>UnitBonusStat.SkillCooldownFlat,("SkillCooldownReduction","Percent")=>UnitBonusStat.SkillCooldownPercent,
                    ("FoodProduction","Flat")=>UnitBonusStat.FoodFlat,("FoodProduction","Percent")=>UnitBonusStat.FoodPercent,
                    ("MaxDroneCount","Flat")=>UnitBonusStat.DroneCountFlat,_=>throw new FormatException("Stat/mode")};
                if(mapped==UnitBonusStat.DroneCountFlat&&(value!=Math.Floor(value)||value>10000)||mapped==UnitBonusStat.SkillCooldownPercent&&value>=100)throw new FormatException("Stat value");
                part=new StatModifierBonus {Stat=mapped,Value=mode=="Flat"?value:value/100f};
            }else{
                int every=Integer(row,"Every");float damage=Number(row,"DamageBonusPercent"),reduction=Number(row,"ChargeReductionPercent");
                if(key!="UnitData_Betan"||Text(row,"CycleId")!="basic_skill"||every<1||reduction>=100)throw new FormatException("Cycle parameters");
                part=new EveryNthCycleBonus{CycleId="basic_skill",Every=every,DamageBonusRatio=damage/100f,ChargeTimeMultiplier=1-reduction/100f};
            }
            if(!result.TryGetValue(key,out var list)){list=new List<UnitLevelBonusDefinition>();result.Add(key,list);}
            var definition=list.Find(x=>x.Level==level);if(definition==null){definition=new UnitLevelBonusDefinition{Level=level,Description=""};list.Add(definition);}
            string group=key+"/"+level;if(!counts.ContainsKey(group))counts.Add(group,new SortedDictionary<int,UnitLevelBonusPart>());counts[group].Add(index,part);
            string label=Text(row,"Stat") switch {"Attack"=>"공격력","BasicAttackFrequency"=>"일반 공격속도","CriticalChance"=>"치명타 확률","CriticalDamage"=>"치명타 피해","SkillCooldownReduction"=>"스킬 쿨타임 감소","FoodProduction"=>"초당 식량 생산","MaxDroneCount"=>"최대 드론 수",_=>""};
            string suffix=Text(row,"ValueMode") switch {"Percent"=>"%","PercentagePoints"=>"%p",_=>""};
            string description=part is EveryNthCycleBonus ? Text(row,"Every")+"번째 기본 자폭 피해 +"+Text(row,"DamageBonusPercent")+"%, 충전시간 -"+Text(row,"ChargeReductionPercent")+"%" : label+" +"+Text(row,"Value")+suffix;
            definition.Description+=(definition.Description.Length>0?", ":"")+description;
        }
        foreach(var pair in result)foreach(var definition in pair.Value){var ordered=counts[pair.Key+"/"+definition.Level];int i=0;foreach(var part in ordered){if(part.Key!=i++)throw new FormatException("PartIndex gap");definition.Parts.Add(part.Value);}new UnitLevelBonusController(new[]{definition},definition.Level);}
        return result;
    }
}
