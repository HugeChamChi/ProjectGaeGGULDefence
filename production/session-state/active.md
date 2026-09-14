# Session State — 2026-09-13

## 다음 새 대화에서 한 번 상기 — 사용자 요청 (전달 완료: 2026-09-12, Codex)

- 전달 기록: 사용자의 “뭐 해야하는거 있었는데 확인좀” 요청에 족장별 레벨업 SO/공유 카드/향후 시트 전환 논의를 상기했다. 이번 세션은 기록 확인만 수행했으며 이 파일의 전달 상태만 갱신했다. 다음 작업과 미결 질문은 아래 기록 유지. 코드/Unity 자산 변경 및 테스트 실행 없음, 반쯤 편집한 씬/프리팹 없음.

- 사용자는 컴퓨터를 끈 뒤 새 대화로 돌아올 예정이다. 다음 세션에서는 첫 질문의 주제와 관계없이 아래 내용을 짧게 한 번 상기한 다음, 사용자가 실제로 요청한 질문/작업을 이어간다.
- 상기 문구: “지난번에 족장별 레벨업 선택지를 SO로 먼저 만들고, 공통 카드를 여러 풀에서 공유하는 구조와 향후 시트 전환 방향을 이어서 이야기하기로 했어.”
- 참고: `docs/technical/chieftain-levelup-pool-design.md`.
- 상기를 전달한 에이전트는 이 항목을 ‘전달 완료’로 바꾸고 전달 날짜를 기록한다. 이후 매 질문마다 반복하지 않는다. 이번 기록 세션에서는 완료 처리하지 않는다.
- 이 요청은 다음 대화의 안내이며 자동 구현 착수나 사용자의 새 요청을 대체하라는 뜻은 아니다.

<!-- STATUS -->
Epic: 토템 TD1007~TD1010 확장
Feature: 그림자 일반 공격 재현 / 사각 고리별 버프 / 공속 감소 / 식량 생성 재사용
Task: 코드·SO 프리셋·검증 완료(확장 34/기존 토템 48/디버프 36). 사용자 SO/프리팹 연결 후 실제 애니메이션·PlayMode·Android 검증.
<!-- /STATUS -->

## Codex 완료 — TD1007~TD1010 (2026-09-13)

- 사용자 요청: SPR_TD1007 그림자, TD1008 이중 범위, TD1009 공속 감소/공격력 증가, TD1010 식량 생산. 인접 8칸=max(abs(x),abs(y))=1, 바깥 16칸=2, 중심 제외. SO 수치/실제 콘텐츠는 사용자가 조정.
- TD1007 사용자 확정: 일반 공격만, 연발·다중 발사와 적중 효과 재현, 0.2초 뒤 원본 최종 피해/치명타 결과까지 복사. 원본 대상 살아 있으면 유닛 이동/범위 이탈해도 예약 실행, 판매 시 취소(판매 외 아군 사망 없음). 반투명 스프라이트 복제. 그림자 범위 모양은 미지정이므로 SO effectRanges로 설정.
- 구현: TotemShadowAttack/Settings, BasicAttackReplay로 공격 시작/연발 문맥 및 각 투사체 발사 기록. 일반 onAttack/스킬/보너스 공격을 재실행하지 않으며 중첩 영역도 한 번만 활성화. 토템 제거/재배치 시 이전 예약 무효화. ShadowAttackVisual은 Transform/SpriteRenderer만 복제·재사용, 원본 Attack 클립 샘플링, 클립 없으면 정적 외형. 실제 캐릭터 애니메이션 연결은 미검증.
- 정확한 피해: IReplayableEffect, RecordedHitEffects, RecordedDamage 추가. AtkCoefficientDamage/FixedDamage Capture 지원(패시브 변형 상속 포함). BossBase.TakeDamageAndRecord/ApplyRecordedDamage로 원본 최종 단위 보관, 방어력 재계산 없이 재현하되 무적/전투 상태/남은 HP 존중. 미래 무작위 피해 IEffect는 IReplayableEffect 구현 필요. 비피해 적중 효과와 추가 연출은 다시 적용, TD1006 공격 시도 아머는 다시 적용하지 않음.
- TD1008/9: TotemSquareRingRange, SquareRingBuffFunction 추가, SimpleBuffFunction의 유한 음수 지원. GenericBuffTotem 재사용. TD1010 기존 TotemFoodGenerator/FoodGeneratorFunction 재사용. 새 시트 로더 없음, 기존 시트 양수 변환 경로는 그대로.
- 수정 파일: TotemData, SimpleBuffFunction, UnitBase(Combat 공개), UnitCombatComponent(일반 공격 기록 경계), MultiShotSkillAction(연발 문맥 전달 및 ID=0 기본 투사체), AtkCoefficientDamage, FixedDamage, BossBase, Editor/TotemDataValidator. 신규 파일: 위 클래스들 + Editor/TotemExpansionPresets, TotemExpansionChecks, Tests/TotemReplayProbeEffect 및 Unity 생성 meta.
- 프리셋: Assets/Totems/Set TD1008 Dual Ring(+10%/+10%), TD1009 Attack Tradeoff(+50%/-20%), TD1010 Food Generator(기존 10초/30식량). 모두 제작 시작용 예시 수치, 선택 SO의 기능/범위 교체·Undo 지원, ID/주소/프리팹은 자동 변경하지 않음.
- 검증: Unity 컴파일 completed/failed=false/errors=[]; 최종 eval에서 Error/Exception/Assert 로그를 수집해 0건 확인, TotemExpansionChecks PASS 34 / TotemBehaviorChecks PASS 48. 순수 C# 디버프 36 PASS. 중간 테스트의 edit-mode Destroy 오류는 테스트 임시 분신의 DestroyImmediate 정리로 수정 후 재검증. 한 eval 응답 시간 초과는 Unity에서는 완료된 것을 로그로 확인, 최종 eval 정상 응답. git diff --check 통과.
- 문서: docs/technical/totem-expansion-td1007-1010.md 신규, totem-so-authoring-guide.md 링크, totem-sheet-schema-redesign.md 음수 지원 상태 현행화. 실제 SO/프리팹/씬 변경 없음. 프로젝트 설정 등 외부 변경 보존. 임시 씬/SO/이벤트 정리 완료, 반편집 Unity 자산 없음, 커밋/푸시 없음.
- 다음: 사용자 제작한 SO와 전용 프리팹(TD1007 TotemShadowAttack / TD1008·9 GenericBuffTotem / TD1010 TotemFoodGenerator) 연결 및 등장 풀 등록. 분신 클립/정렬/각 캐릭터 연출과 실제 PlayMode/Android 성능은 콘텐츠 연결 후 확인. 족장 레벨업 풀 구현은 아래 완료 상태 유지.

## Codex 완료 — 족장 레벨업 독립 풀 (2026-09-13)

- 사용자 요청: 합의된 풀 교체 작업 진행. 게임플레이 프로그래머 스킬 적용, 별도 에이전트 없음. 이전 미착수 기록은 아래의 과거 이력이다.
- 새 파일: LevelUp/LevelUpPoolData.cs, ILevelUpCatalog.cs, LevelUpCatalog.cs, Editor/LevelUpPoolChecks.cs 및 Unity 생성 meta. 풀=ID+카드 목록, 카탈로그=SO 기반 ID 조회/참조 검증, 런 상태와 효과=기존 매니저. 별도 DI 서비스 등록 없이 Init에서 데이터 카탈로그 구성.
- 수정: LevelUpManager 공용/파티 풀 자동 합산 및 GameDataManager 시트 구독/클론 제거, ChieftainSpawner 주입으로 소환과 같은 우선순위의 풀을 시작 시 고정. UnitData 및 ChieftainData에 LevelUpPool 필드, ChieftainSpawner.GetSelectedLevelUpPool 추가. PartyDataSO 기존 exclusiveLevelUpChoices와 매니저 levelUpPool은 이관용 숨김 보존(런타임 미사용). LevelUpUI는 0장일 때 Hide로 진행 복구. 중복 효과 적용 방지.
- 실제 로비는 PartyDataSO.chieftainData(UnitData) 경로이므로 풀은 해당 UnitData에 연결한다. 레거시 Chief ID 경로만 ChieftainData 풀 사용. 테스트 UnitData 경로 지원. 런 도중 교체 시 풀 고정, 새 씬 런에서 새 획득 상태.
- 0장 정책은 2026-09-13 사용자 답변으로 확정: 이번 선택을 건너뛰고 게임 계속. 기존 구현과 같아 후속 코드 변경/재검증 없이 설계 문서와 이 기록만 갱신했다. 1~2장 그대로 표시, 반복 보상 없음. 실제 카드 목록/ID/콘텐츠 연결은 사용자 작업. 기존 효과 enum/버프/디버프 통합 재설계 및 시트 로더 추가 없음.
- 검증: Unity recompile completed/failed=false/errors=[]; LevelUpPoolChecks.Run eval 성공, Editor.log PASS 26 assertions 확인. 풀 간 공유/격리, 누락 및 중복 ID, 실제 선택 전달, 획득/중복 적용/제거/새 런, 부족 조건 및 유효 가중치 검증. git diff --check 통과. 콘텐츠 PlayMode/Android 미실행.
- docs/technical/chieftain-levelup-pool-design.md 상단에 구현 상태/제작 방법 추가. 기존 IngameScene.unity 외부 수정과 삭제 SO 등 사용자 변경 보존, 씬/프리팹 직접 편집 없음. Preview Scene/임시 SO/GlobalData.SelectedParty 복원 완료. 커밋/푸시 없음.

## Codex 완료 — TD 토템 SO 제작용 기능 (2026-09-13)

- 후속 검토 요청: 사용자가 Claude의 족장 레벨업 풀/시트 설명을 전달하여 문서·코드 재대조. 독립 풀·카드 공유·공통 풀 자동 합산 없음·SO 우선 방향은 합의이며 "미결"이 아니라 구현 미착수. 현행 LevelUpManager는 여전히 공용+파티 풀 자동 합산/시트 동기화, LevelUpData.ApplySheetData 존재. LevelUpPoolData/LevelUpCatalog는 아직 없음.
- 별도 docs/technical/totem-sheet-schema-redesign.md(GPT 전달용)도 발견/검토. 시트 작업 브리핑은 이 파일일 가능성도 있어 사용자에게 구분 안내. 신규 Effect_Def/use_sheet_data/effect 슬롯/범위 stage 파서는 미구현. 문서의 인구수 미정/TD1005 미구현을 현행화하고 SO 대 시트 범위 소유권 혼재, 음수 값 미지원, 효과 키 공유에는 명시적 매핑 필요를 검토 메모로 추가. 라이브 시트는 재조회하지 않음. 선택지/신규 시트 구현에는 착수하지 않았다.

- 사용자 승인: 기능 스크립트와 정리는 Codex, 실제 SO는 사용자가 제작. 인구수 시스템 폐기. TD1002는 해당 토템 배치 이후 처치만, TD1005 합성/판매는 원본 등급/데이터 기준으로 확정.
- 구현: TD1001 TotemBonusProjectile(일반 공격 구독/확률/계수/투사체 SO), TD1002 TotemKillRangeGrowth(처치별 범위, 이동/회전 누적 보존), TD1004 TotemWildcardSpawner/TotemSpawnCharge/WildcardUnit(전투 중 충전, 완충 빈칸 대기, 인접 링 생성, 인구수 없음), TD1005 TotemTemporaryTierBoost/UnitBase 임시 데이터/등급 API(원복/상위 스킬 SO 교체). TD1003/1006은 기존 경로 재사용.
- 기존 파일 수정: TotemData SO 설정/UseSheetData 토글, TotemBase 시트 동기화 가드 및 인구수 차감 제거, TotemSpawner 인구수 제한 제거, PopulationManager 토템 대기열 인구수 가드 제거. UnitBase 첫 공격 전 토템 재계산/원본 데이터 보관/조커 인구수 차감 제외. MergeManager 원본 등급/조커 양방향 매칭/선택 유닛 필수 포함. UnitSpawner 판매 계산 전 원본 복구. 기존 인구수 시스템 전체 참조를 삭제한 작업은 아니며 타 시스템에는 잔존 코드가 있다.
- 신규 파일: TotemBonusProjectile.cs, TotemBonusProjectileSettings.cs, TotemGrowthStage.cs, TotemKillRangeGrowth.cs, TotemWildcardSpawnSettings.cs, TotemWildcardSpawner.cs, TotemSpawnCharge.cs, TotemTierUpgrade.cs, TotemTemporaryTierBoost.cs, UnitMergeRules.cs, WildcardUnit.cs, TotemSpawnGaugeUI.cs, Editor/TotemDataValidator.cs, Editor/TotemBehaviorChecks.cs, Tests/TotemCheckProjectile.cs 및 Unity 생성 meta.
- 도구: Assets/Totems/Validate Selected SO, Assets/Totems/Set TD1002 Growth Ranges, Tools/Totems/Run Behavior Checks. 프리셋 편집은 사용자 메뉴 실행 때만 SO 변경, Undo 지원. 실제 SO/프리팹은 새로 만들지 않았다.
- 문서: docs/technical/totem-so-authoring-guide.md 신규, totem-batch-td1001-1006.md 현행화. 기존 totem-system.md는 외부에서 삭제되어 복구하지 않았다. 이 기록이 아래 미구현/미승인/런 전체 처치 기준을 대체한다.
- 검증: Unity 6000.3.11f1 live editor recompile 오류 없음; 2026-09-13 KST Preview Scene 테스트 48개 PASS. SO 보존/시트 호환, 충전, 조커 합성 대칭, 원본 등급, 성장/회전/제거, 공격 구독 중복 방지, 실제 풀 호출→적중 피해/원래 보스 유지, 겹친 아머 공격당 +1/재계산 무부여/제거 후 유지 확인. 투사체 비행은 편집기 대체 투사체로 제어. 기존 dotnet 디버프 36개 PASS. git diff --check 통과. 실제 콘텐츠 PlayMode/Android 미실행.
- 다음: 가이드대로 SO/특수 프리팹 제작 후 TotemSelectUI.totemPool 등록. SO 전용은 UseSheetData OFF. TD1005 스킬이 다른 상위 데이터는 TierUpgrades 등록(기본은 같은 UnitData의 다음 등급 수치). 프리팹/행동 클래스 교체와 이미 생성된 드론/버프 인스턴스의 소급 재작성은 하지 않는다. TD1004 게이지는 TotemSpawnGaugeUI로 연결.
- 외부 변경: 기존 선택지 SO/이미지/LevelUpDataGenerator 삭제, LevelUpManager/LevelUpSpecialEffect 수정, Addressables Data.asset 참조 정리가 관찰됨. 복구하거나 이 작업 결과로 주장하지 않는다. 반편집 씬/프리팹/ProjectSettings 없음. Preview Scene 정리 완료, 사용자 씬 저장 없음. 커밋/푸시 없음.

## Codex 추가 검토 — 선택지 풀 의존성과 효과 재사용 (2026-09-12)

- TD1001~1006 실제 구현 조사: Totem 폴더 전 스크립트 목록, Assets의 ID/조커/등급업/투사체 관련 검색, UnitCombatComponent/MergeManager 및 TotemBase/Spawner/BuffManager/기존 BossKillStack 확인. TD1001 범위 내 일반 공격 확률 보조 발사 전용 연결 없음(기존 공격 이벤트/ProjectilePool 재사용 가능). TD1002 기존 BossKillStack은 전역 공격/공속 수치 누적이며 범위 확장과 다르고 배치/제거 시 카운터 초기화. TD1004 조커 소환/합성 훅 없음(현재 합성은 동일 UnitData+등급 요구). TD1005 임시 등급 상승/원복 경로 발견 못함. TD1003/1006은 공통 발사/적중 화상 및 셀 출처→일반 공격 아머 코드 있음. 로컬 Assets 검색에서 신규 6종 TotemData/전용 등록은 발견 못했고 totemId 직렬화 결과는 OldTreeStub(0)만 확인. 원격 시트 직접 조회/PlayMode 검증은 미수행.
- 권고(미승인): 데이터는 수치/범위/효과 참조, 별도 동작 스크립트는 이벤트/시간/상태 담당. 기존 TotemBase 및 특수 프리팹 경로로 TD1001 보조 발사, TD1002 성장 범위, TD1004 조커 생성, TD1005 임시 등급 적용 구현. 효과별 재사용 이름을 쓰고 ID별 클래스 분기는 피함. TD1003 발사기/TD1006 공통 Binding은 재사용, 추가 전용 클래스 필수 아님. 범용 행동 프레임워크 신설은 보류. SO 우선 시 시트 functions 덮어쓰기 정책 정리. 구현 순서 제안 1003/1006 연결→1001/1002→1004/1005, 후자는 인구수/합성 및 등급 원복 계약 확정 필요. 이번 변경은 배턴만, 코드/Unity 자산 변경 및 테스트/커밋 없음.

- TD 배치 문서 재확인: 사용자 지목으로 docs/technical/totem-batch-td1001-1006.md 전체 확인. 신규 6종은 기존 33종 대체 전제이며 일반 수치 버프만이 아님. TD1001 일반 공격 시 확률 보조 투사체, TD1002 처치 누적 범위 확장, TD1003 화염구/화상, TD1004 조커 소환, TD1005 임시 등급업, TD1006 공격 시 아머 누적. 앞선 '현재 구조 유지' 판단은 범위/데이터/실행 분리 골격에 한정하고 6종이 GenericBuffTotem만으로 구현된다는 의미가 아님을 설명. 문서상 TD1004 인구수 및 합성/TD1005 등급 전환 미결, 콘텐츠 연결/실전 검증 미완. 이번에는 문서 확인만 했고 6종별 현행 코드 구현 여부를 새로 전수 검증하지 않음. 배턴 외 변경 없음.

- 후속 토템 검토: 사용자 질문에 TotemData/ITotemFunction/GenericBuffTotem/RangedBuffTotemBase/TotemBase/TotemBuffManager/TotemDebuffEmitter 및 기술 문서 확인. 범위와 기능 조합, 셀 재계산과 실제 공격/적중 디버프 분리는 유지 권고. ITotemFunction은 컨테이너 주입은 없지만 TotemBuffManager를 실행 인자로 받아 타입 의존성은 있음. TotemBase의 공통 전투 의존성과 TotemBuffManager의 IObjectResolver 조회는 추후 축소 가능하나 전면 재설계 사유는 아님. 주의: TotemData.ApplySheetData는 functions 목록을 단순 버프로 전면 재구성하므로 SO 조건부/특수 기능 보존 정책을 정해야 함. 셀 재계산 안에서 지속 버프/디버프를 반복 부여하면 중첩/시간 갱신 오류 위험. 코드 변경/실행 검증 없이 구조 검토만 수행.

- 사용자 의도: 향후 아웃게임 족장 선택에 따른 독립 카드 풀. 풀에는 DI 의존성이 없고 필요할 때 버프/디버프 기능을 사용하길 원함. 현행 설계가 충분하다면 불필요한 변경은 원하지 않음. 과거 IEffect/Provider/IBuff 조합의 재사용 의도 및 모바일 후처리 최적화 의견도 검토 요청.
- 코드 확인: LevelUpData에는 DI 주입이 없지만 ApplySheetData 인자가 GameDataManager.LevelUpSheetRow라 타입 결합은 있음. LevelUpManager는 8개 의존성 주입, 풀 합산/추첨/시트 동기화/효과 적용/런 상태를 함께 담당. 공용 풀 + 파티 전용 풀 합산은 독립 족장 풀과 다름. GrantCourageBuff는 전용 enum과 매니저의 courageBuffData 참조로 처리.
- IEffect는 Apply(UnitBase caster, BossBase target, Vector3 hitPosition)로 적중 전용. SkillData.hitEffects는 이를 사용하나 LevelUpData는 enum/수치 방식. 아군 버프는 BuffData/IBuffEffect/BuffController, 디버프는 DebuffDefinition/DebuffController 경로. 현재 IBuff 인터페이스는 검색에서 발견하지 못함.
- 권고(미승인): 기존 SO/버프/디버프 구현 유지, 풀/추첨과 효과 실행 경계만 분리. 풀은 카드 참조만 보유하고 추첨에 필요한 상태는 인자로 전달. 실행부가 필요한 기능을 제공하고 카드/SO 자체의 컨테이너 조회는 피함. 기존 IEffect를 모든 선택지에 그대로 쓰기는 대상 계약이 맞지 않음. 적중 디버프와 선택 후 향후 공격에 디버프 부여를 해금하는 효과를 구분해야 함. 기존 DebuffBinding과 새 효과의 중복 적용 주의. 범용 카탈로그/IBuff 통합은 현 단계 필수 아님.
- 후처리 의견: Unity 6.3 공식 문서 확인. Bloom HQ Filtering off, Render Scale/FXAA 타협은 타당하나 후처리도 GPU/대역폭 비용이 있어 드로우콜만으로 안전을 단정할 수 없음. 프로젝트/실기기 성능 측정은 하지 않음.
- 변경: 이 배턴만 갱신. 코드/씬/프리팹/설정 변경 없음, 테스트 실행 없음. 반편집 Unity 자산 없음. 다음은 사용자가 원하는 효과 실행 범위(즉시 부여/공격 시 부여 해금 등)에 맞춘 최소 구조 확정. 기존 미결 정책 및 디버프 후속 작업 유지. 커밋/푸시 없음.

## Codex 완료 — 족장별 레벨업 풀 설계 메모 (2026-09-12)

- 사용자 요청: SO 프로토타입, 족장별 독립 후보군, 여러 풀에서 공통 카드 공유 방향을 문서로 보존. 이번 요청은 구현이 아니라 다음 작업용 기록이다.
- 합의: 공통 풀 자동 합산 없음. 각 족장 풀에 카드 명시, 카드 정의/ID는 여러 풀에서 재사용. SO부터 시작하고 향후 시트 전환을 위한 카탈로그 조회 경계 유지.
- 신규 문서: `docs/technical/chieftain-levelup-pool-design.md`. 기존 LevelUpData/ChieftainData를 확인해 현행 필드와 신규 제안 구분. 이미 ApplySheetData가 있으므로 기존 시트 경로를 없다고 가정하지 않도록 명시.
- 변경 파일: 위 문서와 `production/session-state/active.md`만. 코드/씬/프리팹/meta/시트 변경 및 테스트 실행 없음. 반쯤 편집한 자산 없음. 커밋/푸시 없음.
- 다음: 기존 족장 선택 전달/시트 반영/추첨 경로 확인 후 LevelUpPoolData·LevelUpCatalog와 족장 연결 구현 검토. 실제 카드 구성·ID, 후보 부족/0개 처리, 풀별 가중치, 런 중 족장 변경은 미결. 기본 보상 카드와 로컬 JSON 폴백은 추천 이력이며 승인된 결정으로 취급하지 않는다.
- 기존 디버프 시트/gid·TD1003/TD1006 콘텐츠 연결 및 실전 검증 과제는 취소되지 않았으며 아래 인수인계에 유지한다.
- 구현 전 주의: LevelUpManager에 이미 공용 levelUpPool + SelectedParty.exclusiveLevelUpChoices 자동 합산 경로가 존재한다. 이번 합의와 차이가 있으므로 파티/족장 관계 확인 후 이관하고 중복 경로를 만들지 않는다.

## Claude 완료 — 유닛 확장 패턴 문서 정정 (2026-09-12)

- 배경: 레벨업 선택지에 디버프 부여 기능을 논의하다 `AGENTS.md`의 "Unit Extension - Template
  Method Hooks" 섹션이 실제 코드와 어긋나 있는 걸 발견. 예시로 든 `FrogAlchemist`/`OnGaugeFull`은
  현재 코드에 없고, Frog 유닛 로스터 자체가 "용사 파티" 컨셉(궁수/마법사/사제/도적/전사)으로
  재설계되어 있었음.
- 확인: 유닛 확장 방식이 두 갈래로 공존 중 — (1) `SkillData`(`ISkillAction`+`IEffect` 조합,
  `MultiShotSkillAction`/`AtkCoefficientDamage`/`FixedDamage`/`BuffAllyInRangeSkillAction` 등)로
  구현하는 데이터 주도 방식, 현재 Frog 파티원(Archer/Mage/Priest/Rogue/Warrior) 전부가 빈 클래스로
  이 방식만 사용. (2) `UnitBase` Template Method 훅(`OnUnitPlaced`/`OnUnitRemoved`/`OnSkillFull`) —
  `DroneUnits/`(DroneProducer/DroneFoodProducer/DroneChieftain/DroneBuffer/DebuffDroneUnit/
  DroneSpawnerBase)가 지금도 활발히 사용 중. 둘 다 유효한 패턴이며 폐기된 쪽은 없음.
  `UnitCombatComponent.ExecuteSkill()`은 `InvokeOnSkillFull()`(공용 DebuffBinding 적용 포함)을 먼저
  호출한 뒤 `skillData.action`을 실행하므로, 한 유닛이 둘 다 쓰면 같은 효과를 중복 적용하지 않도록
  주의 필요.
- `UnitTribe` enum도 `UnEmployed/Gunner/Ninja/Wizard` → `Warrior=4/Mage=5/Archer=6/Rogue=7/Support=8`로
  개편되어 있었고(`UnitTribe.cs:1` 주석: 구 로스터 제거, 직렬화 보존 위해 정수 고정), 유닛 데이터
  에셋 경로도 `Data/IngameUnitData/`(존재하지 않음) → `Data/HeroUnits/`로 이동해 있었음.
- 변경 파일: `AGENTS.md`("Unit Extension" 섹션 전체 교체 — 두 패턴 공존/선택 기준/실행 순서·중복
  적용 주의 명시), `.claude/docs/directory-structure.md`(FrogUnits 파일 목록, UnitTribe.cs 설명,
  Data 폴더 경로, Upgrade 직업 타입 서술 4곳 정정), 이 배턴.
- **시트 미확인 항목**: Upgrade 직업 타입 문자열은 코드에 하드코딩되어 있지 않고 구글 시트
  `CharacterType` 컬럼에서 `GameDataManager.UpgradeTypes`로 동적 로드된다(`GameDataManager.cs:671-673`
  확인). 옛 문서에 있던 `Frog_Gunner`/`Frog_Ninja`/`Frog_Wizard`/`Frog_Chief` 예시 문자열은 이미 제거된
  구 로스터 기준이라 사용자 지시로 그냥 삭제했고, 실제 현재 시트의 `CharacterType` 값이 무엇인지는
  이번 조사로 확인 못함 — 시트 작업자/다음 세션이 직접 시트를 봐야 함.
- 범위에서 미룬 것: `ChiefUnit`/`PassiveData`/`ISkillAction`(`MultiShotSkillAction` 등) 클래스들이
  `directory-structure.md`의 `Unit/` 폴더 파일 목록 자체에는 아직 안 나와 있음 — 사용자 승인으로
  이번 범위에서 제외, 필요 시 별도 진행.
- 코드/씬/프리팹/meta/ProjectSettings 수정 없음(문서만). commit/push 없음.

## 최신 인수인계 — 공통 디버프 구현 (2026-09-11, Codex)

이 절이 아래 과거 기록의 FK/HP/시간 경계 미정 및 구현 미착수 상태를 대체한다.

- 사용자 확정: DebuffBinding 저장 / IDebuffSource 조회 / 보스 소유 DebuffController. 등급별 유닛 FK 및 토템 FK. HP×10000 fixed-point long, decimal 입출력, 소수 4자리 AwayFromZero, 사망은 정확한 0.
- 화상: 첫 틱 1초, 한 슬롯, 재적중 스냅샷·종료 갱신/다음 틱 유지, 동시 시각 기존 틱 우선, T/I 정수, 최종 잔여 보정. 아머: 토템이 겹쳐도 일반 공격 시도당 총 +1, 보스 최대 50, 토템 제거 후 스택 유지.
- 마지막 사용자 확정: 제한시간까지 도래한 피해 먼저 처리, 처치 시 성공. CombatCountdown의 시간초과 전 보스 갱신과 HP 0 즉시 타이머 중단으로 사망 연출 중 패배 방지.

### 변경 파일/범위

- 신규 `Assets/WorkSpace/USW/Scripts/Combat/Debuff/`: Binding/조회/정의/Catalog/효과별 상태/Controller/CombatHealth/CombatCountdown/토템 발사 컴포넌트 및 meta.
- 신규 Resources/DebuffSettings 및 Debuffs 3개 SO/meta, Editor/DebuffDataValidator, GameDataManager 경로의 DebuffSheetParser.
- 기존 UnitData/UnitBase/UnitCombatComponent/DamageCalculator, TotemData/TotemBase/TotemBuffManager: FK 조회 및 공통 적용. DroneManager·DebuffDroneUnit 전용 상태/필드 제거, DroneUnit_Debuff.prefab 필드 제거, UnitData_DebuffDroneUnit.asset 4등급 FK 이전.
- BossBase/BossManager/WaveData/TimerController: fixed-point HP·decimal 이벤트·방어력·대상별 수명·제한시간 순서. BossPatternController와 InGameLifetimeScope: singleton 제거 및 DI/패턴 취소 수명 정리.
- RootLifetimeScope/GameDataManager: 설정·카탈로그 DI, Debuff CSV/기존 시트 FK 파싱·검증, decimal 보스 HP. UIManager/DamageFloaterManager/ExpEffectController: decimal 이벤트 경계 대응.
- 문서: `docs/technical/debuff-system-design.md` 현행, `debuff-system-design-history.md` 과거 제안 보존, `debuff-sheet-guide.md` 시트 작업 정본, `debuff-verification.md` 증거. 기존 damage-formula-reference/totem-batch 문서 현행 상태 갱신.
- 검증 하네스: `tools/verification/debuff/` 실제 순수 C# 소스 링크, 36개 검증. Unity 생성 csproj는 로컬 검증용으로 동기화했으며 Git 추적 대상 아님.

### 검증/다음 작업

- 순수 검증 36/36 PASS. Unity 최종 배치 스크립트 컴파일 성공, DebuffDataValidator 로컬 드론 binding 4개 PASS, 종료 코드 0. 상세/한계는 `docs/technical/debuff-verification.md`.
- 시트 작업자에게 `docs/technical/debuff-sheet-guide.md` 전달: 새 Debuff 탭/gid, 유닛·토템 FK, TD1003/TD1006 데이터·리소스·등장 연결. 원격 시트는 아직 변경하지 않았고 현재 로컬 SO fallback 사용.
- 이후 PlayMode에서 드론/화염구/겹친 아머/이동·회전/제거/소수 HP UI/경험치/일시정지/막판 처치 확인. Android 실기기 검증 미실행.
- 공통 디버프의 미결 설계 질문 없음. 전체 유닛 공격 스탯/재화/엔드리스 성장 대형 수 전환과 다른 토템 구현은 별도 범위.
- 반쯤 편집된 씬/프리팹/meta 없음. 커밋/푸시 없음. 기존 사용자 이미지·토템 삭제 및 ProjectSettings 변경 보존. Unity 실행에서 Addressables Data.asset/Default Local Group.asset의 삭제 자산 참조 정리가 관찰되어 작업 트리에 남김; 검토 시 선행 삭제와 함께 확인.

---

> 이 파일은 교대 협업의 **배턴**이다 (`docs/AI-COLLAB-PROTOCOL.md` §3).
> 세션을 끝내기 전 활성 에이전트가 갱신하고, 다음 에이전트는 이 파일을 **가장 먼저** 읽는다.

---

## 작업 브랜치
`Develop`

---

## 이번 세션에서 완성한 것 (2026-09-11)

**HSD 인수 반영 + 지침 통일 (Claude×Codex 한 마인드 구조 확립)**

### 만진 파일
| 파일 | 변경 내용 |
|------|-----------|
| `.claude/docs/directory-structure.md` | HSD "관여 금지" → "USW 인수"로 변경. 빈 JSY/KMS 참조 완전 삭제. 영역 요약표 USW 단독화 |
| `AGENTS.md` | **공용 규칙 정본화** — 정본 선언 + AI Collaboration 요약 + CancellationToken 소유 표·Allowed Libraries·Code Quality 흡수 |
| `CLAUDE.md` | 얇게 재작성 — `@AGENTS.md`로 공용 규칙 로드, Claude 전용(coordination/context/design 기준)만 유지 |
| `.claude/docs/coding-standards.md` | 슬림 — 중복 규칙 제거, 디자인문서 기준 + 검증 기준만 |
| `.claude/docs/technical-preferences.md` | 슬림 — 중복 제거, 플랫폼/테스팅/ADR/스페셜리스트 라우팅만 |
| `README.md` | 브랜치 예시 `KMS_Grid` → `USW_Grid` (죽은 팀원 참조 제거) |
| `docs/AI-COLLAB-PROTOCOL.md` | **신규** — Claude×Codex 협업 합의문 |
| `Assets/WorkSpace/JSY`, `KMS` (+`.meta`) | 빈 폴더 삭제 |

> 참고: Claude 자동 메모리 2건도 갱신(`project_hsd_handover`, `project_ai_collab_instruction_arch`)했으나 **repo 밖**이라 Codex는 못 봄 → 핵심 내용은 위 repo 파일에 이미 반영됨.

---

## 다음 할 일

- 다음 단계: 하네스가 알아야 할 DI 수명·초기화, UI Presenter/View, 데이터·리소스 구조를 코드 기반으로 정리. 이번 요청의 선행 최신화는 완료했으며 상세 구조 작업은 아직 시작하지 않음.

---

## 열린 질문 (사용자 결정 대기)

- 없음. (지침 변경 + 빈 폴더 삭제 모두 **미커밋 상태로 유지** — 사용자 지시로 커밋 안 함.)

---

## 미완 상태 경고

- 반쯤 편집한 씬/프리팹 **없음**.
- 단, git 워킹트리에 **이번 작업과 무관한 미커밋 변경 다수** 존재 (HpBar 연출, GameDataManager DI 리팩토링, URP/셰이더, TutorialManager 등). 이는 별개 작업 흐름 — 이번 세션이 건드리지 않았음. 커밋 시 범위 분리 주의.

---

## Codex 후속 조사 — HSD 리팩토링 / 하네스 (2026-09-11)

> 아래는 수정 전 조사 기록이다. 지적 사항의 처리 결과는 다음 완료 기록을 참조한다.

- 요청: HSD 리팩토링이 스킬과 하네스에 반영됐는지 파악. 구현·규칙 수정은 수행하지 않음.
- 확인한 불일치:
  - `AGENTS.md`는 `Manager.Xxx` / `InGameSingleton<T>`를 요구하지만 현재 `Manager.cs`는 멤버 없는 정적 클래스. `GameManager`는 MonoBehaviour, `GameDataManager`는 순수 C# 클래스이며 Root/InGame LifetimeScope와 주입 기반 초기화가 존재함. HSD뿐 아니라 USW에도 영향.
  - `.agents/skills/claude-docs-reference/references/docs/directory-structure.md`에는 HSD/JSY/KMS 관여 금지 문구가 남음. 같은 참조 묶음의 `technical-preferences.md`에는 Unity 2022.3.62f3 / URP 14.0.12 / 구 네이밍 규칙이 남음.
  - `.claude/rules/ui-code.md` 및 Codex 복사본은 `src/ui/**` 경로와 키보드/마우스/게임패드 요구를 사용함. gameplay 규칙도 `src/gameplay/**` 대상. 실제 Unity 경로 및 터치 전용 규칙과 맞지 않음.
  - `.agents/skills`의 Markdown 검색에서 VContainer / LifetimeScope / `server-client-data-architecture` / `UI_Architecture_Specs` 참조를 찾지 못함.
  - 기존 `docs/technical/server-client-data-architecture.md`에 DI/Player/Table/Chart/RM 설명이 있으나 `Manager.GameData` 안내는 현재 코드와 불일치.
  - `Assets/WorkSpace/HSD/Docs/UI_Architecture_Specs.md` 및 실제 UI_Base/UI_ListBase/UI_SlotBase/Presenter 구조가 있음. 문서의 성능 수치는 이번에 측정·검증하지 않음.
- 다음 할 일 제안: 공용 아키텍처 규칙을 현재 DI 구조에 맞게 정리할지 사용자 결정 → 기존 참조 문서 검증 → 스킬은 공용 정본/문서를 참조하도록 연결 → 오래된 복사 규칙과 경로 정리. 규칙을 스킬에 복제하지 않을 것.
- 열린 질문: 현재 DI 구현을 기준으로 공용 규칙을 갱신하는 범위, `Awaitable`을 사용하는 VContainer 진입점과 UniTask 규칙의 관계를 정비 시 확정할 필요가 있음. 이번 조사는 정책을 변경하지 않음.
- 만진 파일: `production/session-state/active.md`만 (STATUS 갱신 + 조사 인계 추가). 코드·씬·프리팹·meta·ProjectSettings 수정 없음. 빌드/런타임 검증 미실행 (정적 조사).
- Git 상태 정정: 조사 시 HEAD는 `c633c9df`이며 HpBar/DI/셰이더/튜토리얼 작업은 해당 최신 커밋에 포함되어 있음. `git status --short`는 JSY.meta/KMS.meta 삭제와 기존 active.md 수정만 표시했음. 위 Claude 세션 당시 미커밋 설명은 현재 상태와 구분할 것. Codex는 커밋/푸시하지 않음.
- 미완 상태 경고: 이번 조사에서 반쯤 편집한 Unity 에셋 없음.

---

## Codex 완료 — 공용 규칙 및 스킬 최신화 (2026-09-11)

### 확정/반영

- 사용자 승인에 따라 공용 아키텍처를 현재 VContainer DI 구현에 맞게 갱신. 규칙 정본은 `AGENTS.md`만 유지.
- 빈 `Manager.cs` 및 `.meta` 삭제. 실행 코드/GUID 참조 없음 확인. JSY/KMS 폴더와 `.meta`는 이미 모두 없는 상태이며 기존 삭제를 유지함.
- HSD는 USW 인수 영역으로 작업 가능. 스킬 복사본과 회의/인계 문서의 옛 작업 제한도 정정. 과거 코드 리뷰는 역사 자료임을 표시.
- 스킬의 구 Unity/URP 설정을 제거하고 `AGENTS.md`의 Unity 6000.3.11f1 기준으로 연결. Android 터치 전용 범위에 맞춰 키보드/게임패드 필수 요구 제거. 범용 템플릿은 프로젝트 입력 범위를 따르도록 명시.
- 기존 데이터/DI 및 HSD UI 리팩토링 문서를 AGENTS.md, 디렉토리 지도, Codex 문서 참조 스킬에서 연결. 데이터 문서의 `Manager.GameData` 안내도 DI로 정정.
- 현재 설치된 VContainer `IAsyncStartable` 인터페이스가 Unity `Awaitable`을 반환함을 패키지 소스에서 확인. 해당 진입점 경계만 유지하고 일반 비동기 로직은 UniTask를 사용하는 것으로 공용 규칙에 명시.

### 만진 파일

- `AGENTS.md`, `docs/AI-COLLAB-PROTOCOL.md`.
- `.claude/docs/{coding-standards,directory-structure,game-system-map,code-review-2026-04-16}.md`, `.claude/docs/script-maps/ingame-system.md`.
- `.claude/rules/{ui-code,gameplay-code}.md`와 `.agents/skills/claude-rules-reference/references/rules/`의 대응 파일 (Codex 쪽은 원본 참조로 전환).
- `.agents/skills/claude-docs-reference/SKILL.md` 및 `references/docs/{coding-standards,technical-preferences,directory-structure,game-system-map,code-review-2026-04-16}.md`, `references/docs/script-maps/ingame-system.md` (과거 리뷰 외 공용 문서 복사본은 참조로 전환).
- `.claude/agents/`의 `accessibility-specialist`, `ue-umg-specialist`, `ui-programmer`, `unity-specialist`, `unity-ui-specialist` 프로필 및 `.agents/skills/agent-*/SKILL.md`, `claude-agents-reference/references/agents/`의 대응 파일.
- `.claude/skills/{smoke-check,team-ui}/SKILL.md` 및 `.agents/skills/{smoke-check,team-ui}/SKILL.md`.
- `.claude/docs/templates/{accessibility-requirements,interaction-pattern-library,ux-spec}.md` 및 `.agents/skills/claude-docs-reference/references/docs/templates/`의 대응 파일.
- `docs/technical/{server-client-data-architecture,totem-system,unit-action-popup}.md`, `docs/engine-reference/unity/current-best-practices.md`.
- `docs/codex_claude회의/`의 `05_charkey_용도와_런타임검증.md`, `07_레거시_참조감사.md`, `13_실행_로드맵.md`, `16_시트뼈대_및_등급스킬설계.md`, `18_개발자용_시트코드_연동가이드.md`, `HANDOFF_세션이어받기.md`.
- `Assets/WorkSpace/USW/Scripts/CoreSystem/Manager.cs`와 `.meta` 삭제; 같은 폴더 `Singleton.cs`의 삭제된 Manager 호출 주석 정정.
- Unity 생성 파일 `Assembly-CSharp.csproj`에서 삭제한 Manager.cs Compile 항목 제거 (로컬 빌드용, Git 비추적).
- `production/session-state/active.md` 갱신.

### 검증/인계

- `dotnet build Assembly-CSharp.csproj --no-restore --verbosity quiet`: 성공, 오류 0 / 경고 25. Unity 런타임·Android 기기 검증은 수행하지 않음.
- 코드/에셋에서 Manager 사용과 삭제한 GUID 참조 없음. JSY/KMS 및 Manager 파일 삭제 완결 확인.
- 연결 문서 링크 27개 정상, 수정된 스킬 헤더 8개 정상, 문서 참조 스킬 `quick_validate.py` 통과. `git diff --check` 통과.
- Git 제외 상태: `AGENTS.md`, `/.agents`, `/docs` 등에 `.gitignore` 규칙이 있음. 이미 추적 중인 문서는 diff에 보일 수 있으나 많은 지침/스킬 변경은 로컬 파일에만 반영되어 diff에 표시되지 않음. Git 추적 정책은 변경하지 않았고 commit/push 하지 않음.
- 열린 질문: 선행 정비 요청 관련 없음. 상세 구조 하네스 문서 범위는 다음 단계에서 진행.
- 미완 경고: 반쯤 편집한 씬/프리팹/meta/ProjectSettings 없음. 기존 JSY/KMS.meta 삭제 및 세션 배턴의 이전 변경은 보존.

### 후속 질의 — 경고 25개 분류

- 사용자에게 경고 내역 설명을 위해 로그를 남겨 재빌드: 오류 0 / 경고 25 재확인. 로그 `Temp/codex-build-warnings.log` (로컬 임시 파일).
- CS4014 17건: `UI_ChiefSkillEffect` 10, `Anim_BounceJump/Breathing/Scale/Slide` 각 1, `TutorialActor_GachaPop` 1, `UI_BossEncounter` 2. 대부분 DOTween 시퀀스 구성/설정 반환값에 대한 경고이며, 전체 시퀀스를 나중에 await하는 코드도 있음. 개별 호출에 무조건 await를 추가하면 병렬 연출 순서가 바뀔 수 있음.
- CS0108 1건: `UI_SettingPanel_Base.cs:37`의 `_audioManager`가 UI_Base의 static 필드를 가림.
- CS0114 1건: `UI_TotemInfoPanel.cs:19` Awake가 UI_Base의 virtual Awake를 가리고 base 호출이 없음. 기본 Canvas/애니메이션 캐싱 및 닫기 버튼 바인딩 누락 가능성을 우선 확인할 것.
- MSB3277 6건: System.Net.Http 충돌 2개 프로젝트 (Assembly-CSharp / firstpass, Backend 의존성), System.Threading.Tasks.Extensions 충돌 4개 Editor 프로젝트 (GGD.Core / Google.Play.Games / IngameDebugConsole / ParticleImage, CodeAnalysis 의존성). Unity 제공 참조 DLL과 의존 라이브러리 요구 버전 차이.
- 게임 코드 수정 없음. 이번에는 경고 분류/설명만 수행. 상세 구조 하네스 작업은 여전히 다음 단계.

---

## Codex 완료 — C# 경고 19건 수정 (2026-09-11)

- UI_TotemInfoPanel: Awake override 및 base 초기화 복구. SetData에서 Canvas를 다시 활성화하여 닫은 뒤 재표시 보장.
- UI_SettingPanel_Base: 주입 필드를 _settingsAudioManager로 변경하여 상속된 static 필드 숨김 해소.
- DOTween 구성/설정 반환값은 명시적 discard로 처리. 개별 await를 추가하지 않아 기존 시퀀스 타이밍 유지.
- Anim 4종: 반복 시퀀스 대기에 destroy CancellationToken 전달. 가챠 이동은 동시 재생 유지 및 SetLink로 파괴 시 정리.
- 수정 파일: Assets/WorkSpace/HSD/Scripts/UI/Totem/UI_TotemInfoPanel.cs, UI/Setting/UI_SettingPanel_Base.cs, UI/Effect/UI_ChiefSkillEffect.cs; HSD/Scripts/Tutorial/Actor/TutorialActor_GachaPop.cs; HSD/Scripts/Animation/Anim_BounceJump.cs, Anim_Breathing.cs, Anim_Scale.cs, Anim_Slide.cs; Assets/WorkSpace/USW/Scripts/IngameUI/UI_BossEncounter.cs; 이 배턴 파일.
- 검증: 전체 재컴파일 및 최종 증분 dotnet build 성공, 오류 0 / 경고 6. 요청한 CS0114/CS0108/CS4014 19건 제거. 남은 경고는 기존 외부 DLL 버전 충돌 MSB3277. 로그: Temp/codex-warning-fix-build.log.
- 다음 작업: Unity에서 토템 정보 패널 닫기/재표시 및 가챠/족장/보스/반복 애니메이션 육안 확인. 기존 구조 하네스 정리는 별도 후속 작업.
- 열린 질문 없음. Unity 실행 검증은 미수행. 씬/프리팹/meta/ProjectSettings 직접 편집 없음. 기존 작업 변경 보존, commit/push 없음.
---

## Codex 완료 — URP Compatibility Mode 경고 해결 (2026-09-11)

- 사용자 요청: 두 번째 URP 경고 및 define에 따른 빌드 시간/크기 추가 부담 제거. 첫 번째 Pipeline 자동화 모드 경고는 범위 밖.
- 조사: Assets/Packages에 커스텀 ScriptableRenderPass/ScriptableRendererFeature 구현 없음. Performant renderer 기능 목록은 비어 있고 Balanced/HighFidelity는 URP 기본 SSAO만 사용. 설치 URP의 ScreenSpaceAmbientOcclusionPass.RecordRenderGraph 구현 확인.
- 변경 파일: Assets/UniversalRenderPipelineGlobalSettings.asset의 RenderGraphSettings.m_EnableRenderCompatibilityMode를 0으로 변경. ProjectSettings/ProjectSettings.asset의 Android 정의에서 URP_COMPATIBILITY_MODE만 제거. 이 배턴 갱신.
- legacy m_EnableRenderGraph 필드는 현행 설정이 아닌 마이그레이션 데이터이므로 유지 (global settings asset version 10).
- 검증: 실행 중 Unity Pipeline recompile 완료, failed=false/errors=[]; 재컴파일 이후 캡처 로그에 신규 호환 경고 없음. dotnet build 성공, 오류 0 / 기존 MSB3277 경고 6. git diff --check 통과. 로그 Temp/codex-rendergraph-build.log.
- 다음 확인: Unity 실제 씬의 SSAO/후처리 화면 및 Android 실행 확인. Android APK/AAB 빌드와 크기/시간 비교는 미수행; 감소량을 측정한 것은 아님.
- 열린 질문 없음. 미완 상태 씬/프리팹/설정 없음. 커밋/푸시 없음. 시작 시 git clean 상태였음.

---

## Claude 진행 중 — 신규 토템 6종 설계 (SPR_TD1001~1006) (2026-09-11)

> 아직 구현 시작 전. 설계/스펙 확정 단계.

- 사용자가 신규 토템 6종 컨셉 제시 (보조투사체 / 보스킬 스택 반경확장 / 화염구+화상DOT /
  아군 소환·합성 / 임시 등급업 / 아머브레이크). 애매한 지점을 전부 질의응답으로 확정함.
- **기존 TotemData 33종은 사용자가 직접 정리(삭제)할 예정 — Claude는 손대지 않음.**
- 문서 갱신: `AGENTS.md` Refactoring References에 `docs/technical/totem-system.md`,
  `docs/technical/damage-formula-reference.md` 링크 추가.
- **확정된 설계 결정**
  - `MergeManager`는 실제로 **2마리 합성**(3마리 아님) — `AGENTS.md`/`.claude/docs/directory-structure.md`의
    "3합성" 서술은 **stale, 아직 미수정**. 코드가 정답(`GetMergeTargets`가 `count>=2`에서 멈춤).
  - 보스 방어력(신규 스탯) 데미지 계산: **방어계수 = `1/(1+ln(1+방어력/500))`로 확정**
    (근거·원문 스펙·TODO는 `docs/technical/damage-formula-reference.md` 참조). 롤식/단순감산/직접퍼센트는
    기각 — 엔드리스 웨이브에서 방어력이 억~조 단위로 갈 수 있다는 사용자 요구 때문.
  - `UnitBase.onAttack`(UnityEvent, `ExecuteAttack()`에서만 호출, 스킬과 분리)이 이미 있어서
    TD1001/TD1006 모두 "일반 공격 시 트리거" 훅으로 그대로 재사용 가능 — 신규 훅 불필요.
  - TD1004(조커 합성 유닛) 인프라 훅(`UnitBase.IsWildcardMergeUnit` + `MergeManager` 우회 조건)을
    한 번 구현했다가 **사용자 요청으로 롤백함 — 코덱스와 같이 작업 예정, Claude는 아직 손대지 않음.**
- **아직 안 한 것 (다음 세션 이어받을 것)**
  - [ ] 6개 토템 quick-spec 문서(`design/quick-specs/`) 작성 — 아직 파일로 안 씀, 대화로만 확정됨.
  - [ ] `BossEntry`(WaveData.cs)에 `defense` 필드 신설, `BossBase`에 `_armorBreakStack` 추가 — 미착수.
  - [ ] TD1004 조커 합성 인프라 재구현 (롤백된 상태) — 코덱스와 division 논의 후 진행.
  - [ ] HP/데미지 `int`→`long` 또는 BigNumber 전환 여부 — 큰 아키텍처 결정, 별도 논의 필요.
- 실험용 산출물: 방어 공식 A/B/C 비교용 인터랙티브 계산기 아티팩트 제작
  (https://claude.ai/code/artifact/cf693adb-469f-4dd9-ab34-ccd6a0f9c99b) — 코드에는 반영 안 됨, 의사결정 보조용.
- 미완 상태 경고: 씬/프리팹/meta/ProjectSettings 변경 없음. 코드 변경 없음(롤백 완료 상태로 clean).
  커밋/푸시 없음.
## Codex — TD1006 밸런스 검토 (2026-09-11)

- 사용자 확정: TD1006 의도는 완전 파괴가 아닌 점진적 약화. 현재 10웨이브 및 향후 엔드리스 확장을 함께 고려.
- 확인: DebuffDroneUnit.cs와 DroneUnit_Debuff.prefab은 받는 피해 1.2배/5초. DroneManager.ApplyBossDebuff는 단일 슬롯 덮어쓰기/만료 갱신이며 곱중첩하지 않음. BossBase.TakeDamage는 현재 방어 계산 없음. WaveData에도 defense 없음. 화상은 제공된 설계 문서 기준으로 분석.
- 제안(미확정): 방어계수 = 1/(1+ln(1+D/K)/(1+a*s/50)), 강도 a=0.3, K=500을 초기 기준으로 사용. K는 단독으로 밸런스를 결정하지 않으며 보스별 D/K와 함께 설계해야 함.
- 수식 계산: D=200/500/50000, K=500에서 최대 스택 단독 피해 증가 6.17/10.43/23.41%, 드론 동시 적용 시 무디버프 동일 방어력 대비 27.40/32.52/48.09%. 엔드리스 극한에서 아머 단독 30%, 드론 조합 56%에 접근. 실전 평균은 스택 속도/디버프 가동률에 따라 작음.
- 화상 초기 제안(미확정): 갱신형 1중첩으로 시작. 방어/증폭 적용 여부, 보스 목표 처치 시간, 화염구 공격 간격을 사용자에게 질문했으며 답변 대기. 두 효과를 화상에 적용하는 경우 방어력 500에서 화상 5틱 단독의 체력 감소는 시작 체력 대비 약 3.85%(아머 풀스택+드론 5초 유지, 다른 피해와 정수 반올림 제외).
- 주의: 방어 추가 자체가 기존 HP 기준 처치 시간을 늘림. K=500/D=500이면 방어만으로 약 1.69배. 강도/K만으로 실전 균형 검증 완료라고 주장할 수 없음.
- 다음: 질문 답변에 따라 화상 조합과 처치 시간 검증, 기본값 확정. 코드/스펙 변경 없음, 이 배턴만 추가. 씬/프리팹/meta/설정 수정 없음. commit/push 없음.

## Codex 완료 — 네 가지 전투 규칙 기술문서 갱신 (2026-09-11)

- 사용자가 강도 0.3/K=500 초기 기본값과 확장 로그식 채택 및 문서화를 승인했다. 이전 미확정 수치/화상 분석 기록은 이 항목으로 대체한다.
- 화상 확정: 즉발 피해 직전 HP의 총 1% 스냅샷, 지속시간 동안 균등 분할, 틱 최소 1, 소수 보존, 방어·아머·받피증 독립. 기본 5초 지속/1초 틱/5초 발사, 1중첩 갱신. 3초/6초는 튜닝 예시만 해당.
- 아머: 영향 셀 1칸, 일반 공격 시도 +1, 최대 50, 보스 귀속. 기본 방어력 직접 차감 대신 로그 방어 효과를 약화. 도입 목적은 밸런스 조정 수단 및 받피증과의 역할 분리.
- 변경 파일: docs/technical/totem-batch-td1001-1006.md, damage-formula-reference.md, totem-system.md, 이 배턴. 외부 공식 원문과 다른 토템 스펙은 보존하고 관련 모순을 정정.
- 다음 작업: 디버프 시스템 설계. 틱/갱신 경계, 다중 출처, 소수 저장/API, 생명주기 등을 결정한다. 네 가지 기본 계산 규칙은 재질문할 필요 없음.
- 검증: 문서 내용·참조 및 수식 예시 확인. 코드/시트/씬/프리팹/meta/설정 수정 없음. 미완 편집 없음. commit/push 없음.

## Codex — 공통 디버프 1차 설계 제안 (2026-09-11)

- 요청: 화상·아머·받피증을 공통 카테고리로 관리할 클래스 및 시트 키 설계. 공용 협업 하네스를 사용해 단계별 진행.
- 작성: docs/technical/debuff-system-design.md. 현재 코드의 BossBase→DroneManager 받피증 의존, int HP/이벤트, 숫자 totem_id/헤더 기반 시트 로더를 확인하고 설계 초안 작성.
- 제안(미확정): 보스 소유 DebuffController + 효과별 Instance + DebuffData/Catalog. debuff_id/key, kind, stack_group, 런타임 SourceId 분리. 초기 ID 1001~1003은 잠정 번호. 시트 컬럼과 초기값, 검증 기준 포함.
- 다음: 사용자·Claude가 클래스 책임/소유권/키 구조 검토 → 화상 갱신·복수 출처·수명·소수 HP 계약 확정 → 단계별 구현. 기존 확정 공식/기본값은 재질문하지 않는다.
- 열린 질문: 받피증 보스 교체 시 초기화, 화상 스냅샷 교체/틱 시각 유지, T/I 정수 제약, 여러 토템의 동일 공격 감지 정책, 소수 저장/API. 문서 추천은 사용자 승인으로 취급하지 않는다.
- 만진 파일: 위 신규 설계 초안과 이 배턴. 원본 계산 규격, 코드/시트/씬/프리팹/meta/설정 수정 없음. 실행 테스트 미실시(설계 단계). 반쯤 편집한 Unity 자산 없음. 기존 다수 자산 삭제/설정 변경 보존. commit/push 없음.

## Codex — Claude 검토 전달 반영 및 소수 HP 의견 (2026-09-11)

- 사용자 전달 승인: DebuffData/Catalog/Controller/Instance 및 효과별 클래스, 순수 DamageCalculator 구조. 보스 소유 컨트롤러로 이전. DebuffDroneUnit 배율/지속시간 전용 필드 완전 제거 후 시트 debuff_id 기반 범용 부여. Debuff 시트 정의를 유닛/토템 FK로 참조하고 화염구에도 같은 경로 사용.
- 코드 재확인: BossManager.CurrentBoss/CurrentBosses 이미 존재, 새 타겟 배관 불필요. 현재 소환은 단일 보스 흐름이다. HP/피해 이벤트, 보스 페이즈 및 UI/HSD 피해 구독의 int 경계를 변경할 필요가 있다.
- FK 보류: 사용자가 저장 구조 고민 중. Claude의 IDebuffSource + ApplyDebuffFunction + 드론 등급별 debuffId는 후보만 기록, 승인/구현하지 않음.
- 소수 HP 승인: float/double 내부 HP 및 피해 적용 n자리 반올림/사망 엡실론 방향. Codex 추가 추천(미확정): double, 4자리, AwayFromZero, 엡실론 1e-8. 0.01은 유효한 잔여 HP까지 제거한다. 틱 반올림 총량 잔여 및 극소 일반 피해 누적 정책은 후속 결정.
- 수치 확인: HP 1억 - 피해 2.5에서 single 차감 0 / double 차감 2.5. 100/3의 3틱 합은 2자리 99.99, 4자리 99.9999. 빌드/게임 실행 테스트는 수행하지 않음.
- 변경 파일: docs/technical/debuff-system-design.md, 이 배턴. 다음: FK 저장 구조 사용자 확정 → 수치 의견 검토 → 최종 설계 반영 후 구현. 이전 미승인 상태 표기는 이 기록으로 대체. 기타 효과 갱신/복수 출처 정책은 해당 구현 전에 확정.
- 코드/시트/씬/프리팹/meta/설정 수정 없음. 반편집 Unity 자산 없음. commit/push 없음.

## Codex — FK 저장 추천 및 HP 정밀도 재검토 (2026-09-11)

- 사용자 요청: FK 저장 구조 추천. 사용자 전달 정밀도 검토에 따라 HP 상한과 소수 해상도의 연관성을 명시. double 자체 동의, 4자리/마지막 틱 잔여 보정/중간값 규칙 동의. 최종 저장 타입과 HP 상한은 미정.
- 신규 조사: ITotemFunction.Apply는 셀 재계산 훅이어서 직접 디버프 부여/구독에 부적합. UnitData는 PerTier 데이터, SkillData.hitEffects는 적중 효과. 드론 OnSkillFull 및 아머 일반 공격 시도는 적중 훅으로 일괄 처리 불가. CharacterSheetRow.SkillId는 필드만 있고 현재 파서가 채우지 않음.
- FK 추천(미승인): 공통 DebuffBinding(DebuffId/Trigger/StacksPerApply), UnitData는 PerTierDebuffBinding, TotemData는 단일 Binding. 두 시트에 debuff_id/debuff_trigger/debuff_stacks_per_apply. 기존 발동 훅에서 공통 Controller 부여. IDebuffSource는 초기 필수로 두지 않음.
- 수치 확인: double ULP는 HP 1억에서 약 1.49e-8, 1조에서 0.0001220703125, 10조에서 0.001953125. 1조도 정확한 0.0001 보장 불가. 엡실론 증가는 소실된 피해를 복구하지 못함.
- 정정: HP 상한과 정밀도는 타입 선택에서 연결됨. 4자리 고정소수 long의 최대 저장 HP는 922337203685477.5807, 중간 곱셈/변환은 별도 오버플로우 검증 필요. 로그 계산 근사는 남으며 정수 HP는 엡실론 불필요. fixed-point long 우선 검토 제안, 사용자 확정 전.
- 변경: docs/technical/debuff-system-design.md 및 이 배턴. 다음: FK안 검토, HP 범위/저장 계약 결정 후 구현. 코드/시트/Unity 자산 수정 없음, 빌드 미실시, 반편집 자산 없음, commit/push 없음.

## Codex 참고 GIF 확인 (2026-09-15)

- 사용자 요청: 선택지 UI GIF를 읽을 수 있는지 확인. 실제 파일은 docs/technical/선택지UI창.gif(548×936, 68프레임, 6.8초). ffmpeg로 0.5초 간격 프레임 모음을 Temp/choice-ui-gif/contact.png에 추출해 시각 확인.
- 확인: 어두워진 게임 배경 위 노란 카드 선택 배너, 세로로 배치된 가로형 카드 3개, 밝은 테두리/등장 강조, 하단 타이머. 중간 확대된 화면 구간은 편집/확대 가능성이 있어 게임 자체 연출로 단정하지 않음.
- 구현 변경/테스트 실행 없음. 원본 GIF/씬/프리팹 변경 없음, 반편집 자산 없음. 요청 시 이 자료를 기준으로 선택지 UI 연출 분석/구현 가능. 기존 완료/후속 작업 상태 유지.
