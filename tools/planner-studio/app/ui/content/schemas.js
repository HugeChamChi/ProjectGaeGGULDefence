/* The one schema registry drives forms, references, validation and chart export. */
(()=>{
  const f=(key,label,type='text',options={})=>({key,label,type,...options});
  const id=(key,label)=>f(key,label,'id',{required:true});
  const num=(key,label,min=0,max=2147483647)=>f(key,label,'int',{min,max});
  const ref=(key,label,table,options={})=>f(key,label,'ref',{table,...options});
  const source=f('Source','원본 / 기획 메모','text');
  const def=(label,keys,fields,note='')=>({label,keys,fields:[...fields,source],note});
  globalThis.STUDIO_SCHEMAS={
    Item:def('아이템',['ItemId'],[id('ItemId','아이템 ID'),f('Name','이름','text',{required:true}),f('ItemType','종류','enum',{values:['Currency','Material','Character','ProfileIcon','ProfileFrame']}),f('IconKey','아이콘','icon'),f('BackendId','서버 대상 ID'),f('Note','메모')],'재화 외 아이템은 새 기획입니다. 저장소·지급 지원도 별도로 연결해야 합니다.'),
    RewardGroup:def('보상 묶음',['RewardGroupId'],[id('RewardGroupId','보상 ID'),f('Name','이름','text',{required:true}),f('Rewards','지급 아이템','rewards')]),
    AttendancePlan:def('출석표 설정',['AttendanceId'],[id('AttendanceId','출석표 ID'),f('Name','이름','text',{required:true}),num('ResetHourKst','초기화 시각 (한국 시)',0,23),num('CycleDays','순환 일수',1,365)]),
    Attendance:def('출석 일차',['AttendanceId','Day'],[ref('AttendanceId','출석표','AttendancePlan',{required:true}),num('Day','일차',1,365),ref('RewardGroupId','보상 묶음','RewardGroup',{required:true})]),
    Unit:def('유닛 / 족장',['UnitId'],[id('UnitId','유닛 ID'),f('Name','이름','text',{required:true}),f('Kind','종류','enum',{values:['Unit','Chief']}),f('BackendIds','기존 서버 ID 매핑'),f('IconKey','아이콘','icon'),num('MaxLevel','최대 강화 레벨',1,1000),f('GrowthPerLevel','원본 레벨당 성장 비율','float',{min:0}),f('Note','메모')],'실제 배포 수치는 레벨별 강화 표에 있습니다. 성장 비율은 원본 참고값이며 실제 수치는 레벨별 강화 표에서 편집하세요.'),
    UnitUpgrade:def('레벨별 강화',['UnitId','TargetLevel'],[ref('UnitId','대상 유닛','Unit',{required:true}),num('TargetLevel','목표 레벨',2,1000),f('Costs','소모 아이템','costs'),f('Multiplier','유닛 배율','float',{min:0}),f('GlobalBonus','족장 전체 보너스 비율','float',{min:0})],'시안 공식의 실제 전개값입니다. 비용과 성장량을 레벨별로 편집할 수 있습니다.'),
    UnitMilestone:def('레벨 보너스',['UnitId','Level'],[ref('UnitId','대상 유닛','Unit',{required:true}),num('Level','해금 레벨',1,1000),f('Description','효과 설명','textarea',{required:true})],'현재 UI 문구만 존재합니다. 설명 수정만으로 전투 효과가 구현되지는 않습니다.'),
    ResearchTree:def('영구 강화 트리',['TreeKey'],[id('TreeKey','트리 고정 키'),f('Name','표시 이름','text',{required:true}),f('LegacySaveName','이전 저장 이름'),f('ShowLanes','줄기 이름 표시','bool'),f('ColumnSpacing','열 간격 (0 = 기본)','float',{min:0}),f('LaneNames','줄기 이름','strings')],'기존 TreeKey/NodeId 변경은 저장 데이터 이관이 필요합니다. 기존 키는 유지하는 것을 권장합니다.'),
    ResearchNode:def('트리 노드',['TreeKey','NodeId'],[ref('TreeKey','트리','ResearchTree',{required:true}),f('NodeId','노드 고정 ID','nodeid',{required:true}),f('Stat','강화 스탯','stat'),f('IconKey','아이콘','icon'),f('IconAddress','Addressables Icon Address','text'),f('Shape','모양','enum',{values:['Square','Circle','Special']}),num('MaxLevel','최대 레벨',1,1000),f('ValuePerLevel','레벨당 수치 (비율 원값)','float',{min:0}),num('Row','행 (0 = 맨 아래)',0,200),num('Column','열 (-1 왼쪽, 2 옆가지)',-1,2),f('Parents','선행 노드','parents'),f('RequireAnyParent','부모 중 하나 MAX 허용','bool'),num('RequiredTotalLevel','필요 전체 강화 횟수'),f('ExclusiveGroup','택1 그룹'),num('RecommendOrder','추천 순서 (0 = 자동)'),ref('CostItemId','비용 아이템 (빈칸 = 무료)','Item'),num('BaseCost','1레벨 비용'),num('CostStep','레벨당 비용 증가')],'퍼센트 스탯은 0.02 = 2%입니다. 독립된 두 부모의 합류는 양쪽 1레벨 조건이 우선입니다. 비용은 기획안이며 기존 게임은 무료입니다.'),
    Event:def('이벤트',['EventId'],[id('EventId','이벤트 ID (회차 포함)'),f('Name','이름','text',{required:true}),f('StartAt','시작 (한국 시간)','datetime',{required:true}),f('EndAt','종료 (한국 시간)','datetime',{required:true}),f('ClaimEndAt','수령 마감 (한국 시간)','datetime',{required:true}),f('Enabled','활성','bool'),f('Note','메모')]),
    EventMission:def('이벤트 미션',['EventId','MissionId'],[ref('EventId','이벤트','Event',{required:true}),id('MissionId','미션 ID'),f('Name','표시 이름','text',{required:true}),f('ConditionType','달성 조건','enum',{values:['LoginDays','PlayCount','ClearCount','ReachRound','SpendStamina','UpgradeCount']}),num('TargetCount','목표량',1),ref('RewardGroupId','보상 묶음','RewardGroup',{required:true}),f('ResetPolicy','초기화','enum',{values:['Once','DailyKst']}),f('Note','조건 상세')]),
    ShopOffer:def('상점 상품',['OfferId'],[id('OfferId','상품 ID'),f('Name','상품명','text',{required:true}),f('ShopType','상점 종류','enum',{values:['Daily','Dynamic','Normal']}),ref('DynamicShopId','한정 상점','DynamicShop'),f('Costs','가격 아이템','costs'),ref('RewardGroupId','지급 보상','RewardGroup',{required:true}),num('PurchaseLimit','구매 한도 (0 = 무제한)'),f('ResetPolicy','한도 초기화','enum',{values:['Never','DailyKst','Event']}),f('Note','메모')],'기존 Shop_Daily 컬럼과 다른 통합 기획 규약입니다. 직접 업로드해 교체하지 마세요.'),
    DynamicShop:def('한정 상점',['ShopId'],[id('ShopId','상점 ID'),f('Name','이름','text',{required:true}),num('DurationSeconds','활성 유지 시간(초)',1)]),
    GachaPool:def('가챠 설정',['PoolId'],[id('PoolId','가챠 ID'),f('Name','이름','text',{required:true}),f('CostChartName','기존 비용 차트명'),f('ProbabilityName','뒤끝 확률 이름'),f('Costs','1회 비용','costs'),f('Note','메모')]),
    GachaEntry:def('가챠 확률',['PoolId','CharacterId'],[ref('PoolId','가챠','GachaPool',{required:true}),ref('CharacterId','서버 보유 캐릭터','Character',{required:true}),f('Percent','확률 (%)','float',{min:0,max:100})],'서버 보유 캐릭터 ID와 전투 UnitData ID는 서로 다릅니다. 현재 캐릭터 SO의 ID를 사용합니다.'),
    IAPProduct:def('인앱 상품 기획',['ProductId'],[f('ProductId','스토어 상품 ID','text',{required:true}),f('Name','이름','text',{required:true}),f('ProductType','상품 종류','enum',{values:['Consumable','NonConsumable','Subscription']}),ref('RewardGroupId','지급 보상','RewardGroup',{required:true}),f('Note','플랫폼 / 영수증 정책','textarea')],'IAPManager는 로컬 카탈로그 지급 예제입니다. 카탈로그 파일 미발견으로 빈 목록입니다. 서버 영수증 검증/중복 방지/복원 처리가 별도로 필요합니다.'),
    ChiefArtifact:def('족장 유물 기획',['ArtifactId'],[id('ArtifactId','유물 ID'),f('Name','이름','text',{required:true}),f('IconKey','아이콘','icon'),f('EffectDescription','효과 설명','textarea'),ref('UnlockRewardGroupId','해금 보상 (선택)','RewardGroup'),f('Note','설계 메모')],'기존 유물 화면은 데이터/리스트 TODO입니다. 신규 기획 표이며 전투 효과와 서버 저장은 미구현입니다.'),
    Character:def('서버 캐릭터 목록',['CharacterId'],[f('CharacterId','기존 숫자 ID','text',{required:true}),f('Name','이름','text',{required:true}),num('Rarity','희귀도',0,10),f('IconKey','아이콘','icon')]),
    Party:def('파티 / 덱',['PartyId'],[id('PartyId','덱 ID'),f('Name','이름','text',{required:true}),f('IsUnlocked','기본 해금','bool'),f('UseUniformSummonPool','균등 소환','bool'),f('Units','유닛 명단','refs',{table:'Unit'}),f('ChiefAsset','족장 SO 경로')]),
    Profile:def('프로필 해금',['Kind','ProfileId'],[f('Kind','종류','enum',{values:['Icon','Frame']}),f('ProfileId','원본 ID','text',{required:true}),f('Name','이름','text',{required:true}),f('IconKey','아이콘','icon'),f('DefaultUnlocked','기본 지급','bool'),f('UnlockDescription','해금 설명','textarea')]),
    Tutorial:def('첫 전투 튜토리얼',['TutorialId','StepId'],[id('TutorialId','튜토리얼 ID'),f('StepId','단계 ID','text',{required:true}),num('Order','현재 플랜 순서 (0 = 미참조)'),f('Instruction','안내 문구','textarea'),f('PauseGameplay','게임 일시정지','bool'),ref('RewardGroupId','완료 보상 (기획용)','RewardGroup'),f('Note','연결 상태')]),
    MailMapping:def('우편 아이템 매핑',['ChartName','ItemId'],[f('ChartName','차트 이름','text',{required:true}),f('IdColumn','ID 컬럼','text',{required:true}),ref('ItemId','아이템','Item',{required:true}),f('Currency','현재 지원 재화','enum',{values:['Gold','Diamond','Stamina']})]),
    MailTemplate:def('운영 우편 기획',['TemplateId'],[id('TemplateId','우편 ID'),f('Name','기획용 이름','text',{required:true}),f('Title','우편 제목','text',{required:true}),f('Body','본문','textarea'),ref('RewardGroupId','첨부 보상','RewardGroup',{required:true}),num('ExpireDays','유효 일수 (기획값)',1,30),f('Note','대상 / 발송 메모')],'실제 우편 생성/발송은 하지 않습니다. 보상 묶음을 뒤끝 첨부 아이템으로 변환하는 연결이 필요합니다.'),
    CouponPlan:def('쿠폰 기획',['CouponId'],[id('CouponId','쿠폰 기획 ID'),f('Name','이름','text',{required:true}),ref('RewardGroupId','보상','RewardGroup',{required:true}),f('StartAt','시작 (한국 시간)','datetime',{required:true}),f('EndAt','종료 (한국 시간)','datetime',{required:true}),f('Note','대상 / 정책')]),
    AccountLevel:def('계정 경험치 곡선',['Level'],[num('Level','계정 레벨',1,1000),num('RequiredExperience','다음 레벨 필요 EXP (최대 레벨은 0)')]),
    Economy:def('재화 / 스태미나',['ConfigKey'],[id('ConfigKey','설정 키'),f('Name','설명','text',{required:true}),num('Value','값')]),
    BattleReward:def('전투 결과 보상',['RuleId'],[id('RuleId','규칙 ID'),f('Name','이름','text',{required:true}),f('GrantOnWin','승리 EXP 지급','bool'),f('GrantOnLose','패배 EXP 지급','bool'),num('WinBossExperience','승리 보스당 기준 EXP'),num('LoseBossExperience','패배 보스당 기준 EXP'),ref('WinRewardGroupId','승리 추가 보상 (신규)','RewardGroup'),ref('LoseRewardGroupId','패배 추가 보상 (신규)','RewardGroup'),f('Note','정산 설명','textarea')]),
    Notification:def('알림 문구',['NotificationId'],[id('NotificationId','알림 설정 ID'),f('Title','제목'),f('StaminaMessage','스태미나 알림'),f('AttendanceMessage','출석 알림'),num('AttendanceHourKst','출석 알림 시',0,23),num('AttendanceMinute','출석 알림 분',0,59)]),
    UserSchema:{...def('유저 저장 설계',['TableName'],[f('TableName','테이블'),f('Fields','필드 목록','textarea'),f('Status','구현 상태'),f('Note','설명')]),readonly:true}
  };
  globalThis.STUDIO_NAV=[
    {id:'overview',label:'프로젝트 개요',icon:'◈',tables:[]},
    {id:'rewards',label:'아이템 · 보상',icon:'▣',tables:['RewardGroup','Item']},
    {id:'research',label:'영구 강화 트리',icon:'⌘',tables:['ResearchNode','ResearchTree']},
    {id:'units',label:'유닛 · 족장 강화',icon:'⬡',tables:['Unit','UnitUpgrade','UnitMilestone','Party']},
    {id:'attendance',label:'출석',icon:'▦',tables:['Attendance','AttendancePlan']},
    {id:'events',label:'이벤트 · 미션',icon:'⚑',tables:['Event','EventMission']},
    {id:'shops',label:'상점 · 가챠',icon:'◇',tables:['ShopOffer','DynamicShop','GachaPool','GachaEntry','Character','IAPProduct']},
    {id:'mail',label:'우편 · 쿠폰',icon:'✉',tables:['MailTemplate','MailMapping','CouponPlan']},
    {id:'account',label:'계정 · 전투 보상',icon:'▤',tables:['Economy','AccountLevel','BattleReward','Notification']},
    {id:'profile',label:'프로필 · 튜토리얼',icon:'◎',tables:['Profile','Tutorial','ChiefArtifact']},
    {id:'storage',label:'유저 저장 설계',icon:'▥',tables:['UserSchema']}
  ];
})();
