using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LitJson;

namespace GGD.Events
{
    [Serializable] public sealed class SavedMission
    {
        public string eventKey, missionKey, period, lastVisit;
        public double progress;
        public bool claimed;
    }
    public static class EventMissions
    {
        public static MissionEvent[] Events { get; private set; } = Array.Empty<MissionEvent>();
        public static MissionDefinition[] Missions { get; private set; } = Array.Empty<MissionDefinition>();
        static string Text(JsonData row,string key) => row.ContainsKey(key) && row[key]!=null ? row[key].ToString() : "";
        public static void Configure(string eventsJson,string missionsJson)
        {
            var eventRows=JsonMapper.ToObject(eventsJson);var missionRows=JsonMapper.ToObject(missionsJson);
            if(!eventRows.IsArray||!missionRows.IsArray)throw new FormatException("Invalid event configuration");
            var events=new List<MissionEvent>();var missions=new List<MissionDefinition>();
            foreach(JsonData row in eventRows)events.Add(new MissionEvent{EventKey=Text(row,"EventKey"),Name=Text(row,"Name"),StartAt=Text(row,"StartAt"),EndAt=Text(row,"EndAt"),ClaimEndAt=Text(row,"ClaimEndAt"),Enabled=Text(row,"Enabled")=="1"||Text(row,"Enabled").Equals("true",StringComparison.OrdinalIgnoreCase)});
            foreach(JsonData row in missionRows) {
                var rewards=new List<MissionReward>();
                foreach(var token in Text(row,"Rewards").Split(';')) {var parts=token.Split(':');if(parts.Length!=2)throw new FormatException("Invalid rewards");rewards.Add(new MissionReward{itemId=parts[0],amount=int.Parse(parts[1],CultureInfo.InvariantCulture)});}
                string type=Text(row,"MissionType");
                if(string.IsNullOrEmpty(type))type=string.IsNullOrEmpty(Text(row,"EventKey"))?"Normal":"Event";
                if(type!="Normal"&&type!="Event")throw new FormatException("Invalid mission type");
                missions.Add(new MissionDefinition{MissionType=type=="Normal"?MissionType.Normal:MissionType.Event,MissionKey=Text(row,"MissionKey"),EventKey=Text(row,"EventKey"),Name=Text(row,"Name"),DataKey=Text(row,"DataKey"),Period=Text(row,"Period"),Target=double.Parse(Text(row,"Target"),CultureInfo.InvariantCulture),Rewards=rewards.ToArray()});
            }
            if(events.Select(e=>e.EventKey).Distinct().Count()!=events.Count||missions.Select(m=>m.MissionKey).Distinct().Count()!=missions.Count)throw new FormatException("Duplicate event/mission key");
            foreach(var e in events)if(string.IsNullOrWhiteSpace(e.EventKey)||!DateTimeOffset.TryParse(e.StartAt,out var start)||!DateTimeOffset.TryParse(e.EndAt,out var end)||start>=end||(!string.IsNullOrEmpty(e.ClaimEndAt)&&(!DateTimeOffset.TryParse(e.ClaimEndAt,out var claim)||claim<end)))throw new FormatException("Invalid event dates");
            foreach(var m in missions)if(string.IsNullOrWhiteSpace(m.MissionKey)||(m.MissionType==MissionType.Event?!events.Any(e=>e.EventKey==m.EventKey):!string.IsNullOrEmpty(m.EventKey))||!new[]{"AttendanceDays","BattleCount_Normal","BattleCount_Hard","BattleCount_All","PlaySeconds_Normal","BestRound_Normal"}.Contains(m.DataKey)||!new[]{"Total","Event","Daily","Weekly"}.Contains(m.Period)||double.IsNaN(m.Target)||double.IsInfinity(m.Target)||m.Target<=0||(m.DataKey=="AttendanceDays"&&m.Target!=Math.Truncate(m.Target))||m.Rewards.Any(r=>r.amount<=0||!new[]{"Gold","Diamond","Stamina"}.Contains(r.itemId))||m.Rewards.Select(r=>r.itemId).Distinct().Count()!=m.Rewards.Length)throw new FormatException("Invalid mission: "+m.MissionKey);
            Events=events.ToArray();Missions=missions.ToArray();
        }
        public static void Clear(){Events=Array.Empty<MissionEvent>();Missions=Array.Empty<MissionDefinition>();}
        public static MissionDefinition[] ForEvent(string eventKey) => Missions.Where(m=>m.EventKey==eventKey).ToArray();
        public static bool Active(MissionEvent e,DateTimeOffset now)=>e.Enabled&&now>=DateTimeOffset.Parse(e.StartAt)&&now<DateTimeOffset.Parse(e.EndAt);
        static bool CanProgress(MissionDefinition m,DateTimeOffset now)=>m.MissionType==MissionType.Normal||Events.Any(e=>e.EventKey==m.EventKey&&Active(e,now));
        public static string Period(MissionDefinition m,DateTimeOffset now)
        {
            var day=now.ToOffset(TimeSpan.FromHours(9)).AddHours(-DailyManager.ResetHourKst);
            return m.Period=="Daily"?day.ToString("yyyy-MM-dd"):m.Period=="Weekly"?day.AddDays(-((int)day.DayOfWeek+6)%7).ToString("yyyy-MM-dd"):"Event";
        }
        public static SavedMission State(PlayerData data,MissionDefinition m,DateTimeOffset now,bool create=false)
        {
            data.EventMissions ??= new List<SavedMission>();
            string period=Period(m,now);
            var state=data.EventMissions.Find(s=>s.eventKey==m.EventKey&&s.missionKey==m.MissionKey&&s.period==period);
            if(state==null&&create){state=new SavedMission{eventKey=m.EventKey,missionKey=m.MissionKey,period=period};
                var legacy=data.EventAttendance?.Find(s=>s.eventKey==m.EventKey);
                if(m.DataKey=="AttendanceDays"&&legacy!=null){state.progress=Math.Min(m.Target,legacy.claimed);state.claimed=legacy.claimed>=m.Target;state.lastVisit=legacy.lastDay;}
                data.EventMissions.Add(state);}
            return state;
        }
        public static bool Visit(PlayerData data,DateTimeOffset now)
        {
            bool changed=false;string today=AttendanceEventRuntime.DayKey(now);
            foreach(var m in Missions.Where(m=>m.DataKey=="AttendanceDays"&&CanProgress(m,now))){
                var s=State(data,m,now,true);
                if(string.CompareOrdinal(today,s.lastVisit??"")<=0)continue;
                s.progress=Math.Min(m.Target,s.progress+1);s.lastVisit=today;changed=true;
            }
            return changed;
        }
        public static bool Battle(PlayerData data,DataOccurrence occurrence)
        {
            data.EventMissionReceipts ??= new List<string>();
            if(data.EventMissionReceipts.Contains(occurrence.OccurrenceKey))return false;
            foreach(var m in Missions.Where(m=>m.DataKey!="AttendanceDays"&&CanProgress(m,occurrence.OccurredAt))){
                bool hard=occurrence.Hook==EventHookKey.BattleEnd_Hard;
                double value=m.DataKey=="BattleCount_All"||m.DataKey==(hard?"BattleCount_Hard":"BattleCount_Normal")?1:!hard&&m.DataKey=="PlaySeconds_Normal"?occurrence.PlaySeconds:!hard&&m.DataKey=="BestRound_Normal"?occurrence.ReachedRound:0;
                if(value<=0)continue;
                var s=State(data,m,occurrence.OccurredAt,true);s.progress=Math.Min(m.Target,m.DataKey=="BestRound_Normal"?Math.Max(s.progress,value):s.progress+value);
            }
            data.EventMissionReceipts.Add(occurrence.OccurrenceKey);return true;
        }
        public static bool Claim(PlayerData data,string key,DateTimeOffset now)
        {
            var m=Missions.SingleOrDefault(m=>m.MissionKey==key);if(m==null)return false;
            if(m.MissionType==MissionType.Event){var e=Events.Single(e=>e.EventKey==m.EventKey);
                if(!e.Enabled||now<DateTimeOffset.Parse(e.StartAt)||now>=DateTimeOffset.Parse(string.IsNullOrEmpty(e.ClaimEndAt)?e.EndAt:e.ClaimEndAt))return false;}
            var state=State(data,m,now,true);if(state.claimed||state.progress<m.Target)return false;
            long gold=data.Gold,diamond=data.Diamond,stamina=data.Stamina;
            foreach(var r in m.Rewards){if(r.itemId=="Gold")gold+=r.amount;else if(r.itemId=="Diamond")diamond+=r.amount;else stamina+=r.amount;}
            if(gold>int.MaxValue||diamond>int.MaxValue||stamina>int.MaxValue)return false;
            data.Gold=(int)gold;data.Diamond=(int)diamond;data.Stamina=(int)stamina;state.claimed=true;return true;
        }
    }
}
