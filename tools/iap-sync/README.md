# IAP Catalog Sync — 적용 설명서

`Assets/Resources/IAPProductCatalog.json`에 정의된 상품을 Google Play Console 인앱 상품(One-time products)에
Android Publisher API(`monetization.onetimeproducts.batchUpdate` + `purchaseOptions.batchUpdateStates`)로
일괄 생성/수정하고 자동으로 활성화(ACTIVE)까지 처리하는 스크립트입니다.

## 팀원이 할 일 (한 번만)

### 1. 인증 파일 받기

프로젝트 담당자가 만들어둔 OAuth 클라이언트 파일을 아래 링크에서 받아 저장하세요.

- 다운로드: <https://drive.google.com/file/d/1_ZBFjykzZMKwN8tMW7POAt-UQp6GvHNG/view?usp=sharing>
- 저장 위치: `tools/iap-sync/client_secret.json` (파일명 그대로, 경로 정확히)

> ⚠️ 이 파일은 git에 커밋하지 마세요 (`.gitignore`로 이미 제외되어 있습니다). 링크 자체도 팀 외부에 공유하지 마세요.

### 2. 권한 확인

아래 두 가지가 안 돼 있으면 로그인/실행이 막힙니다. 안 되면 프로젝트 담당자에게 요청하세요.

1. **Play Console 사용자 초대** — 본인 구글 계정이 Play Console "사용자 및 권한"에 이 앱의 인앱 상품 관리 권한으로 초대되어 있어야 함
2. **OAuth 테스트 사용자 등록** — 본인 구글 계정이 Google Cloud 프로젝트의 "OAuth 동의 화면 > 테스트 사용자" 목록에 등록되어 있어야 함 (앱이 아직 검증 전 상태라 목록에 없는 계정은 로그인 자체가 막힘)

## 사용법

```bash
cd tools/iap-sync
npm install
npm run sync
```

- 처음 실행하면 콘솔에 브라우저 로그인 링크가 출력됩니다. 링크를 열어 본인 Google 계정으로 로그인 + 동의하면,
  이후로는 로컬에 캐싱된 토큰(`.token.json`, git에 커밋 안 됨)으로 자동 인증됩니다.
- 카탈로그를 수정한 뒤 다시 `npm run sync`만 실행하면 Play Console에 그대로 반영됩니다 (기존 상품은 업데이트, 없는 상품은 새로 생성).

## 이 스크립트가 실제로 하는 일

1. `Assets/Resources/IAPProductCatalog.json`을 읽어서 상품별 id/제목/설명/가격/지급 다이아 수량을 파싱
2. `monetization.onetimeproducts.batchUpdate`로 Play Console에 상품 생성/수정 (리전은 우선 US/USD 기준)
3. 새로 생성된 구매 옵션은 기본적으로 초안(DRAFT) 상태로 만들어지므로, `purchaseOptions.batchUpdateStates`로 자동으로 활성화(ACTIVE) 처리까지 이어서 수행

## 문제 해결

| 증상 | 원인 / 조치 |
|---|---|
| "앱은 현재 테스트 중이며 개발자가 승인한 테스터만..." | 본인 계정이 OAuth 테스트 사용자 목록에 없음 → 담당자에게 등록 요청 후 재시도 (등록 직후엔 반영까지 1~2분 걸릴 수 있음) |
| 로그인 페이지가 안 뜨거나 계속 실패 | 브라우저에 여러 구글 계정이 로그인돼 있으면 엉뚱한 계정으로 시도될 수 있음 → 로그인 화면에서 "다른 계정 사용"으로 정확한 계정 선택, 또는 시크릿 창 사용 |
| `Error: listen EADDRINUSE: address already in use :::53682` | 이전에 실행한 스크립트가 로그인 완료 없이 계속 떠 있는 상태. 그 프로세스를 종료하고 다시 실행 |
| 상품 상태가 계속 DRAFT로 보임 | 정상 동작이면 스크립트가 같은 실행 안에서 자동으로 ACTIVE로 바꿉니다. 재실행해서 `[buy:ACTIVE]`로 뜨는지 확인 |
| Google Cloud에서 서비스 계정 키 생성이 막힘 (`iam.disableServiceAccountKeyCreation`) | 이 스크립트는 서비스 계정 키를 아예 안 씁니다(OAuth 사용자 로그인 방식). 이 에러를 만났다면 다른 방식으로 인증을 시도한 것이니 이 README의 방식(`client_secret.json` + 브라우저 로그인)을 그대로 따르세요 |

## 참고 / 제약사항

- 구독(subscription) 타입 상품은 아직 지원하지 않고 건너뜁니다 (현재 카탈로그에는 소비성 상품만 있음)
- 가격은 카탈로그의 `googlePrice.num`을 USD 기준으로 그대로 사용합니다. 다른 통화/리전을 추가하려면 `sync.js`의 `CURRENCY`, `regionalPricingAndAvailabilityConfigs` 부분을 수정하세요
- 앱 패키지명은 `sync.js`의 `PACKAGE_NAME` 상수(`com.GaeGGUL`)로 고정되어 있습니다
- 카탈로그의 한글 제목/설명은 `\uXXXX` 이스케이프 형태로 저장되어 있을 수 있는데(Unity IAP Catalog 창이 이렇게 저장), 스크립트가 자동으로 디코딩해서 보내므로 카탈로그 파일을 따로 손볼 필요는 없습니다
