using System;
using System.IO;
using LitJson;
public static class BonusDescriptionChecks {
 public static string Run(){
  var rows=JsonMapper.ToObject(File.ReadAllText("outputs/bonus-description-rows.json"));
  var effects=UnitBonusChart.Parse(rows);var texts=UnitBonusChart.ParsePresentations(rows);
  if(effects.Count!=5||texts.Count!=10)throw new Exception("Definition counts");
  var chief=texts["AlphanChief/12"];
  if(!chief.Text.Contains("{공격속도효과}")||chief.EffectLink!="ChiefAttackSpeed"||!chief.HighlightNumbers||!chief.LinkTerms)throw new Exception("Chief text mapping");
  var wrong=JsonMapper.ToObject(rows.ToJson());foreach(JsonData row in wrong)if(row.ContainsKey("RecordType")&&row["RecordType"].ToString()=="Description"){row["Description"]="<link=bad>arbitrary</link>";break;}
  bool rejected=false;try{UnitBonusChart.ParsePresentations(wrong);}catch(FormatException){rejected=true;}if(!rejected)throw new Exception("Raw rich text accepted");
  return "PASS 5 drone effect sets and 10 Betan/chief descriptions; chief token/style flags; unsupported rich text rejected";
 }
}
