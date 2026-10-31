# 이벤트·미션 두 표

현재 이벤트 저작·배포 기준은 Events와 Missions 두 표입니다. 출석 별도 차트와 RecordType/Payload 형식은 신규 배포에 사용하지 않습니다.

- Events: EventKey, Name, StartAt, EndAt, ClaimEndAt, Enabled.
- Missions: MissionKey, MissionType, EventKey, Name, DataKey, Target, Period, Rewards.
- MissionType=Normal은 EventKey를 비우며 이벤트 기간 제한 없이 진행합니다. MissionType=Event는 존재하는 EventKey가 필수이며 이벤트 기간과 수령 마감을 적용합니다.
- Period는 Total/Daily/Weekly입니다. 이전 Period=Event는 Total로 변환하며 기존 진행 키는 유지합니다.
- Rewards는 `Gold:500;Stamina:10` 형식입니다. 게임 지급 연결은 세 재화로 한정합니다.
- AttendanceDays는 서버 날짜 기준 접속일 누적입니다. 보상을 수령해야 출석일이 증가하는 방식이 아닙니다.
- Daily/Weekly/Event를 지원하며 주간은 월요일 공통 초기화 시각 기준입니다.
- 이전 일회성 출석 초안은 미션으로 변환합니다. 반복 Cycle은 자동 변환하지 않으며 일일/주간 미션으로 재설정해야 합니다.
- PlayerData.EventMissions에 진행도와 수령 기록, EventMissionReceipts에 처리한 전투 키를 저장합니다. 재화와 같은 저장 스냅샷 및 실패 저장 저널을 사용합니다.
- EventAttendance의 이미 수령한 일차는 변환 시 재지급을 막습니다. 기존 일반 출석 기록은 별도 보존합니다.

기획용 Excel은 Events/Missions 두 시트이며 배포는 Events.xlsx와 Missions.xlsx 두 파일입니다. DEV_Events/DEV_Missions에 적용합니다. 게임은 해당 차트들을 함께 검증한 다음 출석 메뉴의 이벤트 미션 화면에서 보여줍니다.

게임 구현: EventMissions.cs, AttendancePopup.cs + AttendanceMissionData.cs, EventChartLoader.cs. 기획툴 공통 변환: app/event-tables.cjs. 현재 전달본과 검사 결과: outputs/event-missions-release.

서버 업로드·실계정 지급·모바일 조작은 미검증입니다. 신규 가입자 자격 제한은 현재 지원하지 않습니다.

Attendance presentation uses the existing AttendancePopup prefab and its original seven slots. AttendanceMissionData supplies AttendanceDays definitions and rewards; AttendanceService claims the corresponding mission. The replacement panel was removed. No extra attendance UI is generated. Multiple eligible event groups require an explicit EventKey binding.
