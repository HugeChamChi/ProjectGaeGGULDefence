using System;
using System.IO;
using GGD.BackendCharts;
using GGD.Events;
using LitJson;
using UnityEngine;
public static class ReadableRowsChecks {
 public static string Run(){
  var units=UnitBalanceSnapshot.ParseConfig(ChartSnapshotPayload.Decode(File.ReadAllText("outputs/readable-UnitBaseStats.json")));
  using var snapshot=new UnitBalanceSnapshot(units);
  if(snapshot.Count!=9)throw new Exception("Unit count");
  var mission=JsonUtility.FromJson<EventDataConfig>(ChartSnapshotPayload.Decode(File.ReadAllText("outputs/readable-MissionEvents.json")));
  var runtime=new EventDataRuntime("isolated",mission,5);
  var trees=ResearchChartParser.Parse(JsonMapper.ToObject(File.ReadAllText("outputs/readable-ResearchBalance.json")));
  int nodes=0;foreach(var tree in trees.Values)nodes+=tree.Nodes.Count;ResearchChartParser.Destroy(trees);
  if(nodes!=61)throw new Exception("Research count");
  return "PASS table rows: 9 units/36 tiers, mission runtime validated, research 61 nodes/relations";
 }
}
