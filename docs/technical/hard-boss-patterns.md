# HardIngameScene과 악어 HP 구간 패턴

2026-10-22 사용자 최종 결정. 구현 커밋 `610e8c7b`.

`Assets/Scenes/HardIngameScene.unity`를 Editor에서 열고 Play한 뒤 기존 시작 버튼과 소환 버튼을 사용한다. 기존 인게임 UI·드래그·전투·선택지·토템 보상·승천·성장 경로를 쓴다. 보스 선택/스킬/HP 조작 같은 별도 검사 버튼이나 미리 배치한 유닛은 없다. 이 씬의 HARD_INGAME SO는 악어와 도마뱀의 Hard 슬롯 두 개를 무한 반복하며 서버 운영 풀과 별개다. 출시용 HARD 모드 정책을 확정하지 않는다.

악어 skill1 지진은 기존 20초 반복이다. HP70%와 HP30%를 처음 통과할 때 각각 skill2~6 중 하나를 무작위로 선택해 1회 시전하며, 실제 기믹도 공용8종 중 무작위로 선택한다. 두 선택은 독립이며 같은 스킬/기믹이 다시 선택될 수 있다. 둘을 한 번에 넘으면 진행 중 시전 뒤 두 시전을 순서대로 처리한다. 이미 소비한 구간은 회복 후 재통과나 카운터로 재발동하지 않는다. 배치 조건을 충족하지 않는 후보는 다른 후보를 시도한다.

8종은 절단 사슬·틈새 장벽·폭풍 피난처·이동 폭탄·유도 포격·기억의 발판·봉인석 점령·붉은 신호다. 첫 판정 전 족장 카운터는 기존 허용3종만 가능하며 나머지는 이동·배치로 해결한다. 선택 화면 정지/사망/교체 때 시계와 소유 효과를 함께 정리한다. BossPatternController 하나가 공통 스케줄링을 소유해 기믹 드라이버와 이중 진행하지 않는다. Spine skill2~6는 실제 선택 기믹의 예고 시간에 맞춰 재생한다.

도마뱀의 위협·가시방벽·잠복은 기존 구현과 수치를 유지한다. 아직 커밋되지 않았던 도마뱀 구현/메타/HP 회복·타깃 제외·유닛 공속 소유 계약은 새 씬의 필수 의존성으로 함께 포함했다. 악어 프리팹의 기존 미커밋 외형·피격 변경은 커밋에 섞지 않고 패턴/Spine 매핑 배열만 포함했다. 종지기 보스와 전용 씬/UI는 이관하지 않았다.

`Data/BossPatterns/Hard/CrocusHpPhases.asset`가 구간 원본, `CrocusSkill2~6.asset`가 스킬/후보/머티리얼 연결, `Library/*.asset`가 실제 기믹 수치다. runtime에 SO를 수정하지 않는다. 원본 Imports는 Rclone 배포, 해당 `.meta`는 Git 관리라는 기존 정책을 따른다. HardIngameEntry는 Editor 직접 Play에서 연구 저장 로드를 요구하지 않게 하며 화면이나 계정 보상을 추가하지 않는다.

검증:

- Develop 실제 Play67 PASS: 일반 시작·소환·24라운드 후보/승천 진행, 5스킬×8기믹, HP70/30 단발/회복/동시통과, 카운터·정지·정리와 기존 도마뱀.
- 기존8기믹 실제 객체76 PASS. Unity 컴파일 오류0, 커밋 인덱스의 런타임 소스1015개도 별도 컴파일하여 미커밋 코드 의존성을 검사했다.
- 전체 `run-regression.ps1` 실행: Disigman160·실제투사체Play118, SelectionExpansion, DroneTotem, DroneSizeSpeed2707 통과. **DroneCardsChecks의 옛 DeltanCooldown/9109 최소 공격횟수 감소 기대값1건은 실패**했다. 10-22 사용자 결정은 9109를 감망 기폭 여운으로 재정의했으며 해당 카드 구현/기존 검사 코드는 이번 보스 커밋에서 변경하지 않았다. 전체 회귀 PASS로 간주하지 않는다.
- Android 실기/성능·최종 기믹 난도·사운드는 미검증이다. 원본 씬에도 있던 미해결 GUID6개는 새로 만들지 않았으며 씬의 missing script 수는0이다.

저작/재검사: `tools/verification/HardIngameAuthoring.cs`, `HardIngameChecks.cs`, `HardPatternLibraryChecks.cs`, `HardIngameReview.cs`를 기존 Unity Pipeline으로 실행한다. 실행 결과 원본은 `outputs/hard-develop-20261022/`다.
