using System;
using System.Linq;
using UnityEngine;

namespace GGD.Events
{
    [Serializable] public sealed class AttendanceEventDefinition
    {
        public string eventKey, name, startAt, endAt, claimEndAt, repeatMode;
        public int cycleDays;
        public bool enabled;
        public AttendanceEventDay[] days;
    }
    [Serializable] public sealed class AttendanceEventDay { public int day; public AttendanceEventReward[] rewards; }
    [Serializable] public sealed class AttendanceEventReward { public string itemKey; public int amount; }
    [Serializable] public sealed class AttendanceEventProgress
    {
        public string eventKey, lastDay;
        public int claimed;
    }
    public static class AttendanceEventRuntime
    {
        [Serializable] private sealed class Payload { public AttendanceEventDefinition[] attendanceEvents; }
        public static AttendanceEventDefinition[] Definitions { get; private set; } = Array.Empty<AttendanceEventDefinition>();
        public static void Clear() => Definitions = Array.Empty<AttendanceEventDefinition>();
        public static void Configure(string json)
        {
            var definitions = JsonUtility.FromJson<Payload>(json)?.attendanceEvents ?? Array.Empty<AttendanceEventDefinition>();
            if (definitions.Select(x => x.eventKey).Distinct().Count() != definitions.Length) throw new ArgumentException("Duplicate attendance event key");
            foreach (var e in definitions) {
                if (string.IsNullOrWhiteSpace(e.eventKey) || e.cycleDays < 1 || e.cycleDays > 365 || e.days == null || e.days.Length != e.cycleDays
                    || (e.repeatMode != "Once" && e.repeatMode != "Cycle")) throw new ArgumentException("Invalid attendance event");
                if (e.enabled && (!DateTimeOffset.TryParse(e.startAt, out var start) || !DateTimeOffset.TryParse(e.endAt, out var end) || start >= end)) throw new ArgumentException("Invalid attendance dates");
                if (!string.IsNullOrEmpty(e.claimEndAt) && (!DateTimeOffset.TryParse(e.claimEndAt,out var claim) || claim < DateTimeOffset.Parse(e.endAt))) throw new ArgumentException("Invalid claim deadline");
                for (int day = 1; day <= e.cycleDays; day++) {
                    var rewards = e.days.Single(x => x.day == day).rewards;
                    if (rewards == null || rewards.Length == 0 || rewards.Select(r=>r.itemKey).Distinct().Count()!=rewards.Length
                        || rewards.Any(r=>r.amount<=0 || (r.itemKey!="Gold" && r.itemKey!="Diamond" && r.itemKey!="Stamina"))) throw new ArgumentException("Unsupported attendance reward");
                }
            }
            Definitions = definitions;
        }
        public static bool Active(AttendanceEventDefinition e, DateTimeOffset now) => e.enabled && now >= DateTimeOffset.Parse(e.startAt) && now < DateTimeOffset.Parse(e.endAt);
        public static string DayKey(DateTimeOffset now) => now.ToOffset(TimeSpan.FromHours(9)).AddHours(-DailyManager.ResetHourKst).ToString("yyyy-MM-dd");
        public static AttendanceRules.Status Evaluate(AttendanceEventDefinition e, AttendanceEventProgress state, DateTimeOffset now)
        {
            int claimed = state?.claimed ?? 0;
            string today = DayKey(now);
            if (!Active(e,now) || (state?.lastDay != null && string.CompareOrdinal(state.lastDay,today)>=0)
                || (e.repeatMode=="Once" && claimed>=e.cycleDays)) return new AttendanceRules.Status(claimed,0,state?.lastDay==today);
            return new AttendanceRules.Status(claimed%e.cycleDays,claimed%e.cycleDays+1,false);
        }
        public static int Claim(PlayerData data, AttendanceEventDefinition e, DateTimeOffset now)
        {
            data.EventAttendance ??= new System.Collections.Generic.List<AttendanceEventProgress>();
            var state=data.EventAttendance.SingleOrDefault(x=>x.eventKey==e.eventKey);
            var status=Evaluate(e,state,now);
            if (!status.CanClaim) return 0;
            var rewards=e.days.Single(x=>x.day==status.ClaimableDay).rewards;
            long gold=data.Gold,diamond=data.Diamond,stamina=data.Stamina;
            foreach(var r in rewards) { if(r.itemKey=="Gold")gold+=r.amount; else if(r.itemKey=="Diamond")diamond+=r.amount; else stamina+=r.amount; }
            if(gold>int.MaxValue || diamond>int.MaxValue || stamina>int.MaxValue) return 0;
            if(state==null){state=new AttendanceEventProgress{eventKey=e.eventKey};data.EventAttendance.Add(state);}
            data.Gold=(int)gold;data.Diamond=(int)diamond;data.Stamina=(int)stamina;
            state.claimed++;state.lastDay=DayKey(now);
            return status.ClaimableDay;
        }
    }
}
