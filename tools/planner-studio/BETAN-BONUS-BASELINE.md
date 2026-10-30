# 베탕 브랜치 기준

2026-10-30. 기준 브랜치 `feat/betan-level-bonuses`, 확인 HEAD `df6bfe00`, 구현 커밋 `4e403925`.

브랜치 전환이나 전체 병합 없이 git show/diff로 비교했다. 베탕 원본 UnitData_Betan.asset은 현재 브랜치와 내용 차이가 없다. StatModifierBonus와 EveryNthCycleBonus 합성 방식을 기준으로 한다.

| 레벨 | 확정 효과 | 엑셀 값 |
|---|---|---|
| 3 | 공격력 +5% | StatModifierBonus / Attack / Percent / 5 |
| 6 | 매 2번째 기본 자폭 피해 +40% | EveryNthCycleBonus / basic_skill / Every=2 / DamageBonusPercent=40 / ChargeReductionPercent=0 |
| 9 | 일반공격 속도 +5% | StatModifierBonus / BasicAttackFrequency / Percent / 5 |
| 12 | 매 2번째 기본 자폭 충전시간 -30% | EveryNthCycleBonus / basic_skill / Every=2 / DamageBonusPercent=0 / ChargeReductionPercent=30 |
| 15 | 유닛 치명타 피해 +50%p | StatModifierBonus / CriticalDamage / PercentagePoints / 50 |

- 모든 해금은 누적. 추가 자폭(선택지)은 기본 자폭 주기 카운터와 주기 보너스에서 제외한다.
- Lv.6/12는 서로 다른 Part지만 같은 basic_skill 주기에 합성된다. Lv.15의 2번째 기본 자폭은 피해1.4배/충전0.7배다. 첫 번째 기본 자폭은 피해1배/충전1배다.
- 고정 숫자와 비율을 혼동하지 않는다. Excel=5% → 런타임0.05, Excel=50%p → 런타임0.5.
- 새 기본 설명은 원본 문구를 기준으로 하되 [자폭드론]에 기존 용어 링크를 연결한다. '선택지 추가 자폭 제외'를 명시한다. 효과가0인 부가 항목은 자동 설명에 표시하지 않는다.
- 베탕5개 설명 + 알팡5개 설명 + 유닛45개 효과 = 업로드55행. 기본 프로젝트와 새 작성용 Excel에 반영한다. 기존 사용자 초안 설명은 자동 덮어쓰지 않으므로 새 작성용 파일을 가져와 반영하거나 필요한 행만 편집한다.
- 다른 유닛8종은 기존 임시 제안값을 유지하며 베탕 브랜치의 승인된 설계라고 표시하지 않는다.
- 자폭 소유자 수명, 환급, 식량 서비스 등 브랜치의 다른 코드 차이는 이 작업에 병합하지 않는다.

검증: BetanBranchParityChecks가 브랜치와 동일한 원본 SO와 표 파서 결과를 12개 레벨 경계 및 각4회 주기로 비교한다. 이 비교는 컨트롤러 결과의 동등성이며 실제 전투 전체 회귀를 대체하지 않는다.
