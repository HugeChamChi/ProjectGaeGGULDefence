using System;
using System.Linq;
using GGD.Events;
public static class EventMissionTypeChecks
{
 public static object Run(){
  int passed=0;void Check(bool ok,string message){if(!ok)throw new Exception(message);passed++;}
  var oldEvents=EventMissions.Events;var oldMissions=EventMissions.Missions;
  string events="[{\"EventKey\":\"limited\",\"Name\":\"Limited\",\"StartAt\":\"2026-11-01T05:00:00+09:00\",\"EndAt\":\"2026-11-03T05:00:00+09:00\",\"Enabled\":1}]";
  string Mission(string key,string type,string eventKey,string period="Total")=>"{\"MissionKey\":\""+key+"\",\"MissionType\":\""+type+"\",\"EventKey\":\""+eventKey+"\",\"Name\":\"Test\",\"DataKey\":\"AttendanceDays\",\"Target\":1,\"Period\":\""+period+"\",\"Rewards\":\"Gold:10\"}";
  try{
   var at=DateTimeOffset.Parse("2026-10-31T05:00:00+09:00");
   EventMissions.Configure(events,"["+Mission("normal","Normal","")+","+Mission("event","Event","limited")+"]");
   Check(EventMissions.Missions[0].MissionType==MissionType.Normal,"Typed normal enum");
   var p=new PlayerData();EventMissions.Visit(p,at);
   Check(EventMissions.Claim(p,"normal",at),"Normal works before event starts");
   Check(!EventMissions.Claim(p,"event",at),"Event blocked before start");
   EventMissions.Visit(p,at.AddDays(1));Check(EventMissions.Claim(p,"event",at.AddDays(1)),"Event enabled in period");
   Check(EventMissions.ForEvent("").Length==1&&EventMissions.ForEvent("limited").Length==1,"Normal and event views separated");
   EventMissions.Configure("[]","["+Mission("normal_only","Normal","","Daily")+"]");
   var only=new PlayerData();EventMissions.Visit(only,at.AddYears(2));Check(EventMissions.Claim(only,"normal_only",at.AddYears(2)),"Normal-only with no events and no date limit");
   EventMissions.Visit(only,at.AddYears(2).AddDays(1));Check(EventMissions.Claim(only,"normal_only",at.AddYears(2).AddDays(1)),"Normal daily reset");
   foreach(var invalid in new[]{Mission("bad","Event",""),Mission("bad","Event","missing"),Mission("bad","Normal","limited"),Mission("bad","Unknown","")}){bool rejected=false;try{EventMissions.Configure(events,"["+invalid+"]");}catch(FormatException){rejected=true;}Check(rejected,"Conditional EventKey/type validation");}
   return new{passed};
  }finally{
   if(oldMissions.Length==0)EventMissions.Clear();else{
    var er=oldEvents.Select(e=>new{e.EventKey,e.Name,e.StartAt,e.EndAt,e.ClaimEndAt,e.Enabled});
    var mr=oldMissions.Select(m=>new{m.MissionKey,MissionType=m.MissionType.ToString(),m.EventKey,m.Name,m.DataKey,m.Target,m.Period,Rewards=string.Join(";",m.Rewards.Select(r=>r.itemId+":"+r.amount))});
    EventMissions.Configure(LitJson.JsonMapper.ToJson(er.ToArray()),LitJson.JsonMapper.ToJson(mr.ToArray()));
   }
  }
 }
}
