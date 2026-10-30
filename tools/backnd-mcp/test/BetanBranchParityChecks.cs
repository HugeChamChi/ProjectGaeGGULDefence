using System;
using System.IO;
using UnityEditor;
using LitJson;
public static class BetanBranchParityChecks {
 static void Near(float a,float b,string label){if(Math.Abs(a-b)>.00001f)throw new Exception(label+": "+a+" != "+b);}
 public static string Run(){
  var source=AssetDatabase.LoadAssetAtPath<UnitData>("Assets/WorkSpace/USW/Data/DroneUnits/UnitData_Betan.asset");
  var rows=JsonMapper.ToObject(File.ReadAllText("outputs/bonus-description-rows.json"));
  var remote=UnitBonusChart.Parse(rows)["UnitData_Betan"];
  int checks=0;foreach(int level in new[]{1,2,3,5,6,8,9,11,12,14,15,20}){
   var original=new UnitLevelBonusController(source.LevelBonuses,level);var converted=new UnitLevelBonusController(remote,level);
   foreach(UnitBonusStat stat in Enum.GetValues(typeof(UnitBonusStat))){Near(original.GetStat(stat),converted.GetStat(stat),"Level "+level+"/"+stat);checks++;}
   for(int action=1;action<=4;action++){
    var a=original.CompleteCycle("basic_skill");var b=converted.CompleteCycle("basic_skill");Near(a.DamageMultiplier,b.DamageMultiplier,"Damage");Near(a.ChargeTimeMultiplier,b.ChargeTimeMultiplier,"Charge");checks+=2;
   }
  }
  var texts=UnitBonusChart.ParsePresentations(rows);
  foreach(int level in new[]{6,12})if(!texts["UnitData_Betan/"+level].Text.Contains("선택지 추가 자폭 제외"))throw new Exception("Exclusion text lost");
  return "PASS source SO identical to feat/betan-level-bonuses; "+checks+" controller comparisons at 12 level boundaries; explicit additional-bomb exclusions";
 }
}
