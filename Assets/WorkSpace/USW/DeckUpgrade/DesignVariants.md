# 별도 디자인 시안

현재 `UnitUpgradeUI.unity`와 `Prefabs/UnitUpgradeUI.prefab`은 변경하지 않습니다.

| 씬 | 방향 |
|---|---|
| `Concepts/UnitUpgrade_01_Clean.unity` | 원래 간격과 크기 유지, 장식을 뺀 기본 정돈형 |
| `Legacy/DesignTrials/UnitUpgrade_02_BoldFrame.unity` | 이전 비교안, 현재 씬에서 로드하지 않음 |
| `Legacy/DesignTrials/UnitUpgrade_03_Flat.unity` | 이전 비교안, 현재 씬에서 로드하지 않음 |

- 3열, 덱별 연속 배치, 족장 우선, 보유/미보유 구분과 강화 흐름을 유지합니다.
- 프레임의 드론·금속 모서리·리벳 장식은 숨깁니다. 족장은 골드 프레임과 복원한 금왕관으로 구분하며, 왕관은 3.8초 주기로 천천히 부유합니다. 작은 금색 알갱이도 천천히 상승합니다.
- 아래 이름은 배경 판 없이 텍스트만 표시합니다.
- 제목, 이름, 레벨, 수치, 설명 등 모든 글자를 볼드로 표시합니다.
- 기본 정돈안 하나만 유지합니다. 색감은 현재 원본과 동일하며 색상별 테마 변경은 적용하지 않습니다.
- 맨 위 제목·골드 표시줄만 숨기며 기존 상단 공간은 향후 UI를 위해 비워 둡니다. 대표 이미지와 목록의 시작 위치도 원래대로 유지합니다.
- 별도 상단 박스 없이 화면 바탕 자체를 대표 이미지 영역의 배경색으로 통일합니다. 대표 이미지 패널의 둥근 상단 경계도 제거해 끊김 없이 이어지게 합니다.
- 상단에는 `LobbyScene.unity`의 `MainUI_TopPanel`을 복사한 `Concepts/LobbyTopPreview.prefab`을 임시 배치했습니다. 유저 정보·스태미나·골드·다이아 디자인을 재사용하며 Player, 30/30, 12,000, 1,000은 예시 값입니다. 계정 연동 스크립트와 버튼 동작은 복사본에서 제외했습니다.
- 강화창 상단의 현재→다음 수치 행은 제거했습니다. 스탯/스킬/레벨 보너스 3개 페이지를 0.22초 좌우 슬라이드로 전환하며, 강화 성공 시 스탯 탭으로 이동해 현재 값만 표시합니다. 내부 게이지와 강화 버튼의 골드 아이콘은 삭제했습니다.
- `DeckUpgradeDesignVariant`가 각 시안 씬에서 기본 화면 생성 이후 디자인을 적용합니다. 기존 프리팹에는 이 컴포넌트를 추가하지 않았습니다.
- 씬을 열고 Play하면 확인할 수 있습니다. 기존 시안 로컬 진행 상태를 읽으며 실계정 데이터는 사용하지 않습니다.

생성: `Tools/verification/DeckSelectorDesignAuthoring.cs`의 `Build`. 캡처·검증: `DeckSelectorDesignChecks.cs`의 `Capture`.
비교 화면: `outputs/unit-upgrade-designs/`.
