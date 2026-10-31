using System;
using System.IO;
using System.Linq;
using GGD.Events;
public static class AttendanceMissionBindingChecks
{
 public static object Run(){
  int checks=0;void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
  var oldEvents=EventMissions.Events;var oldMissions=EventMissions.Missions;
  try{
   EventMissions.Configure(File.ReadAllText("tools/planner-studio/tests/events/Events.json"),File.ReadAllText("tools/planner-studio/tests/events/Missions.json"));
   var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/WorkSpace/USW/Prefabs/UI/AttendancePopup.prefab");
   Check(prefab!=null&&prefab.GetComponent<AttendancePopup>()!=null,"Existing attendance prefab reused");
   var slots=prefab.GetComponentsInChildren<AttendanceDaySlot>(true);Check(slots.Length==7,"Original seven reward slots");
   var at=DateTimeOffset.Parse("2026-10-31T12:00:00+09:00");
   var missions=AttendanceMissionData.Resolve("",at,slots.Length);
   Check(missions.Length==7,"Server missions bind to existing slots");
   Check(missions.All(m=>m.EventKey=="dev_attendance_7day_20261031"),"Correct event");
   Check(missions.Select(m=>m.Target).SequenceEqual(new double[]{1,2,3,4,5,6,7}),"Day order");
   Check(missions[0].Rewards.Any(r=>r.itemId=="Stamina"&&r.amount==10),"Mission rewards supply existing view");
   Check(AttendanceMissionData.Resolve("missing",at,slots.Length).Length==0,"Unknown binding does not fall back to another event");
   bool overflow=false;try{AttendanceMissionData.Resolve("",at,6);}catch(InvalidOperationException){overflow=true;}Check(overflow,"No additional UI slots invented");
   Check(!File.Exists("Assets/WorkSpace/USW/Scripts/Events/EventMissionPanel.cs"),"Replacement screen removed");
   return new{passed=checks,noServerWrites=true};
  }finally{
   if(oldMissions.Length==0)EventMissions.Clear();else{
    var er=oldEvents.Select(e=>new{e.EventKey,e.Name,e.StartAt,e.EndAt,e.ClaimEndAt,e.Enabled}).ToArray();
    var mr=oldMissions.Select(m=>new{m.MissionKey,MissionType=m.MissionType.ToString(),m.EventKey,m.Name,m.DataKey,m.Target,m.Period,Rewards=string.Join(";",m.Rewards.Select(r=>r.itemId+":"+r.amount))}).ToArray();EventMissions.Configure(LitJson.JsonMapper.ToJson(er),LitJson.JsonMapper.ToJson(mr));
   }
  }
 }
}
