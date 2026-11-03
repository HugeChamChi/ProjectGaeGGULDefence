using System;
using System.IO;
using LitJson;
using UnityEditor;
public static class BonusValueContractChecks {
 static int count;
 static void Check(bool yes,string name){count++;if(!yes)throw new Exception(name);}
 static JsonData Copy(JsonData rows)=>JsonMapper.ToObject(rows.ToJson());
 static JsonData Effect(JsonData rows,string type){foreach(JsonData row in rows)if(row["RecordType"].ToString()=="Effect"&&row["EffectType"].ToString()==type)return row;throw new Exception(type);}
 public static string Run(){
  count=0;string file=File.Exists("outputs/value-workbook-path.txt")?File.ReadAllText("outputs/value-workbook-path.txt"):"outputs/bonus-model-v2-20261030/UnitLevelBonuses_2026-10-30.xlsx";var rows=BonusValueTestWindow.ReadWorkbook(file);
  var parsed=UnitBonusChart.Parse(rows);Check(rows.Count==60&&parsed.Count==6,"Workbook 60 rows / 6 owners");
  foreach(var pair in new[]{("HackingAttackSpeedBonus","15,2"),("ChiefAttackSpeedBonus","20,10"),("RallyVulnerabilityBonus","10,5"),("HackingProductionBonus","1,1,2,3"),("HackingConsumptionBonus","1,1,2,4")})Check(Effect(rows,pair.Item1)["Value"].ToString()==pair.Item2,"Contract "+pair.Item1);
  foreach(string bad in new[]{"15","15,2,3","15,","-1,2","NaN,2","1e999,2","9007199254740992,2"}){
   var copy=Copy(rows);Effect(copy,"HackingAttackSpeedBonus")["Value"]=bad;bool rejected=false;try{UnitBonusChart.Parse(copy);}catch(FormatException){rejected=true;}Check(rejected,"Invalid CSV "+bad);
  }
  foreach(var pair in new[]{("EveryNthCycleBonus","0,40,0"),("EveryNthCycleBonus","2,40,100"),("HackingProductionBonus","1,1,2.5,3")}){
   var copy=Copy(rows);Effect(copy,pair.Item1)["Value"]=pair.Item2;bool rejected=false;try{UnitBonusChart.Parse(copy);}catch(FormatException){rejected=true;}Check(rejected,"Invalid range "+pair.Item1);
  }
  var window=EditorWindow.GetWindow<BonusValueTestWindow>("보너스 Value 검사");window.LoadWorkbook(file);window.Show();
  Check(UnitBonusChart.ParsePresentations(rows).Count==30,"30 rendered descriptions");
  return "PASS "+count+" checks: real XLSX reader, 7 Value contracts, arity/range rejection, editor test window loaded.";
 }
}
