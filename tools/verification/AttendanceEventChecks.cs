using System;
using GGD.Events;
public static class AttendanceEventChecks {
 [Serializable] private sealed class Wrapper { public AttendanceEventDefinition[] attendanceEvents; }
 public static object Run() {
  int checks=0;
  void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
  var e=new AttendanceEventDefinition{eventKey="test",name="test",enabled=true,startAt="2026-11-01T05:00:00+09:00",endAt="2026-12-01T05:00:00+09:00",repeatMode="Once",cycleDays=14,days=new AttendanceEventDay[14]};
  for(int i=0;i<14;i++)e.days[i]=new AttendanceEventDay{day=i+1,rewards=new[]{new AttendanceEventReward{itemKey="Gold",amount=10},new AttendanceEventReward{itemKey="Diamond",amount=1}}};
  var p=new PlayerData();var start=DateTimeOffset.Parse(e.startAt);int gold=p.Gold;
  Check(AttendanceEventRuntime.Claim(p,e,start.AddSeconds(-1))==0,"before start");
  Check(AttendanceEventRuntime.Claim(p,e,start)==1,"start included");
  Check(p.Gold==gold+10 && p.Diamond==1,"actual rewards");
  Check(AttendanceEventRuntime.Claim(p,e,start.AddHours(23))==0,"duplicate day");
  var restored=UnityEngine.JsonUtility.FromJson<PlayerData>(UnityEngine.JsonUtility.ToJson(p));
  Check(AttendanceEventRuntime.Claim(restored,e,start)==0,"restored duplicate");
  Check(AttendanceEventRuntime.Claim(restored,e,start.AddDays(3))==2,"missed days preserved");
  for(int i=2;i<14;i++)Check(AttendanceEventRuntime.Claim(restored,e,start.AddDays(i+2))==i+1,"14 day progression");
  Check(AttendanceEventRuntime.Claim(restored,e,start.AddDays(17))==0,"once completed");
  e.repeatMode="Cycle";Check(AttendanceEventRuntime.Claim(restored,e,start.AddDays(17))==1,"cycle repeats");
  e.eventKey="other";Check(AttendanceEventRuntime.Claim(restored,e,start.AddDays(17))==1,"event isolation");
  Check(AttendanceEventRuntime.Claim(restored,e,DateTimeOffset.Parse(e.endAt))==0,"end excluded");
  var overflow=new PlayerData{Gold=int.MaxValue};Check(AttendanceEventRuntime.Claim(overflow,e,start)==0 && overflow.EventAttendance.Count==0,"atomic overflow");
  Check(restored.AttendanceCount==0,"legacy attendance isolation");
  var previous=AttendanceEventRuntime.Definitions;
  try {
   AttendanceEventRuntime.Configure(System.IO.File.ReadAllText("tools/planner-studio/tests/events/legacy-attendance.json"));
   Check(AttendanceEventRuntime.Definitions.Length==1,"planner export loaded");
   var exported=AttendanceEventRuntime.Definitions[0];var player=new PlayerData();
   Check(AttendanceEventRuntime.Claim(player,exported,DateTimeOffset.Parse(exported.startAt))==1,"planner reward claim");
  } finally { AttendanceEventRuntime.Configure(UnityEngine.JsonUtility.ToJson(new Wrapper{attendanceEvents=previous})); }
  return new {passed=checks};
 }
}
