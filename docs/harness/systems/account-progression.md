---
system: account-progression
title: 계정 경험치·계정 레벨업
keywords: 계정 경험치, 계정 레벨, 계정레벨, account.xlsx, accountprogression, accountexperience, playerexp, maxexp, 전투 경험치, 지급 영수증
verified: 2026-10-30
commit: de2cbd00
---
# 계정 경험치 카드

**한 줄 요약**: 보스별 초기 최대 HP 대비 최고 감소 비율을 합산해 승패 결과에서 계정 경험치를 지급한다. 전투 ExpManager와 별도다. 수치/종료 정책은 시험값이다. 10-28에 HEAD 151a2427의 시작/결과·직렬화 경계를 대조했으며, 아래 과거 검증을 새로 실행한 것은 아니다.

## 2026-10-30 정적 대조
BossManager의 디버프 시작 팝업 구독과 인게임 스코프의 StatusPopupService 등록이 추가됐다. 계정 EXP 추적·지급 순서는 유지된다.

기준은 `de2cbd00`. 아래 날짜별 미커밋 표현과 검증 횟수는 당시 기록이며 현재 미커밋 상태나 이번 재실행을 뜻하지 않는다. 이번에는 문서 대조만 했고 Unity/Android 검증은 실행하지 않았다. [대조 및 미등록 분류](../refresh-20261030.md).

## 흐름
1. Editor 메뉴 Tools/Balance/Import AccountProgression.xlsx → AccountWorkbookImporter가 지원 수식을 현재 입력으로 계산·검증 → 안정 GUID의 설정 SO만 갱신.
2. Root는 설정과 기존 PlayerDataController를 등록하고 로드 전 현재 곡선을 전달. 씬은 AccountExperienceRun을 공유.
3. GameManager 시작에서 본게임 IngameScene/HardIngameScene만 계정/세대/전투 ID/단조 번호 예약 → BossManager가 Init 직후 Track → HP/사망 이벤트로 최고 진행 기억. `GameManager.IsEditorPreview`인 직접 Play는 지급 티켓과 결과 기록을 만들지 않는다.
4. GameManager 종료에서 Finish → 레벨/잔여/MaxExp/영수증을 같은 저널 스냅샷으로 확정 → 저장 요청 → StopRun/정리.
5. 결과 EXP 슬롯은 상한 적용 후 실제 적립량. 로비 상단·프로필은 기존 계정 변경 이벤트와 Data를 읽음.

## 진입 파일
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/USW/Scripts/Editor/AccountWorkbookImporter.cs` | Editor XLSX 검증·수식 템플릿 재계산·원자적 SO 반영 |
| `Assets/WorkSpace/USW/Scripts/OutGame/AccountProgressionConfig.cs` | 레벨 곡선·시험 정책·복수 레벨업/이월/상한 |
| `Assets/WorkSpace/USW/Scripts/OutGame/AccountExperienceRun.cs` | 씬 수명 보스별 최고 HP 감소율·종료 동결 |
| `Assets/WorkSpace/USW/Scripts/OutGame/AccountExperienceTicket.cs` | 메모리 계정/데이터 세대/전투 예약 토큰 |
| `Assets/WorkSpace/USW/Scripts/OutGame/AccountExperienceReceipt.cs` | 마지막 지급 ID/단조 번호/실제 획득량/레벨 변화 |
| `Assets/WorkSpace/USW/Scripts/OutGame/PlayerDataController.cs` | 예약·한 번 지급·계정 저널/SaveId/저장 재시도 |
| `Assets/WorkSpace/USW/Scripts/OutGame/PlayerData.cs` | 기존 계정 데이터와 영수증/예약 필드 |
| `Assets/WorkSpace/USW/Scripts/OutGame/BackendGameData.cs` | 기존 PlayerData 행 직렬화·구형 컬럼 호환 |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/Managers/GameManager.cs` | 시작 캡처·StopRun 전 지급·결과 전달 |
| `Assets/WorkSpace/USW/Scripts/Boss,Enemy/BossManager.cs` | 초기 HP 확정 직후 기존 이벤트 연결 |
| `Assets/WorkSpace/HSD/Scripts/CoreSystem/RootLifetimeScope.cs` | 설정·PlayerData 등록/로드 전 곡선 전달 |
| `Assets/WorkSpace/USW/Scripts/CoreSystem/InGameLifetimeScope.cs` | 씬 단위 진행 서비스 등록 |
| `Assets/WorkSpace/USW/Scripts/IngameUI/Result/ResultScreenData.cs` | 실제 적립 경험치 전달 |
| `Assets/WorkSpace/USW/Scripts/IngameUI/Result/ResultScreenView.cs` | 기존 EXP 아이콘에 실제 획득량 표시 |
| `Assets/WorkSpace/USW/Scripts/OutGame/TopPanelSystem.cs` | 같은 계정 데이터·최대 레벨 표시 |
| `Assets/WorkSpace/USW/Scripts/UI/LobbyUI/Profile/LobbyProfilePopup.cs` | 새 프로필의 같은 경험치와 MAX 표시 |

## 데이터
| 파일 | 역할 |
|---|---|
| `Data/Balance/AccountProgression.xlsx` | 조정 가능한 시험값 편집 원본; 런타임 읽기 없음 |
| `Assets/WorkSpace/USW/Resources/AccountProgressionConfig.asset` | 빌드 설정: 최대50·최초500·단계+100·보스100%당100 |
| `Assets/WorkSpace/USW/Prefab/UI/Result/UI_ResultScreen.prefab` | 기존 보상 템플릿/EXP 아이콘/레이블; L31 글씨32/수치40 반영 |

## 불변 규칙·함정
- 보스 최대 HP 크기가 경험치를 키우지 않는다. 초기 최대 HP 대비 최고 감소 비율이며 회복 반복/오버킬/중복 콜백은 추가 몫 없음. 전투 합계에서 한 번 내림.
- 승패 지급, 자발적 종료/튜토리얼/실험실/Editor 직접 Play 미리보기/오류 종료 미지급. NORMAL/HARD 본게임 공통 시험 정책. 레벨업 재화/스태미나 보상 없음.
- 수식 캐시 단독 사용 금지. 지원하지 않는 수식/잘못된 셀/정책을 0으로 치환하거나 무시하지 않는다. 런타임 Excel/시트 SO 덮어쓰기 없음.
- 계정 경험치와 전투 ExpManager는 독립이다. 결과 지급은 반드시 StopRun/보스 정리 전에 확정.
- 기존 SaveId와 계정별 저널에 경험치·레벨·영수증을 함께 넣는다. 재시도는 절대 스냅샷 저장만 한다. 미결 저널 충돌은 기존 충돌 정책을 따른다.
- 영수증 없는 구형 행은 읽을 수 있어야 한다. 설치 LitJson은 최상위 null을 파싱하지 못하므로 빈 문자열을 영수증 없음으로 사용.
- 2026-10-18 빌드 전 수정: Unity JsonUtility 스냅샷이 null 영수증을 모든 필드가 기본값인 객체로 만드는 것을 재현했다. 로더는 정확한 기본값 객체만 미지급 상태로 읽고, 서버 저장은 빈 문자열로 정규화한다. 실제 지급 영수증·부분 손상 기록 검증과 중복 지급 차단은 유지한다. 상단 EXP 참조도 로비 씬의 배경 Image에서 기존 자식 Filled Image로 수정했다.
- 여러 기기의 동시 쓰기 서버 원자성·실제 서버 전투 검증·Android 성능을 보장한 작업이 아니다.

## 검증
| 파일 | 역할 |
|---|---|
| `Tools/verification/AccountWorkbookImport.cs` | Pipeline의 명시적 Import 진입·검증용 압축 어셈블리 로딩 |
| `Tools/verification/WorkbookRelocationChecks.cs` | 이동한 원본을 실제 Reader로 검증; SO·씬·플레이어 쓰기 없음 |
| `Tools/verification/AccountProgressionChecks.cs` | 수식/캐시/오류 입력·진행/상한·저장 실패/재접속/응답 유실/동시 변경·구형 행·복귀 정규화 46 PASS |
| `Tools/verification/AccountProgressionIntegrationChecks.cs` | 실제 보스 생성/피해/사망→GameManager 종료→결과/상단/프로필·Play Import 차단 19 PASS, 실제 계정 값 불변 |
| `Tools/verification/OutgamePrebuildChecks.cs` | 빈 영수증 Unity clone/서버 payload/재로딩·유효/손상 기록·저널·상점 격리 회귀 |
| `Tools/verification/OutgamePrebuildInspect.cs` | 실제 계정 읽기 전용 파싱 진단·상단/프로필 표시·잔액 보존 확인 |
| `outputs/account-progression-20261018/RESULT.md` | 실제 환경·실패와 수정·실행 근거·미검증 경계 |

## 더 읽을 곳
| 파일 | 역할 |
|---|---|
| `docs/technical/account-progression.md` | 현재 구현·시험 정책·Importer 사용·저장 계약 원본 |
| `docs/technical/player-data-architecture.md` | 공용 PlayerData 변경/저장 계약 |
| `outputs/account-workbook-20261018/RESULT.md` | 변경하지 않은 Excel 최초 작성 근거 |

2026-10-18 경로 정리: [데이터 원본 안내](../../../Data/README.md), [이동·참조 변경 근거](../../../outputs/data-organization-20261018/RESULT.md). 위 commit은 기준 커밋이며 이번 이관은 미커밋이다.
