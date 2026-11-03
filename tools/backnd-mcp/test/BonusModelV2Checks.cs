using System;
using System.IO;
using System.Collections.Generic;
using LitJson;
public static class BonusModelV2Checks {
 static int count;
 static void Check(bool value,string label){count++;if(!value)throw new Exception(label);}
 static JsonData Copy(JsonData data)=>JsonMapper.ToObject(data.ToJson());
 static JsonData First(JsonData rows,string type){foreach(JsonData row in rows)if(row["RecordType"].ToString()==type)return row;throw new Exception(type);}
 static void Reject(JsonData rows,string name){bool rejected=false;try{UnitBonusChart.Parse(rows);UnitBonusChart.ParsePresentations(rows);}catch(FormatException){rejected=true;}catch(ArgumentException){rejected=true;}Check(rejected,name);}
 public static string Run(){
  count=0;var rows=JsonMapper.ToObject(File.ReadAllText("outputs/bonus-description-rows.json"));var parts=UnitBonusChart.Parse(rows);var texts=UnitBonusChart.ParsePresentations(rows);
  Check(parts.Count==6&&texts.Count==30,"30 bonus definitions");Check(parts["UnitData_Deltan"].Find(b=>b.Level==15).BonusId=="deltan_lv15","Runtime identity");
  var modified=JsonMapper.ToObject(File.ReadAllText("outputs/bonus-model-v2-20261030/modified-rows.json"));var m=UnitBonusChart.Parse(modified);var text=UnitBonusChart.ParsePresentations(modified)["UnitData_Deltan/15"].Text;
  Check(text.Contains("8")&&text.Contains("23.5%"),"Numeric template values");var definition=m["UnitData_Deltan"].Find(b=>b.Level==15);Check(definition.BonusId=="custom_delta"&&definition.Description==text,"Identity and presentation belong to definition");
  var control=new UnitLevelBonusController(m["UnitData_Deltan"],15);Check(Math.Abs(control.GetStat(UnitBonusStat.HackingPostSpeedPercent)-.235f)<.00001f&&control.GetStat(UnitBonusStat.HackingPostDuration)==8,"Same values drive combat");
  var bad=Copy(rows);First(bad,"Effect")["BonusId"]="missing";Reject(bad,"Dangling bonus");
  bad=Copy(rows);First(bad,"Bonus")["DescriptionTemplate"]="{missing.Value}";Reject(bad,"Unknown token");
  bad=Copy(rows);First(bad,"Bonus")["DescriptionTemplate"]="{attack.Every}";Reject(bad,"Wrong token field");
  bad=Copy(rows);First(bad,"Bonus")["DescriptionTemplate"]="{attack.Value";Reject(bad,"Unbalanced token");
  bad=Copy(rows);bad.Add(Copy(First(bad,"Effect")));Reject(bad,"Duplicate effect key");
  bad=Copy(rows);var duplicate=Copy(First(bad,"Effect"));duplicate["EffectKey"]="anotherKey";bad.Add(duplicate);Reject(bad,"Duplicate stat/mode");
  bad=Copy(rows);bad.Add(Copy(First(bad,"Bonus")));Reject(bad,"Duplicate bonus ID");
  bad=Copy(rows);First(bad,"Bonus")["DescriptionTemplate"]="<color=red>bad</color>";Reject(bad,"Raw markup");
  var legacy=JsonMapper.ToObject(File.ReadAllText("tools/backnd-mcp/test/fixtures/unit-bonus-v1.json"));var old=UnitBonusChart.Parse(legacy);
  foreach(var unit in parts)foreach(int level in new[]{1,2,3,6,9,12,15}){
   var a=new UnitLevelBonusController(unit.Value,level);var b=new UnitLevelBonusController(old[unit.Key],level);
   foreach(UnitBonusStat stat in Enum.GetValues(typeof(UnitBonusStat)))Check(Math.Abs(a.GetStat(stat)-b.GetStat(stat))<.00001f,"Legacy parity "+unit.Key+"/"+level+"/"+stat);
   for(int i=0;i<4;i++){var x=a.CompleteCycle("basic_skill");var y=b.CompleteCycle("basic_skill");Check(x.DamageMultiplier==y.DamageMultiplier&&x.ChargeTimeMultiplier==y.ChargeTimeMultiplier,"Cycle parity");}
  }
  return "PASS "+count+" checks: relational identities, shared numeric template/combat values, safe references, malformed/duplicate/orphan rejection, and legacy chart parity at7 level boundaries.";
 }
}
