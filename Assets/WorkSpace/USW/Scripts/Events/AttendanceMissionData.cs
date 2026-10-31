using System;
using System.Linq;
namespace GGD.Events
{
    public static class AttendanceMissionData
    {
        public static MissionDefinition[] Resolve(string eventKey, DateTimeOffset now, int slots)
        {
            var candidates=EventMissions.Missions.Where(m=>m.DataKey=="AttendanceDays");
            if(!string.IsNullOrEmpty(eventKey))candidates=candidates.Where(m=>m.MissionType==MissionType.Event&&m.EventKey==eventKey);
            else candidates=candidates.Where(m=>m.MissionType==MissionType.Normal||EventMissions.Events.Any(e=>e.EventKey==m.EventKey&&e.Enabled&&now>=DateTimeOffset.Parse(e.StartAt)&&now<DateTimeOffset.Parse(string.IsNullOrEmpty(e.ClaimEndAt)?e.EndAt:e.ClaimEndAt)));
            var groups=candidates.GroupBy(m=>m.EventKey).ToArray();
            if(groups.Length>1)throw new InvalidOperationException("출석 화면에 연결할 EventKey를 지정해주세요.");
            var missions=groups.SelectMany(g=>g).OrderBy(m=>m.Target).ToArray();
            if(missions.Length>slots)throw new InvalidOperationException("기존 출석 화면의 보상 칸 수를 초과했습니다.");
            return missions;
        }
    }
}
