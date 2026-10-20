# UI 프레임워크

현재 기본 클래스와 연결을 설명한다. 배포 준비 완료·CPU 90% 개선·GC 0 같은 측정 근거 없는 성능 판정은 이 문서의 계약이 아니다.

| 원본 | 실제 역할 |
|---|---|
| [UI_Base](../Scripts/UI/UI_Base.cs) | Open/Close 및 UniTask OpenAsync/CloseAsync, Canvas/활성 상태, 공통 닫기 버튼·연출 |
| [UI_ListBase](../Scripts/UI/Base/UI_ListBase.cs) | Render(IEnumerable, onBind), 기존 슬롯 재사용, 부족한 슬롯 RM 생성, 남은 슬롯 비활성화 |
| [UI_SlotBase](../Scripts/UI/Base/UI_SlotBase.cs) | SetData → OnBind, Data 보관과 선택 표시 확장 |

UI_ListBase는 MonoBehaviour가 아닌 직렬화 가능한 일반 클래스다. Render는 슬롯 SetData 뒤 Presenter의 onBind를 호출한다. GetActiveSlots는 FindAll로 새 리스트를 만들기 때문에 프레임워크 전체를 무할당이라고 부르지 않는다.

View는 Unity 참조와 화면 갱신/입력을 제공하고 Presenter가 데이터 조회·이벤트·명령을 연결한다. RootLifetimeScope가 UI_Base에 AudioManager를 명시 주입한다. 씬 Presenter/컴포넌트는 해당 LifetimeScope 및 Installer의 실제 연결을 확인한다. Canvas를 숨겨도 소유 객체의 이벤트/비동기 작업이 자동 정리되는 것은 아니다.

족장 선택은 PlayerChiefManager의 저장 데이터와 ChieftainSelection의 인게임 전달, IChiefActiveSkill HUD를 구분한다. [족장 현재 구조](../../../../docs/technical/alphan-active-skill.md), [데이터 초기화](../../../../docs/technical/server-client-data-architecture.md)를 참조한다.

레벨업·토템 보상·필드 보기·결과창은 [선택/정지](../../../../docs/technical/levelup-system.md)와 [진행 GDD](../../../../design/gdd/ingame-system.md)에 설명한다. 공유 코딩 규칙은 [AGENTS.md](../../../../AGENTS.md)에만 유지한다.

2026-10-11 후속 적용: 유닛·배치 토템 정보창은 우측 상단 공용 `DescriptionToggleView`로 간단히/자세히를 전환한다. 매 열기 간단히로 초기화하며 선택지 저장 설정과 독립이다. 유닛은 SO 설명과 현재 전투 수치, 토템은 같은 SO의 두 문구와 현재 성장/수확·범위를 표시한다. `InfoPanelPresentation`이 활성화 후 화면 캔버스와 정렬30을 적용해 정렬20의 피해 숫자가 창 뒤에 그려지게 한다. 씬/프리팹 추가 배선은 필요 없다. [실제 플레이 검증](../../../../outputs/info-toggle-20261011/RESULT.md).

2026-10-13 적용: `USW/Prefab/UI/IngameOptionsMenu.prefab`을 IngameScene·TutorialScene의 기존 MainUI 안전영역 아래에 배치한다. ≡ 메뉴, 중첩 설정 팝업, 딜표 컴포넌트, 중첩 선택지 보관함을 함께 재사용한다. 씬별 참조는 인게임 컨트롤러의 설정창, 보관함의 유닛 정보창, 튜토리얼의 옵션 노출 대상이다. 튜토리얼 옵션 버튼은 레벨업 선택지부터 표시하며 안내 중 입력 제한은 기존 튜토리얼 흐름을 따른다.

`SelectionInventoryUI`는 씬 스코프에서 선택지·시간·설명 설정·족장 의존성을 주입받는다. `LevelUpManager.ChosenIds` 순서와 기존 확정 설명을 사용하며 LevelUpManager를 변경하지 않는다. 열기/닫기/비활성화 때 자기 소유의 TimeScaleService 요청만 관리한다. 보관함 정렬25 → 유닛 정보창30 → 용어 시트40, 설정층4를 사용한다. 카탈로그 유닛 정보창은 판매 버튼을 숨기며 기존 전투 유닛 참조를 지운다. 9121·9124는 SO Feedback의 델탕, 9122·9123은 공용 표시다.

처음 설치용 `IngameOptionsPrefabBuilder`는 기존 프리팹 덮어쓰기를 거부한다. 완성 후 수정은 프리팹에서 한다. 씬 참조 교체는 노출된 직렬화 참조만 대상으로 하며 Unity 소유권 필드(`m_GameObject` 등)는 변경하지 않는다. 중첩 Canvas는 부모 Canvas가 있는 상태에서 배치·정렬을 기록해야 한다. `Build Selection Inventory Prefabs` 메뉴는 선택지 창/칸을 덮어쓰므로 수동 수정 뒤 재실행하지 않는다. [통합 검증과 크래시 원인/수정](../../../../outputs/ingame-options-20261013/RESULT.md).

2026-10-20 글씨 기준: 1080 기준 본문·설명32, 핵심 수치40, 자동 크기 최소28을 사용한다(`UiTypography`). 기존 큰 글씨는 줄이지 않는다. 정보창/강화/토템/설명/연구·해킹 HUD의 런타임 지정도 같은 기준을 따른다. 강화 카드는 가격·레벨 자동 크기 하한을 유지하고 아이콘/설명 영역을 분리한다. 로비 이름·레벨·추가 표시의 칸은 커진 글씨가 잘리지 않게 확대했다. CanvasScaler Match=0은 유지한다. [변경 및 세 화면비 검증](../../../../outputs/touch-typography-20261020/RESULT.md).
