# 상점 목록 차트와 ID 확률표

브랜치 `feature/shop-chart`. 뒤끝 콘솔 업로드·파일 적용은 사용자 담당이다.

## 편집과 추출

`tools/planner-studio/GGD-Planner/GGD-Planner.exe` → **상점** → 일일 / 주간 / 골드 / 인앱.
상품 목록, 지급 재화 Enum, 수량, 결제 재화, 가격, 무료 고정 여부, 가중치, 표시 순서, 활성 여부를 편집한다.
가중치 합계는 자유다. 툴에서 첫 추첨의 `Weight / 후보 가중치 합계` 비율을 표시한다.
**편집용 Excel 저장** 또는 **뒤끝 업로드용 Excel**을 누르면 목록과 확률 파일을 함께 추출한다.

| 추출 파일 | 뒤끝 위치와 이름 | 내용 |
|---|---|---|
| ShopDailyList.xlsx | 차트 `ShopDailyList` | 일일 상품 정보와 무료 고정 상품 |
| ShopDailyProbability.xlsx | 확률 `ShopDailyProbability` | 일일 후보의 ShopID / percent |
| ShopWeeklyList.xlsx | 차트 `ShopWeeklyList` | 주간 상품 정보와 무료 고정 상품 |
| ShopWeeklyProbability.xlsx | 확률 `ShopWeeklyProbability` | 주간 후보의 ShopID / percent |
| ShopCatalog.xlsx | 차트 `ShopCatalog` | 골드/인앱 공통 상품 정보 |
| ShopCatalog_Editing.xlsx | 기획 툴 가져오기·보관용 | 전체 목록과 원본 가중치. 뒤끝 업로드용이 아님 |

`shop-project.json`은 프로젝트 백업이다. `.editor-preview.json`은 Unity 로컬 테스트에 연결한다.
이전 형식의 편집 파일은 무료 고정/골드/인앱 가중치 0, 일일/주간 일반 후보 가중치 1로 이관하며 기존 수량·가격은 보존한다.

## 규칙

- `ShopID`는 전체 목록에서 고유한 양수 정수다. 확률표의 `ShopID`로 목록 차트의 상품 정보를 찾는다.
- `RewardCurrency`는 현재 `Gold`, `Diamond`. 기존 CurrencyType Enum을 공유한다. 신규 재화는 게임 보상 처리와 툴 허용 목록을 함께 확장한다.
- 일일·주간 각각 무료 고정 상품 최대 1개. `FixedFree=1`, `CurrencyType=Free`, `Price=0`, `Weight=0`이다. 지급 재화·수량을 편집할 수 있으며 첫 슬롯에 항상 표시한다.
- 일반 후보의 `Weight`는 1~1,000,000 정수다. 판매 활성 후보만 확률표에 포함한다. 무료 고정 상품은 확률표에서 제외한다.
- 매 기간 무료 고정 + 중복 없는 일반 상품 최대 5개를 진열한다. 뽑힌 상품은 다음 추첨에서 제외하는 가중치 방식이다. 표시 순서는 무료 우선, SortOrder, ShopID 순서다.
- 서버에서는 뒤끝 `GetProbabilitys`로 ID를 받고 중복을 제외한다. 배치당20회, 최대3배치이며 충분한 고유 ID를 얻지 못하면 임의 대체 없이 상점을 비활성화하고 저장하지 않는다. 서버 오류·알 수 없는 ID도 저장하지 않는다.
- 로컬 에디터는 동일 가중치의 비복원 추첨을 사용한다. 화면 새로 그리기는 재추첨하지 않으며 테스트 초기화 버튼은 재추첨한다.
- 일일은 KST05시, 주간은 월요일 KST05시에 초기화한다. 선택된 상품 snapshot과 구매 상태를 저장하고 재접속 시 복원한다. 현재 기간의 상품은 차트를 수정해도 보존하며 다음 초기화부터 새 설정을 사용한다.
- 골드·인앱은 고정 목록, 최대6개다. 인앱 StoreID와 지급량은 기존 Unity IAP 정의와 일치해야 한다. 현금 가격은 스토어에서 받는다.

## 뒤끝 파일 규약

확률표는 `ShopID`, `percent` 두 열이다. `percent`는 가중치에서 자동 계산하며 합계가 정확히100%다.
정수 단위(1단위=0.0000001%)로 나눈 뒤 나머지가 큰 순서로 반올림 오차를 배분한다. 표현 불가능할 정도로 작은 확률은 추출 오류로 안내한다.
단일 시트, 영문·숫자 열 이름, 예약어 `id` 제외, 소수점 최대7자리, 수식 없음으로 추출한다.
목록과 확률은 반드시 같은 추출본을 함께 업로드·적용한다.

공식 규칙: https://docs.backnd.com/guide/console-guide/backnd-base/probability/
SDK 응답/소수점: https://docs.backnd.com/sdk-docs/backend/base/probability/lotto-by-number/

## 임시 밸런스와 확인

Supercell 공식 상점 안내의 무료 로그인 선물/일일 진열 구조를 참고했다. 수량·가격·가중치는 타 게임의 실제 수치가 아닌 자체 초안이다.
https://support.clashroyale.com/hc/en-us/articles/49484889956251-Shop-Offers

일일 무료 골드100, 주간 무료 골드1000. 각 기간 일반 후보8개 가중치는60/50/36/24/14/8/6/2(합200)이고 첫 추첨 비율은30/25/18/12/7/4/3/1%다.
25개 임시 상품을 활성 상태로 넣었다. 기존 기획 툴 작업 파일은 `outputs/shop-chart/planner-before-probability.json`에 백업하고 상점 목록만 임시값으로 교체했다.
원격 콘솔에는 업로드하지 않았다.

`Test_ShopScene.unity`의 ShopPreviewCanvas → Chart Preview에 추출한 editor-preview.json을 TextAsset으로 연결한 뒤 Play한다.
기본 fixture는 기획 툴과 같은 임시 목록이다. 서버·실제 재화·스토어 결제 없이 테스트한다.

검사: `node tools/planner-studio/check-shop-catalog.cjs`, `node tools/planner-studio/check-shop-ui.cjs`.
Unity: `ShopChartEditorChecks.Run`(가중치/중복/화면), `ShopPreviewSceneChecks.Run`(포인터/스크롤), `ShopPersistenceChecks.Run`(기간/재접속)을 run_script로 실행한다.
2026-11-03 사용자 콘솔 적용 후 실서버 33검사 PASS. 기존 Editor 테스트 계정으로 차트3개(일일9/주간9/공통7행)의 전체 컬럼을 추출본과 대조했다. 확률2개는 후보8개씩이며 무료 고정 제외·목록 ID 연결·가중치 일치·합계100%를 확인했다. 운영 ShopCatalogTable 로더와 ShopStockSelection.DrawAsync를 실행해 각6개 고유 슬롯/무료 우선/정확한 목록 참조를 검증하고 테스트 화면에 해당 서버 결과를 표시했다. 실제 구매·재화 차감·PlayerShopData 쓰기는 수행하지 않았다. 테스트 종료 후 Play를 정지했다.
검증 코드는 `Tools/verification/ShopLiveChecks.cs`, 결과는 `outputs/shop-live/RESULT.txt`, 실서버 진열 캡처는 `outputs/shop-live/live-top.png`와 `live-bottom.png`다. 계정 상품 저장·재접속 왕복 및 실제 구매 연결은 별도 범위다.
