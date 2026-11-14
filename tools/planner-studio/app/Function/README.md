# 체력 연동 경제 Function 계산 모듈

기획 기준: `design/gdd/hp-linked-outgame-economy.md` (2026-11-14).
`OutgameEconomy.cs`는 뒤끝펑션 프로젝트에서 호출하는 C# 계산 모듈이다. SDK 진입점·서버 지급·구매 트랜잭션을 포함하지 않는다.

## 기획툴

강화 밸런스 → 체력 연동 경제에서 계수를 편집하고 **일반 유닛·BASIC 비용표 생성**을 누른다. 기존 저장본을 열 때 비용은 유지한다. 새 프로젝트는 문서 기준 비용으로 시작한다. 문서 기준값 불러오기는 계수만 복원한다.

**백엔드 Excel 저장**은 아래 파일을 한 폴더로 생성한다. 공식과 비용표가 다르면 생성을 막는다. 모든 Excel은 단일 시트이며 수식 셀이 없다.

- `OutgameEconomy.backend.xlsx`: `HpLinkedV1` 공식 버전과 편집한 계수 1행. 런타임 기준 HP는 저장하지 않는다.
- `UnitBaseStats.backend.xlsx`: 기존 스탯·목표 레벨별 UpgradeCost. 일반 유닛에만 공식을 적용하며 족장은 보존한다.
- `ResearchBalance.backend.xlsx`: Node/Parent/Lane. BASIC의 Gold/BaseCost/CostStep을 포함하고 다른 트리는 보존한다.
- `Function/OutgameEconomy.cs`: 계수를 하드코딩하지 않은 서버 계산 모듈.
- `source-project.json`: 편집 원본 복원용.

## 서버 연결

1. `DEV_OutgameEconomy` / `LIVE_OutgameEconomy`를 같은 열 구성으로 생성한다. 기존 `DEV_/LIVE_UnitBaseStats`, `DEV_/LIVE_ResearchBalance`에도 각각 해당 파일을 적용한다. 기존 OutgameBalance와는 별도 차트다.
2. 배포 대상 환경은 서버 설정으로 고정한다. 그 환경의 **서버가 읽은** OutgameEconomy 데이터 1행을 `IDictionary<string,string>`으로 변환해 `new BackendFunction.OutgameEconomy(row)`에 전달한다. 열의 누락·미등록·범위 오류는 전체 거절한다. 뒤끝 SDK 메타데이터 열은 이 사전에 넣지 않는다. 클라이언트 요청의 계수/비용을 사용하지 않는다.
3. 뒤끝 공식 CLI가 생성하는 Function 프로젝트에 이 소스를 포함한다. `Function(Stream, ILambdaContext)` 진입점에서 SDK 초기화 후, 서버에서 확정한 데이터로 아래 메서드를 호출한다.
4. `BattleReward(referenceHp,bosses,goldBonus,tokenBonus)`는 최종 정수 골드·강화석을 반환한다. 기준 HP는 해당 전투 난이도의 첫 일반 보스 등장 최대 HP, bosses는 실제 등장 보스별 등장 최대 HP/전투 중 최저 HP다. 동일 보스는 한 번만 전달한다. 보너스는 서버 계정 연구에서 읽는다. 각 재화는 합산·보너스 후 한 번 내리며 나머지를 이월하지 않는다.
5. `UnitUpgradeCost(targetLevel)`는 일반 유닛 Lv2~20 비용이다. `ResearchUpgradeCost("BASIC",row,stat,value,currentLevel,maxLevel)`는 서버가 읽은 노드 정의·현재 저장 레벨로 계산한다. 목표 노드·선행 조건·보유·최대 레벨 검증과 재화 차감/진행 저장은 구매 핸들러에서 원자적으로 처리한다. 다른 트리·족장에는 자동 적용하지 않는다.

전투 핸들러는 정상 종료와 시간 초과를 정산하고 튜토리얼·테스트·중도 포기·오류 종료를 제외해야 한다. 서버에서 검증한 동일 run ID 영수증으로 재시도를 멱등 처리하고 결과 화면에 확정량을 돌려준다. 클라이언트가 보낸 HP만으로 보상을 지급하지 않는다. 기존 연구 보너스 API에서 다시 보너스나 소수 이월을 적용하지 않는다. 계정 EXP·식량은 별도다.

현재 체크아웃에는 서버 정산 핸들러와 뒤끝 Function CLI가 없다. 이 출력만으로 배포·차감·지급이 연결되지는 않는다. 기존 무료 연구 게임에 비용 필드를 업로드해도 구매 코드는 별도 연결이 필요하다. 이 모듈은 서버 통합 준비용이며 자동 업로드하지 않는다.

공식 문서 확인(2026-11-14): [Function 템플릿](https://docs.backnd.com/sdk-docs/function/template/), [요청 값 읽기](https://docs.backnd.com/sdk-docs/function/request-response/read-input/), [CLI 1.1.1 변경](https://docs.backnd.com/update-note-function/Function-1-1-1/). SDK/CLI 빌드·실서버 배포는 별도 환경에서 확인한다.
