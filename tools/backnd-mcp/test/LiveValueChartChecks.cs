using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using LitJson;
public static class LiveValueChartChecks {
 static string Text(JsonData row,string key)=>row.ContainsKey(key)&&row[key]!=null?row[key].ToString():"";
 static string Key(JsonData row)=>Text(row,"RecordType")+"/"+Text(row,"BonusId")+"/"+Text(row,"EffectKey");
 public static async Task<string> Run(){
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
  var rows=await GGD.BackendCharts.BackendChartReader.ReadAsync(UnitBonusChart.ChartName,timeout.Token);
  string file=File.Exists("outputs/value-workbook-path.txt")?File.ReadAllText("outputs/value-workbook-path.txt"):"outputs/bonus-model-v2-20261030/UnitLevelBonuses_2026-10-30.xlsx";var expected=BonusValueTestWindow.ReadWorkbook(file);
  if(rows.Count!=60||rows.Count!=expected.Count)throw new Exception("Server row count mismatch: "+rows.Count);
  var actual=new Dictionary<string,JsonData>();foreach(JsonData row in rows)actual.Add(Key(row),row);
  int cells=0;foreach(JsonData row in expected){if(!actual.TryGetValue(Key(row),out var server))throw new Exception("Missing row: "+Key(row));foreach(string field in row.Keys){if(Text(row,field)!=Text(server,field))throw new Exception("Mismatch: "+Key(row)+"/"+field);cells++;}}
  var parsed=UnitBonusChart.Parse(rows);var texts=UnitBonusChart.ParsePresentations(rows);int stats=0;
  foreach(var unit in parsed){var loaded=UnitBonusChart.Resolve(unit.Key,null);if(loaded==null||loaded.Count!=5)throw new Exception("Boot-loaded definitions missing");foreach(int level in new[]{1,2,3,6,9,12,15}){var a=new UnitLevelBonusController(loaded,level);var b=new UnitLevelBonusController(unit.Value,level);foreach(UnitBonusStat stat in Enum.GetValues(typeof(UnitBonusStat))){if(Math.Abs(a.GetStat(stat)-b.GetStat(stat))>.00001f)throw new Exception("Boot stat mismatch "+unit.Key+"/"+stat);stats++;}}}
  foreach(var text in texts){var parts=text.Key.Split('/');if(UnitBonusChart.GetPresentation(parts[0],int.Parse(parts[1])).Text!=text.Value.Text)throw new Exception("Boot text mismatch");}
  Directory.CreateDirectory("outputs/value-live-20261031");File.WriteAllText("outputs/value-live-20261031/server-rows.json",rows.ToJson());
  return $"PASS server vs exported XLSX: {rows.Count} rows / {cells} cells identical; 6 actors/30 descriptions; boot-loaded stats match fresh server at7 level boundaries ({stats} stat comparisons).";
 }
}
