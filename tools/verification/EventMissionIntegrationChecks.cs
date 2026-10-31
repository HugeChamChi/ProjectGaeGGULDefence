using System;
using System.IO;
using System.Linq;
using GGD.Events;
using LitJson;
using UnityEngine;
public static class EventMissionIntegrationChecks
{
 public static object Run()
 {
  int checks=0;void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
  var oldEvents=EventMissions.Events;var oldMissions=EventMissions.Missions;
  string events=File.ReadAllText("outputs/event-missions-no-version/Events.rows.json"),missions=File.ReadAllText("outputs/event-missions-no-version/Missions.rows.json");
  try {
   EventMissions.Configure(events,missions);
   var e=EventMissions.Events.Single(x=>x.EventKey=="dev_attendance_7day_20261031");
   var items=EventMissions.Missions.Where(m=>m.EventKey==e.EventKey).OrderBy(m=>m.Target).ToArray();
   Check(items.Length==7&&items.All(m=>m.DataKey=="AttendanceDays"),"Attendance migration");
   var start=DateTimeOffset.Parse(e.StartAt);var p=new PlayerData();
   Check(!EventMissions.Visit(p,start.AddSeconds(-1)),"Before start");
   Check(EventMissions.Visit(p,start),"Start included");
   Check(!EventMissions.Visit(p,start.AddHours(1)),"Duplicate login");
   Check(EventMissions.State(p,items[0],start).progress==1,"First login");
   Check(!EventMissions.Claim(p,items[1].MissionKey,start),"Incomplete reward");
   Check(EventMissions.Claim(p,items[0].MissionKey,start),"First claim");
   Check(p.Stamina==40,"Real reward grant");
   Check(!EventMissions.Claim(p,items[0].MissionKey,start),"Duplicate claim");
   p=JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(p));
   Check(!EventMissions.Claim(p,items[0].MissionKey,start),"Restored duplicate");
   EventMissions.Visit(p,start.AddDays(3));
   Check(EventMissions.State(p,items[6],start.AddDays(3)).progress==2,"No absent-day credit");
   for(int i=4;i<=8;i++)EventMissions.Visit(p,start.AddDays(i));
   Check(EventMissions.State(p,items[6],start.AddDays(8)).progress==7,"Seven login days");
   Check(EventMissions.Claim(p,items[6].MissionKey,start.AddDays(8)),"Independent milestone claim");
   Check(EventMissions.Claim(p,items[1].MissionKey,start.AddDays(8)),"Earlier milestone remains claimable");
   Check(!EventMissions.Visit(p,DateTimeOffset.Parse(e.EndAt)),"End excluded");
   Check(!EventMissions.Claim(p,items[2].MissionKey,DateTimeOffset.Parse(e.EndAt)),"Claim deadline");
   var legacy=new PlayerData();legacy.EventAttendance.Add(new AttendanceEventProgress{eventKey=e.EventKey,claimed=2,lastDay=AttendanceEventRuntime.DayKey(start)});
   EventMissions.Visit(legacy,start);
   Check(EventMissions.State(legacy,items[0],start).claimed,"Legacy claim migrated");
   Check(!EventMissions.Claim(legacy,items[1].MissionKey,start),"Legacy duplicate blocked");
   Check(EventMissions.State(legacy,items[6],start).progress==2,"Legacy progress retained");
   var overflow=new PlayerData{Stamina=int.MaxValue};EventMissions.Visit(overflow,start);
   Check(!EventMissions.Claim(overflow,items[0].MissionKey,start)&&!EventMissions.State(overflow,items[0],start).claimed,"Overflow atomic");
   var changed=JsonMapper.ToObject(events);changed[0]["Name"]="independent event edit";
   EventMissions.Configure(changed.ToJson(),missions);
   Check(EventMissions.Events[0].Name=="independent event edit","Independent event edit accepted");
   var invalid=JsonMapper.ToObject(missions);invalid[0]["EventKey"]="missing-event";
   bool rejected=false;try{EventMissions.Configure(events,invalid.ToJson());}catch(FormatException){rejected=true;}
   Check(rejected&&EventMissions.Events[0].Name=="independent event edit","Orphan mission rejected atomically");
   foreach(var definition in EventMissions.Events)Check(EventMissions.ForEvent(definition.EventKey).All(m=>m.EventKey==definition.EventKey),"Event-specific mission list");
   Check(EventMissions.ForEvent(e.EventKey).Length==7,"Attendance event receives only its seven missions");
   Check(EventMissions.ForEvent("missing-event").Length==0,"Unknown event receives no missions");
   var battle=EventMissions.Missions.First(m=>m.DataKey=="BattleCount_Normal");var battleEvent=EventMissions.Events.Single(x=>x.EventKey==battle.EventKey);battleEvent.Enabled=true;
   var time=DateTimeOffset.Parse(battleEvent.StartAt);var data=new PlayerData();
   var occurrence=new DataOccurrence{OccurrenceKey="battle-1",Hook=EventHookKey.BattleEnd_Normal,OccurredAt=time,PlaySeconds=60,ReachedRound=10};
   Check(EventMissions.Battle(data,occurrence),"Battle recorded");
   Check(!EventMissions.Battle(data,occurrence),"Battle duplicate");
   Check(EventMissions.State(data,battle,time).progress==1,"Battle mission progress");
   Check(!data.EventMissions.Any(s=>s.eventKey==e.EventKey),"Battle does not update another event's attendance missions");
   Check(EventMissions.Claim(data,battle.MissionKey,time),"Battle reward");
   Check(data.EventMissions.Where(s=>s.claimed).All(s=>s.eventKey==battleEvent.EventKey),"Claim belongs to the linked event only");
   Check(EventMissions.State(data,battle,time.AddDays(1))==null,"Daily reset");
   var weekly=new MissionDefinition{Period="Weekly"};
   Check(EventMissions.Period(weekly,DateTimeOffset.Parse("2026-11-02T04:59:59+09:00"))!=EventMissions.Period(weekly,DateTimeOffset.Parse("2026-11-02T05:00:00+09:00")),"Weekly boundary");
   return new {passed=checks};
  } finally {
   if(oldEvents.Length==0)EventMissions.Clear();
   else {
    var er=oldEvents.Select(e=>new{e.EventKey,e.Name,e.StartAt,e.EndAt,e.ClaimEndAt,e.Enabled}).ToArray();
    var mr=oldMissions.Select(m=>new{m.MissionKey,m.EventKey,m.Name,m.DataKey,m.Target,m.Period,Rewards=string.Join(";",m.Rewards.Select(r=>r.itemId+":"+r.amount))}).ToArray();
    EventMissions.Configure(JsonMapper.ToJson(er),JsonMapper.ToJson(mr));
   }
  }
 }
}
