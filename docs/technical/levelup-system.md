# 레벨업 선택지·필드 보기·정지

원본: [LevelUpManager](../../Assets/WorkSpace/USW/Scripts/Selection/LevelUpManager.cs), [LevelUpPoolData](../../Assets/WorkSpace/USW/Scripts/Selection/LevelUpPoolData.cs), [LevelUpData](../../Assets/WorkSpace/USW/Scripts/Selection/LevelUpData.cs), [LevelUpUI](../../Assets/WorkSpace/USW/Scripts/IngameUI/LevelUpUI.cs).

> 2026-10-10 [ADR-0001](../architecture/adr-0001-levelup-effects.md)로 책임 분리·조합형 효과 데이터·기존 카드/튜토리얼 이관 설계가 확정됐다. L0~L4 구현과 핵심 검증을 완료했다. 실제24장 SO는 schema1이며 전체 통합 회귀(L5)는 남아 있다. [단계 작업판](../implementation/levelup-refactor/README.md)과 [L4 근거](../../outputs/levelup-refactor-20261010/L4/RESULT.md)를 참조한다.

## 풀과 효과

LevelUpManager.Init이 ChieftainSelection.GetSelectedLevelUpPool로 **이번 런의 풀을 고정**한다. LevelUpCatalog는 PoolId/chooseId 참조를 검증한다. 다른 족장 풀이나 공용 배열을 자동 합산하지 않는다. 같은 카드 SO는 여러 풀에서 공유할 수 있으며 중복 등록으로 후보 가중치를 늘리지 않는다.

GetRandomChoices는 씬 조건과 소비성 레전드 보장을 수집해 LevelUpDrawer에 위임한다. Drawer는 획득 카드, 조건에 맞지 않는 카드, 유효하지 않은 가중치를 제외하고 풀의 등급 가중치로 등급을 한 번 추첨한 뒤, 그 등급 안에서 균등하게 중복 없이 뽑는다. 부족하면 나머지 등급 후보에서 균등하게 보충한다. 카드 spawnRate는 유한한 양수인지 확인하는 후보 조건이며 카드별 추첨 가중치가 아니다. PoolData에는 현재 등급 가중치도 있다. 후보가 1~2장이면 남은 카드만 표시하고 0장이면 선택을 건너뛴다. 누락/잘못된 풀은 오류를 보고하며 다른 풀로 대체하지 않는다.

LevelUpRunState가 획득 이력·활성 카드·프리뷰 난수·소비 보장·결과 문구를 소유한다. 같은 chooseId는 제거 후에도 런 내 재획득할 수 없다. SelectionEffectProjector가 남은 지속 기여로 불변 값을 계산하고 Manager가 토템 선택 슬롯·강화 할인에 반영한 뒤 알림을 보낸다. SelectionCommandExecutor는 최초 지급/스폰/환급/토템 요청만 처리하며 제거 시 재실행·회수하지 않는다. 결과 문구는 획득 시점 문자열로 저장한다.

기존24 SO는 schema1이며 SelectionDefinitionReader는 Composition만 읽고 빈 목록도 legacy fallback하지 않는다. Player 빌드에는 schema0 변환 경로가 없고, Editor에서도 저작 schema0 자산은 거절한다. 구형 변환은 Editor 이관 API와 비영속 역사 검사 fixture 전용이다. 새 카드 메뉴와 preset은 schema1을 작성한다. 공유 SO에 런 상태를 쓰지 않는다. 유닛은 UnitDependencies를 통해 ISelectionCombatReader/IDroneEffectReader/ISelectionEffectChanges를 받는다. 경제·족장도 자기 reader만 주입받으며 모든 인터페이스는 동일한 Scoped SelectionEffectReader를 공유한다. 기본 전투 설정·피해 편차는 CombatSettings로 분리했다. 기존 GameConfig 직렬화 참조를 유지하고 Scope 구성 시 snapshot을 고정한다. 임시 legacy reader와 Manager 수치 facade는 제거했다. UI 재추첨은 RequestsReroll과 TryApplyChoice의 성공 결과로 결정한다. 옛 G07 표를 새 카드 원본으로 사용하지 않는다.

Composition.Feedback는 기능 소유자·행·부족·실제 소유 등급 조건을 정의한다. LevelUpFeedbackTargets는 표시 정의만 읽으며 유닛/족장 버튼의 조합 대상을 중복 없이 합친다. 이관된 Composition은 기존 표시 대상을 보존하고 빈 Feedback는 기본 수집 지점만 사용한다. 드론은 배치/재활성화/reader 교체 때 구독하고 제거/비활성화 때 해제한다. 스킬 충전·델탕 마나/소수 잔량·무관 카드 변경 시 베탕 누적 주기는 실행 소유자에서 유지한다.

## 연출과 정지

[InGameInstaller](../../Assets/WorkSpace/USW/Scripts/CoreSystem/InGameInstaller.cs)가 이벤트와 UI를 연결한다. LevelUpPresentation 폴더의 Reveal/Select/Collect/Peek/TimeDirector가 공개·선택·흡수·필드 강조를 분담한다.

[TimeScaleService](../../Assets/WorkSpace/USW/Scripts/CoreSystem/TimeScaleService.cs)는 소유자별 Pause/Request/Release 중 가장 느린 요청을 적용한다. [FieldPauseVisuals](../../Assets/WorkSpace/USW/Scripts/CoreSystem/FieldPauseVisuals.cs)는 새 공격을 먼저 보류하고, 이미 날아간 공격의 허용된 적중을 처리한 뒤 전투 정지와 대기 모션/이펙트의 실제 시간 재생을 구분한다. 한 UI를 닫았다고 다른 소유자의 정지를 해제하지 않는다.

[UI_Peekthrough](../../Assets/WorkSpace/HSD/Scripts/UI/Utils/UI_Peekthrough.cs)는 빈 곳/카드 홀드 시 필드를 보이게 하고 선택 입력을 잠근다. 복귀 페이드, 모든 터치 해제 및 해제 프레임이 지나야 새 선택을 받는다. 투명한 배경 Graphic의 raycast가 홀드 입력 경로이므로 등급 연출에서 오브젝트를 꺼 버리면 안 된다. 투명도만 바뀐 상태와 선택 가능 상태는 별개다.

보스 처치 [TotemRewardUI](../../Assets/WorkSpace/USW/Scripts/IngameUI/TotemReward/TotemRewardUI.cs)는 독립 보상 화면이다. LevelUpUI/구 TotemSelectUI의 UI_Peekthrough와 동일한 기능이 있다고 가정하지 않는다. 세 화면의 실제 연결·상호작용을 구분한다. 보상 세션 취소는 큐·콜백·트윈을 함께 정리한다.

게임/보상 순서는 [인게임 진행](../../design/gdd/ingame-system.md), 제작 중인 족장 참조는 [족장 문서](alphan-active-skill.md), 확장 기획은 [미채택 드론 카드 제안](drone-build-selection-design.md)에 있다.
