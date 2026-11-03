---
system: ui-authoring
title: 실제 설정창 UI 제작실
keywords: UI Authoring, ui-authoring, 제작실, UI 제작 도구, UI 시뮬레이터, UI 실험실, 폰트 실험, TTF, OTF, G06, 설정창 편집, XML 프리팹, 아트 루트, 아트 받기, rclone
verified: 2026-11-01
commit: de2cbd00
---
# UI 제작실 — UI3030

## 2026-11-01 폰트 편집 지연과 Undo 묶음 수정 (미커밋)

`UiLabWindow.Interaction`은 프레임 정지를 입력 중단과 구분하고, 글자 속성 편집에 기존 계층을 재사용한다. `UiLabWorkspace.Apply`의 typographyOnly 경로는 외형 오버라이드를 누적하지 않으며 임시 머티리얼을 재사용/정리한다. `ImportFont()`는 UI 버튼과 선택창 등록의 공통 진입점이다. 최종 독립 Player 349 PASS(실험174/정보33/Arial43/맑은고딕43/제작실56), 실제 버튼부터 일반 폴더 TTF 선택/등록/제목 적용/저장/종료 PASS. [결과와 처리시간 및 검증 경계](../../../outputs/ui-studio-font-fix-20261101/RESULT.md). 고정 키트는 이번에 교체하지 않았다.

## 2026-11-01 파일 선택 창과 종료 확인 v1.12 (미커밋)

`StudioFileDialog`는 선택 창을 STA 스레드에서 열고 메인 스레드 콜백으로 결과를 준다. OnGUI 안 동기 `GetOpenFileName` 호출이 v1.11 Player에서 스택 오버플로를 냈고 폰트 등록이 실패했다. 모든 호출(실험실 폰트, 제작실 키트 열기/다른 이름/그림/아트 폴더)은 콜백 API를 쓴다. `UiLabWindow`는 미저장 종료 시 보이는 확인창(`QuitPromptVisible`, `ResolveQuit`)을 띄운다. 실험실이 열려 있으면 `StudioWindow.CheckQuit`은 복구본만 남기고 종료 판단을 넘긴다. 실험174/정보33/상호작용42/제작실56 PASS. [근거/검사/제한](../../../outputs/ui-lab-fix-verification/RESULT.md).

## 2026-10-30 정보창과 토글 복구 v1.11 (미커밋 추가)

`UiLabExport`는 유닛/토템 정보창을 실제 `IngameScene`의 현재 인스턴스에서 가져온다. 기존 별도 유닛 prefab에는 연결이 없는 슬롯과 테스트 표시가 남아 있었다. 델탄 SO와 토템 SO를 원래 Presenter에 넣어 아이콘/기본 수치/설명을 채운다. `DescriptionToggleView`의 HideAndDontSave 런타임 스프라이트는 영구 asset으로 복사하고, `UiLabInfoPreview`가 원래 View에서 캡처한 간단히/자세히 문구/레이아웃/트랙/손잡이/라벨 상태만 표시한다. 게임 콜백은 제거한다. 두 정보창 ID에 `scene-info/`를 붙여 옛 인덱스별 실험값이 새 계층의 다른 요소에 적용되지 않게 한다. 원본 XML은 보존한다. Player 정보창33/기존170/상호작용42, 설치9 PASS. [근거/검사/제한](../../../outputs/ui-info-fix-verification/RESULT.md).

## 2026-10-30 창 비율과 자동 반영 v1.10 (미커밋 추가)

사용자 근거 `design/프로그램예시1.png`: 프로그램 창 비율에서 속성/버튼 잘림, 수동 적용의 불편, 미리보기 휠 줌과 스크롤바 정렬을 수정했다. `UiLabWindow.Interaction.cs`는 창 크기에 따른 GUI 좌표/픽셀 좌표, 휠 영역 판정/포인터 기준 줌/이동, 유효한 라이브 입력과 초안을 소유한다. `UiLabWorkspace.BeginLiveGroup/EndLiveGroup`이 연속 입력의 Undo를 묶고 폰트 변경 때 원래 아웃라인을 보존한다. 속성 패널 콘텐츠 폭을 고정하고 세로 스크롤만 사용한다. `UiLabTheme`의 가로 슬라이더 padding과 세로 트랙/손잡이 폭은 독립적으로 맞춘다. 실험 저장/게임 설치는 기존 경계를 유지한다. [사용법](../../technical/ui-authoring-studio.md), [근거/검사/제한](../../../outputs/ui-lab-live-verification/RESULT.md).

## 2026-10-30 외형 정리 v1.9 (미커밋 추가)

사용자가 시뮬레이터 프로그램 자체의 외형 변경을 요청했다. `UiLabTheme`의 소유하는 스킨/둥근 패널/버튼을 기존 제작실과 실험실에 적용하고, `UiLabWindow.Layout.cs`에서 라이브러리 3탭/속성 3탭/색상 견본과 RGBA/슬라이더/하단 타임라인을 정리했다. `UiLabFontPreview`는 선택 폰트를 카메라가 분리된 작은 RenderTexture에 그려 예문을 보여준다. 미리보기는 별도 RectMask2D와 확대/맞춤을 사용한다. UI-only 캡처는 이 마스크를 일시적으로 끄고 복원한다. 실험 XML과 게임 시각 스냅샷은 기존 계약을 유지한다. [현재 사용법](../../technical/ui-authoring-studio.md), [실제 화면/검사](../../../outputs/ui-lab-polish-verification/RESULT.md).

## 2026-10-30 UI 실험실 v1.8 (미커밋 추가)

사용자 지시로 별도 실험 모드를 추가했다. `commit`은 기존 기반 커밋이며 아래 코드는 이번 working tree에 추가됐고 커밋하지 않았다. `StudioWindow.OpenLab`이 기존 편집 상태를 보존하고 `UiLabWindow`를 연다. `UiLabWorkspace`가 외부 TTF/OTF와 실험 XML/Undo/머티리얼을 소유하고, `UiLabPlayback`이 원본 스케일 또는 실험 프리셋을 시간으로 평가한다. `UiLabTemplate`은 실제 표시 계층의 안정적인 참조다. `Tools/ui-authoring/UiLabExport.cs`는 preview scene에서 실제 레벨업/토템/정보창/강화/아웃게임 10개 표시를 캡처하며 런타임 생성 계층과 compact 패널 크기를 보존한다. `UiLabSmokeDriver.cs`가 독립 Player를 검증한다.

실행기 `outputs/UI-Authoring-Kit/Start UI Lab.cmd`, 원본 `outputs/UI-Authoring-Kit/UILab/`의 폰트/experiment.xml, 내보내기 `outputs/ui-lab-content/lab.xml`. 게임 명령/서버/원본 자동 설치 없음. TTF/OTF 각170, Core107, 기존 제작실56/native16, 배포보존8, 설치9 통과. [현재 사용법](../../technical/ui-authoring-studio.md), [검증과 한계](../../../outputs/ui-lab-verification/RESULT.md). Android/원본 복합 연출 전체는 미검증/미지원이다.

실제 공통 설정창 75요소·16그림·KCC-Ganpan SDF를 독립 Windows 도구에서 편집한다. DocumentCore/UGUI 렌더러를 게임과 공유한다. 전체 제품/Android 합격은 현재 안내와 TODO의 검증 경계를 따른다.

## 2026-10-30 정적 대조
기존의 유효하지 않은 working tree 포함 commit 표기를 실제 병합 커밋으로 교체했다. 게임 호스트의 구운 프리팹 경로와 원본 native 모드의 기존 프리팹 경로를 구분한다. 원본/구운 프리팹 도구, 게시 자산, 아틀라스와 편집기 비교 진입이 이 커밋에 포함됐다.

기준은 `de2cbd00`. 아래 날짜별 미커밋 표현과 검증 횟수는 당시 기록이며 현재 미커밋 상태나 이번 재실행을 뜻하지 않는다. 이번에는 문서 대조만 했고 Unity/Android 검증은 실행하지 않았다. [대조 및 미등록 분류](../refresh-20261030.md).

## 진입 파일
| 파일 | 역할 |
|---|---|
| `Assets/WorkSpace/HSD/UIExternal/Studio/StudioFileDialog.cs` | Windows 파일/폴더 선택 창을 STA 스레드에서 열고 메인 스레드 콜백으로 전달 |
| `Assets/WorkSpace/HSD/UIExternal/AuthoringRuntime/UiLabInfoPreview.cs` | 게임 서비스 없는 정보창 간단히/자세히 표시와 네이티브 토글 시각 상태 |
| `Tools/ui-authoring/UiLabInfoSmokeDriver.cs` | 실제 씬 정보창, 번들 스프라이트, 토글 입력과 편집 보존 검사 |
| `Tools/ui-authoring/UiLabInfoProbe.cs` | 샘플 SO 아이콘의 실제 로드 여부를 읽기 전용 확인 |
| `Assets/WorkSpace/HSD/UIExternal/Studio/UiLabWindow.Interaction.cs` | 프로그램 창 비율 대응, 유효 입력 자동 반영/Undo 묶음과 포인터 휠/패닝 |
| `Tools/ui-authoring/UiLabInteractionSmokeDriver.cs` | 5개 창 크기/비율, 컨트롤 잘림, 라이브 입력/Undo/휠 영역 검사 |
| `Assets/WorkSpace/HSD/UIExternal/Studio/UiLabTheme.cs` | 기존 제작실/실험실의 데스크톱 스킨과 임시 텍스처 소유권 |
| `Assets/WorkSpace/HSD/UIExternal/Studio/UiLabFontPreview.cs` | 실제 선택 폰트의 소형 TMP 예문 렌더링 |
| `Assets/WorkSpace/HSD/UIExternal/Studio/UiLabWindow.Layout.cs` | 라이브러리/속성 탭, 조절 UI와 미리보기 도구/타임라인 |
| `Tools/ui-authoring/UiLabShellSmokeDriver.cs` | 두 해상도의 실제 GUI 캡처와 외형 상태 불변 검사 |
| `Assets/WorkSpace/HSD/UIExternal/AuthoringRuntime/UiLabTemplate.cs` | 실험실 실제 시각 스냅샷과 원본 스케일 연출 참조 |
| `Assets/WorkSpace/HSD/UIExternal/Studio/UiLabWindow.cs` | 인게임/아웃게임 전환과 글자/외형/연출/A-B 실험 UI |
| `Assets/WorkSpace/HSD/UIExternal/Studio/UiLabWorkspace.cs` | 외부 폰트 파일, 상대 경로, 실험 저장/Undo와 머티리얼 소유권 |
| `Assets/WorkSpace/HSD/UIExternal/Studio/UiLabPlayback.cs` | 결정적인 시간 평가, 배속/반복/정지 복원 |
| `Tools/ui-authoring/UiLabExport.cs` | 실제 UI 10종과 런타임 생성 토템/강화 계층 캡처 |
| `Tools/ui-authoring/UiLabSmokeDriver.cs` | Windows Player의 폰트/화면/저장/연출 검사 |
| `Assets/WorkSpace/HSD/Scripts/Editor/UIAuthoring/NativeLobbyPrefabBridge.cs` | 우편함/출석/계정 원본 계층 번들, 인덱스 경로와 file ID로 왕복, 원본 충돌 차단 |
| `Tools/ui-authoring/LobbyNativeChecks.cs` | 로비 3종 Unity 원본 적용/참조 보존/복원 검사 |
| `Tools/ui-authoring/LobbyNativeSmokeDriver.cs` | 실제 독립 실행본의 로비 편집/게시/연출 검사 |
| `Assets/WorkSpace/HSD/Scripts/Editor/UIAuthoring/NativeSettingsPrefabBridge.cs` | 원본 설정 prefab 내보내기/고정 ID 왕복/다음 리비전/백업 |
| `Assets/WorkSpace/HSD/UIExternal/DocumentCore/NativePrefabContract.cs` | 원본 모드 지원 속성 및 구조 불변 검사 |
| `Assets/WorkSpace/HSD/UIExternal/AuthoringRuntime/NativePrefabTemplate.cs` | 실제 계층 ID와 기존 연출 프로파일 |
| `Assets/WorkSpace/HSD/UIExternal/AuthoringRuntime/NativePrefabMotion.cs` | 독립 제작실 열기/닫기 미리보기, 편집 스케일 복원 |
| `Assets/WorkSpace/HSD/UIExternal/AuthoringRuntime/NativeVisualProperty.cs` | 원본 컴포넌트 표시 속성 적용 |
| `Tools/ui-authoring/NativePrefabChecks.cs` | 원본 적용/이름 변경/다음 내보내기/바이트 복원 검사 |
| `Tools/ui-authoring/NativeStudioSmokeDriver.cs` | 실제 Windows 실행본 native 모드 검사 |
| `Assets/WorkSpace/HSD/UIExternal/DocumentCore/AuthoringWorkspace.cs` | 원본 모델·Undo/Redo·충돌/원자 저장·복구 |
| `Assets/WorkSpace/HSD/UIExternal/DocumentCore/UiComposer.cs` | 중첩 프리팹/슬롯/오버라이드·타입/속성 검증 |
| `Assets/WorkSpace/HSD/UIExternal/DocumentCore/ResolvedUiDocument.cs` | 별도 해석 트리와 출처 |
| `Assets/WorkSpace/HSD/UIExternal/DocumentCore/PrefabEditing.cs` | 추출·원본 적용·되돌리기·연결 해제 |
| `Assets/WorkSpace/HSD/UIExternal/DocumentCore/IAuthoringBindings.cs` | mock/game 공통 상태·명령 |
| `Assets/WorkSpace/HSD/UIExternal/DocumentCore/UiPublishLint.cs` | 터치 기본값과 조합 후 게시 경고, 아틀라스 지정 검사 |
| `Assets/WorkSpace/HSD/UIExternal/DocumentCore/KitPublisher.cs` | 내장 불변 게시·마지막 포인터 교체 |
| `Assets/WorkSpace/HSD/UIExternal/DocumentCore/PersonalArtSettings.cs` | APPDATA 개인 아트 루트, 공유 XML과 분리 |
| `Assets/WorkSpace/HSD/UIExternal/DocumentCore/ArtImageSource.cs` | 상대경로 검증, 개인 원본 우선과 키트 복사본 대체 |
| `Assets/WorkSpace/HSD/UIExternal/AuthoringRuntime/AuthoredUiView.cs` | 같은 UGUI/TMP 렌더러와 이벤트/재료 수명 |
| `Assets/WorkSpace/HSD/UIExternal/AuthoringRuntime/BakedUiDocument.cs` | 구운 프리팹의 ID·UI 참조·상태/명령 연결, 게임에서 XML 해석 없음 |
| `Assets/WorkSpace/HSD/UIExternal/AuthoringRuntime/AuthoredSwitch.cs` | 실제 sliding toggle의 편집 가능한 입력 어댑터 |
| `Assets/WorkSpace/HSD/UIExternal/AuthoringRuntime/AuthoringAssets.cs` | 설치형 문서/그림/폰트 참조 |
| `Assets/WorkSpace/HSD/UIExternal/AuthoringRuntime/AuthoringResources.cs` | 외부 PNG/JPG 또는 설치형 리소스 소유권 |
| `Assets/WorkSpace/HSD/UIExternal/Studio/StudioWindow.cs` | 시각 편집·파일·속성·계층·모드 UI |
| `Assets/WorkSpace/HSD/UIExternal/Studio/StudioBindings.cs` | 개발자 catalog 계약의 모의 상태/명령 |
| `Assets/WorkSpace/HSD/UIExternal/Studio/StudioFileDialog.cs` | 제작자 Windows 파일 선택, Editor 미의존 |
| `Assets/WorkSpace/HSD/UIExternal/DocumentCore/*.asmdef` | 엔진 비의존 문서 어셈블리 |
| `Assets/WorkSpace/HSD/UIExternal/AuthoringRuntime/*.asmdef` | 공유 렌더러 어셈블리 |
| `Assets/WorkSpace/HSD/UIExternal/Studio/*.asmdef` | Windows 제작 UI 어셈블리 |
| `Assets/WorkSpace/HSD/Scripts/UI/Setting/ISettingsView.cs` | Presenter의 작은 View 계약 |
| `Assets/WorkSpace/HSD/Scripts/UI/Setting/SettingsGameBindings.cs` | 실제 오디오·선호·프레임·닫기/확인 요청 |
| `Assets/WorkSpace/HSD/Scripts/UI/Setting/UI_SettingPanel_Base.cs` | 기존 호스트/애니메이션 유지, 새 그래프 설치 |
| `Assets/WorkSpace/HSD/Scripts/UI/Setting/UI_SettingPanel_InGame.cs` | AllowOpening·기존 진입 가드 |
| `Assets/WorkSpace/HSD/Scripts/UI/Setting/UI_SettingPresenter.cs` | 기존 저장·확인·씬 전환 로직 |
| `Assets/WorkSpace/HSD/Scripts/Editor/UIAuthoring/SettingsExportProbe.cs` | Unity가 해석한 실제 두 사용처 조사 |
| `Assets/WorkSpace/HSD/Scripts/Editor/UIAuthoring/SettingsAuthoringExporter.cs` | 실제 prefab·그림·정확한 폰트 추출 |
| `Assets/WorkSpace/HSD/Scripts/Editor/UIAuthoring/SettingsAuthoringBridge.cs` | 검증 게시본 설치·기존 원본 백업·빌드 신선도 검사 |
| `Assets/WorkSpace/HSD/Scripts/Editor/UIAuthoring/SettingsPrefabBaker.cs` | 게시본을 비활성 프리팹·공유 머티리얼로 굽고 게시 해시 검증 |
| `Assets/WorkSpace/HSD/Scripts/Editor/UIAuthoring/SettingsUiPrefabComparison.cs` | XML 조립/구운 프리팹/기존 프리팹 에디터 비교와 씬 복원 |
| `Assets/WorkSpace/HSD/Generated/UiAuthoring/Baked/**` | SO가 참조하는 생성 프리팹과 머티리얼, 직접 편집하지 않음 |
| `Assets/WorkSpace/HSD/Scripts/Editor/UIAuthoring/UiAuthoringAtlases.cs` | Bridge 전용 SpriteAtlasV2 생성과 포함 여부, 빌드 검사 |
| `ProjectSettings/EditorSettings.asset` | UI SpriteAtlasV2 패킹 활성화 |
| `Assets/WorkSpace/HSD/Resources/UiAuthoring/Atlases/*.spriteatlasv2` | 게시 그림 그룹에서 생성된 UI 아틀라스, Imports 밖 |
| `Assets/WorkSpace/HSD/Scripts/Editor/UIAuthoring/SettingsGameIntegrationChecks.cs` | 실제 호스트/서비스·디자이너 왕복 Play 검사 |
| `Assets/WorkSpace/HSD/Scripts/Editor/UIAuthoring/SettingsSceneUseChecks.cs` | 실제 씬의 설정창 인스턴스/오버라이드·씬 무변경 조사 |
| `Assets/WorkSpace/HSD/Resources/UiAuthoring/Settings.asset` | 생성된 게임 설치 자산, 직접 편집 금지 |
| `Data/UI/Authoring/*.xml` | 유지 원본 인덱스·리소스/기능 계약 |
| `Data/UI/Authoring/Screens/*.xml` | 실제 화면 원본 |
| `Data/UI/Authoring/Prefabs/*.xml` | 재사용 원본 |
| `Data/UI/Authoring/Images/*.png` | 기존 그림의 외부 편집용 추출본 |
| `Tools/ui-authoring/StudioBuild.cs` | 독립 도구 씬/폰트/Windows 빌드 |
| `Tools/ui-authoring/prepare_project.py` | 운영 설정을 바꾸지 않는 빌드 셸 생성 |
| `Tools/ui-authoring/build.ps1` | 숨긴 배치 Editor 빌드 |
| `Tools/ui-authoring/package.py` | 프로젝트/Editor/Python 없는 키트/ZIP |
| `Tools/ui-authoring/Receive-Art.ps1` | 전용 읽기 전용 OAuth 로그인, GGD_ArtWork copy, 변경 감지와 한국어 오류 안내 |
| `Tools/ui-authoring/bundle_art_tools.py` | 배포본에만 팀 client와 rclone 포함, 사용자 토큰 제외 |
| `Tools/ui-authoring/Test-ReceiveArt.ps1` | 로그인/반복 받기/만료/요청 제한 흐름 격리 검사 |
| `Tools/ui-authoring/Art-Readme.txt` | 키트에 동봉하는 기획/아트용 설명 |
| `Tools/ui-authoring/ArtChecks.cs` | 기존16 그림 artPath 보충, Unity GUID 검사와 게시 설치 |
| `Tools/ui-authoring/release.ps1` | 호스트에서 제작실 종료 대기 후 고정 경로 갱신 |
| `Tools/ui-authoring/release.py` | 임시 포장, 원본 승계, previous 순환, 검증 백업과 실패 복구 |
| `Tools/ui-authoring/test_release.py` | 원본 승계, 변조 거부, 실패 복구, 경로 제한 검사 |
| `Tools/ui-authoring/migrate_releases.py` | v1~v1.4 최초 정리와 원본 보존 기록 |
| `Tools/ui-authoring/StudioSmokeDriver.cs` | 실제 설정창 편집·게시 acceptance |
| `Tools/ui-authoring/OptimizationChecks.cs` | P1~P3 설치와 패킹 확인, live Play 실행 입구 |
| `Tools/ui-authoring/PrefabBakeChecks.cs` | P4 설치·75요소 대조·게임 검사·에디터 비교 입구 |
| `Tools/ui-authoring/CoreChecks.cs` | 엔진 없는 중첩/슬롯/참조/원자 편집 검사 |
| `Tools/ui-authoring/CoreChecks.csproj` | 개발자용 문서 검사 빌드, Creator 미필요 |
| `docs/technical/ui-authoring-studio.md` | 현재 사용법·지원/검증 경계 |
| `docs/proposals/ui-authoring-studio.md` | 최초 지정 설계·합격 기준 보존 |
| `docs/architecture/adr-0004-ui-authoring-studio.md` | 채택 경계 |

## 불변 규칙
- 별도 샘플을 실제 UI 이관으로 간주하지 않는다. UI3030 및 기존 리소스 키를 보존한다.
- 쓰기 원본과 해석된 트리를 분리한다. 인스턴스에 원본 전체를 복사 저장하지 않는다.
- 상태 주입은 명령/저장을 발행하지 않는다. 게임은 mock 상태를 읽지 않는다.
- 기존 호스트가 열기/닫기/가드/확인을 소유한다. 새로운 시각 트리와 기존 입력을 동시에 활성화하지 않는다.
- 디자이너가 쓰는 열린 창/미저장 원본을 빌드/검사 도구가 덮어쓰지 않는다.
- 버전별 실행 폴더를 늘리지 않는다. 임시 빌드 후 호스트에서 제작실 종료를 기다리고 고정 키트를 교체한다. Authoring 승계, 비런타임 파일 검증 백업, previous 하나를 유지한다.
- G06 미완 작업은 docs/TODO.md 한 곳에 둔다. Windows 증거를 Android 실기기 합격으로 바꾸지 않는다.
- 개인 루트와 OAuth 토큰은 공유 XML/배포본에 넣지 않는다. team.json은 배포본 전용이며 git에 넣지 않는다. 아트 받기는 전용 config/read-only/copy만 사용하고 meta와 로컬 삭제를 제외한다.

## 로컬 생성 파일
| 파일 | 역할 |
|---|---|
| `outputs/UI-Authoring-Kit/**` | 최신 고정 배포 폴더, version.txt로 버전 표시 |
| `outputs/UI-Authoring-Kit-previous/**` | 바로 이전 버전 하나 |
| `outputs/UI-Authoring-Kit-staging/**` | 교체 전 후보, 실패 시 조사용 보존 |
| `outputs/ui-authoring-build-staging/**` | 성공 배포 후 정리하는 임시 빌드 |
| `outputs/ui-authoring-user-backups/**` | 퇴역 키트의 편집 원본, 복구 파일, 로그 보존 |
| `outputs/ui-authoring-release-cleanup/**` | 최초 정리의 해시 목록과 검증 결과 |
| `outputs/ui-authoring-project/**` | 개발자 전용 생성 Unity 프로젝트 |
| `outputs/ui-authoring-verification/**` | 추출·편집·왕복/서비스 검사·참조 증거 |
| `outputs/ui-authoring-import-backup/**` | Bridge 설치 전 유지 원본 백업 |

- v1.3 결함 수정 증거: `outputs/ui-authoring-fixes/RESULT.md`. 이전 독립 리뷰는 당시 결함 재현 증거로 보존한다.

- 10-28 P1~P3: 조합 후 외곽선 글자17개가 머티리얼1개 공유, 명시값 우선 그림 raycast 기본값, 내장 게시 경고, UI3030 그림16장이 2048×4096 한 장, 화면 Canvas own/inherit와 Undo. `outputs/ui-authoring-optimization/RESULT.md`: Core93 / 독립 EXE50 / 게임 Play41 PASS. 이후 P4 프리팹 굽기를 채택·구현했으며 Android 실기기 성능은 미측정이다.
- 10-28 P4: Unity Bridge 설치 시 75요소 프리팹·공유 머티리얼 생성, 게임은 Instantiate와 직렬화 참조 바인딩. 게임 호스트에서 Resolve/Create 호출을 제거했다. 75요소 대조·오래된 프리팹 거부·Play43 / 에디터3방식 각5회 PASS. `outputs/ui-authoring-bake-20261028/RESULT.md`. v1.4 제작실 EXE 사용 유지, XML 경로는 제작실과 명시적 에디터 비교에만 남긴다. 원격 UI 배포는 도입하지 않았다.
