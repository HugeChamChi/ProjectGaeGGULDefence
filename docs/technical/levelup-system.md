# 레벨업 선택지·필드 보기·정지

원본: [LevelUpManager](../../Assets/WorkSpace/USW/Scripts/Selection/LevelUpManager.cs), [LevelUpPoolData](../../Assets/WorkSpace/USW/Scripts/Selection/LevelUpPoolData.cs), [LevelUpData](../../Assets/WorkSpace/USW/Scripts/Selection/LevelUpData.cs), [LevelUpUI](../../Assets/WorkSpace/USW/Scripts/IngameUI/LevelUpUI.cs).

## 풀과 효과

LevelUpManager.Init이 ChieftainSelection.GetSelectedLevelUpPool로 **이번 런의 풀을 고정**한다. LevelUpCatalog는 PoolId/chooseId 참조를 검증한다. 다른 족장 풀이나 공용 배열을 자동 합산하지 않는다. 같은 카드 SO는 여러 풀에서 공유할 수 있으며 중복 등록으로 후보 가중치를 늘리지 않는다.

GetRandomChoices는 획득 카드, 조건에 맞지 않는 카드, 유효하지 않은 가중치를 제외하고 풀의 등급 가중치로 등급을 한 번 추첨한 뒤, 그 등급 안에서 균등하게 중복 없이 뽑는다. 부족하면 나머지 등급 후보에서 균등하게 보충한다. 카드 spawnRate는 유한한 양수인지 확인하는 후보 조건이며 카드별 추첨 가중치가 아니다. PoolData에는 현재 등급 가중치도 있다. 후보가 1~2장이면 남은 카드만 표시하고 0장이면 선택을 건너뛴다. 누락/잘못된 풀은 오류를 보고하며 다른 풀로 대체하지 않는다.

ApplyEffect/RemoveEffect는 chooseId별 획득 상태와 주/부/특수 효과를 처리한다. 드론 카드의 추첨 결과/설명은 DroneSelectionState와 GetChoiceDescription을 사용한다. 공유 SO에 이번 런의 획득/무작위 결과를 저장하지 않는다. 카드 수치와 효과 종류는 저작 SO 및 LevelUpEffectType/DroneSelectionKind가 근거다. 옛 G07 표를 새 카드 원본으로 사용하지 않는다.

## 연출과 정지

[InGameInstaller](../../Assets/WorkSpace/USW/Scripts/CoreSystem/InGameInstaller.cs)가 이벤트와 UI를 연결한다. LevelUpPresentation 폴더의 Reveal/Select/Collect/Peek/TimeDirector가 공개·선택·흡수·필드 강조를 분담한다.

[TimeScaleService](../../Assets/WorkSpace/USW/Scripts/CoreSystem/TimeScaleService.cs)는 소유자별 Pause/Request/Release 중 가장 느린 요청을 적용한다. [FieldPauseVisuals](../../Assets/WorkSpace/USW/Scripts/CoreSystem/FieldPauseVisuals.cs)는 새 공격을 먼저 보류하고, 이미 날아간 공격의 허용된 적중을 처리한 뒤 전투 정지와 대기 모션/이펙트의 실제 시간 재생을 구분한다. 한 UI를 닫았다고 다른 소유자의 정지를 해제하지 않는다.

[UI_Peekthrough](../../Assets/WorkSpace/HSD/Scripts/UI/Utils/UI_Peekthrough.cs)는 빈 곳/카드 홀드 시 필드를 보이게 하고 선택 입력을 잠근다. 복귀 페이드, 모든 터치 해제 및 해제 프레임이 지나야 새 선택을 받는다. 투명한 배경 Graphic의 raycast가 홀드 입력 경로이므로 등급 연출에서 오브젝트를 꺼 버리면 안 된다. 투명도만 바뀐 상태와 선택 가능 상태는 별개다.

보스 처치 [TotemRewardUI](../../Assets/WorkSpace/USW/Scripts/IngameUI/TotemReward/TotemRewardUI.cs)는 독립 보상 화면이다. LevelUpUI/구 TotemSelectUI의 UI_Peekthrough와 동일한 기능이 있다고 가정하지 않는다. 세 화면의 실제 연결·상호작용을 구분한다. 보상 세션 취소는 큐·콜백·트윈을 함께 정리한다.

게임/보상 순서는 [인게임 진행](../../design/gdd/ingame-system.md), 제작 중인 족장 참조는 [족장 문서](alphan-active-skill.md), 확장 기획은 [미채택 드론 카드 제안](drone-build-selection-design.md)에 있다.
