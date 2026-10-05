# 인게임 EXP

원본: [ExpManager](../../Assets/WorkSpace/USW/Scripts/CoreSystem/Managers/ExpManager.cs), [ExpEffectController](../../Assets/WorkSpace/HSD/Scripts/UI/Effect/ExpEffectController.cs), [BossManager](../../Assets/WorkSpace/USW/Scripts/Boss,Enemy/BossManager.cs), [ExpLevelData](../../Assets/WorkSpace/USW/Data/ExpLevelData.asset).

인게임 레벨은 계정 PlayerLevel과 별개이며 최대 20이다. 최대 레벨이 무한 런을 종료하지 않는다.

## 지급 경로

BossManager.OnBossEntryed → ExpEffectController가 현재 BossBase.OnDamaged 구독 → CalculateExpFromDamage → 이펙트 도착 시 ExpManager.AddExp.

`획득 EXP = 실제 피해량 × 현재 보스 ExpMultiplier × LevelUpManager.ExpGainMultiplier`.

무한모드는 BossManager가 `RuntimeBossStats.ExpReward / 양자화된 MaxHp`로 배율을 설정한다. 따라서 HP만 성장하면 총 EXP가 자동으로 늘지 않는다. 유한 BossEntry는 데이터 배율 또는 제한된 G02 fallback을 사용한다. '항상 피해량=EXP'와 '사망 이벤트에서만 일괄 지급' 모두 현재 연결을 설명하지 못한다.

ExpEffectController는 짧은 구간의 EXP를 묶어 비행시킨다. 선택 유예 중 이미 발사된 공격의 EXP는 즉시 지급하고 연출에는 보상을 넣지 않는다. 연출 경로/프리팹이 없거나 timeScale이 0이면 즉시 지급하는 fallback이 있다. OnDisable은 누적/비행 중 미지급량을 정산하고, 종료 런은 ExpManager가 추가 지급을 거부한다. 중복 지급을 막기 위해 도착 시 Flight.Reward를 먼저 0으로 만든다.

## 레벨과 필요량

일반 ExpLevelData 저장값: 1→2부터 **600 / 1300 / 2800 / 3600 / 3600 / 3100**, 이후 **4000 반복**. 튜토리얼은 별도 TutorialExpLevels를 사용한다. SO 미연결 시 코드 fallback 표가 있으나 일반 저작 기준값으로 사용하지 않는다.

AddExp는 초과량을 이월하며 한 호출에서 한 단계만 처리한다. Playing이며 FieldPauseVisuals의 공격 보류가 없으면 OnLevelUp을 발행하고 아니면 보류한다. FlushPendingLevelUp과 UI의 후속 정산을 함께 확인한다. DeferLevelUps는 튜토리얼 상호작용 중 발동을 미룬다.

AddExpFromDamage API는 남아 있지만 현재 ExpEffectController 경로에 다시 연결하면 중복 지급된다. [선택지](levelup-system.md), [EXP 공간 경로 제작](exp-effect-route-authoring.md)을 함께 읽는다.
