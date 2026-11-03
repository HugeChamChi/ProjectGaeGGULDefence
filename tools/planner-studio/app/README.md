# GGD 기획 작업실 — Electron 0.3.0

기획자가 키워드로 항목을 찾고, 설명을 보며 수치를 수정하고, 개인 실험을 저장한 뒤 Excel로 내보내는 로컬 시제품이다.

실행은 프로젝트 루트의 `GGD-Planner/GGD-Planner.exe`. 전달할 때는 `GGD-Planner.zip` 또는 `GGD-Planner/` 폴더 전체를 사용한다. 단일 exe만 복사하면 Electron 런타임이 빠져 실행할 수 없다. 기획자 PC에는 Unity·Node·Python·Excel 설치가 필요 없다.

유지할 사용법·검사 범위: [docs/technical/planner-studio.md](../../docs/technical/planner-studio.md).

EXE 옆 `자료/`에 지원 Excel2개·항목안내·자료정보를 동봉한다. 자료 목록에서 검사/새로고침/선택 가져오기를 제공한다. 설정에서 다른 폴더를 선택하면 해당 위치를 우선하며 이후 출력은 `내보내기/<생성 시각>-<ID>/`에 모인다. 사용자별 설정·기존 실험·기준 사본·이전 출력은 보존하고, 다른 PC로 전달하는 실험에는 개인 폴더 설정을 포함하지 않는다. 자동 감시/기준 덮어쓰기나 미지원 Excel 자동 매핑은 없다.

오른쪽 **설명 편집**에서 설명과 입력 예시를 바꾼다. 기본 문장·변경 영향은 `schema.cjs`, 사용자 수정은 사용자 작업 폴더의 `field-guides.json`에 보관한다. 설명은 Excel에서 읽는 문장이 아니며 원본 Excel/서버/수치 실험을 변경하지 않는다. 수정한 설명은 검색에 반영하며 `.ggdplan`에 동봉한다. 수신 PC는 설명 유지/적용/가져오기 취소를 명시 선택한다. Excel 출력에는 별도 `변경요약.md`도 생성한다.

## 개발

Node 22.12 이상. 의존성은 package-lock.json에 고정한다.

```powershell
npm ci --no-fund --no-audit
npm run setup:electron
npm start
npm test
npm run package
```

Electron 44 npm 패키지는 런타임 설치 명령을 따로 제공하므로 `setup:electron`을 실행한다. 이 스크립트는 공식 설치 코드를 호출하며 캐시는 프로젝트 `outputs/planner-studio/electron-cache`에 둔다. `package`는 배포 폴더를 만들며 압축은 별도다. 개발 도구/실험/게임 저장소를 새로 만들거나 원격에 push하지 않는다.

## 소스 책임

| 파일 | 책임 |
|---|---|
| `schema.cjs` | 표시 이름·실제 연결 키·단위·범위·별칭·Excel 위치·적용 경로·수치 비교식 |
| `workbook.cjs` | 지원하는 OOXML 형식 검사, 셀 수정, 파생 수식 캐시 재계산, ZIP 다른 내용 보존 |
| `library.cjs` | 동봉 자료·안내 생성, 폴더 목록·파일 형식 검사·해시 변경/선택 검증 |
| `store.cjs` | 기준 자료 해시 보관, 로컬 실험 이력, 개인 엑셀 폴더 설정, 전달 묶음, Excel/manifest 내보내기 |
| `main.cjs` | Electron 창/프로토콜/파일 선택/IPC, 단일 작업실 인스턴스 |
| `preload.cjs` | 제한된 UI API 노출 |
| `ui/` | 검색·편집·키 설명·수치 그래프·실험/내보내기 화면 |
| `seeds/` | 실제 프로젝트 Excel의 보존된 기준 스냅샷. 최신 서버 자료라는 의미가 아님 |
| `test/core.test.cjs` | 원본·캐시·버전·다른 PC 전달·형식 거부 검증 |

새 데이터 추가는 `schema.cjs`의 항목 설명과 입력/출력 규격, `workbook.cjs`의 검증/변환을 함께 구현한다. 이름만 비슷한 Excel을 자동 연결하지 않는다.

패키징은 기존 GGD-Planner/자료 파일을 덮어쓰지 않는다. 전달 ZIP은 `outputs/planner-studio/latest-package.json`의 `staged` 폴더를 사용해 새로 만든다. 기존 배포 폴더를 통째로 압축하면 개인 자료가 섞일 수 있다. 0.3.0 근거: `outputs/planner-portable-20261018/RESULT.md`.
