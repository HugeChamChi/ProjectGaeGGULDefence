# 아트 검수 툴 (Art Preview Tool)

**위치** `Assets/WorkSpace/USW_Test/`  
**빌드 타겟** Windows x86_64 Standalone (Mono 백엔드 권장)

---

## 개요

아트 작업자가 PNG 스프라이트를 실제 모바일 게임 화면(1080×1920) 맥락에서  
확인할 수 있는 독립 실행형 검수 툴입니다.

---

## 씬 구성

**씬 파일** `ArtPreviewScene.unity`

```
Canvas  (1920×1080 기준 해상도, Scale With Screen Size)
├── PreviewPanel        좌측 540px — 미리보기 영역
│   └── PreviewContainer    9:16 (405×720) 중앙 배치
│       ├── BackgroundImage     배경 (RawImage)
│       ├── SpriteDisplay       스프라이트 표시 (RawImage)
│       ├── GameUIOverlay       게임 UI 목업 오버레이
│       └── GridOverlay         4열×6행 그리드 오버레이
└── ControlPanel        우측 — 조작 패널 (VerticalLayoutGroup)
    ├── 파일 로드 버튼
    ├── 오버레이 Toggle
    ├── 위치·크기 슬라이더 (X / Y / 배율)
    ├── 반전 / 초기화 버튼
    ├── 배경 선택 버튼 (검정 / 흰색 / 회색 / 체커)
    ├── 그리드 투명도 슬라이더
    └── 스크린샷 저장 버튼
```

> **Game 뷰 해상도**: 1280×720 (가로) 으로 설정해야 레이아웃이 정상 표시됩니다.  
> Unity Game 뷰 드롭다운 → `+` → Width 1280 / Height 720 추가 후 선택.

---

## 스크립트 목록

| 파일 | 부착 위치 | 역할 |
|------|-----------|------|
| `ArtPreviewManager.cs` | SystemRoot | 중앙 조율자 — 이벤트 연결, 상태 관리 |
| `RuntimeSpriteLoader.cs` | SystemRoot | Windows 파일 대화상자 + `Texture2D.LoadImage` |
| `SpriteTransformController.cs` | SystemRoot | 위치·크기·좌우반전 슬라이더 제어 |
| `BackgroundSelector.cs` | SystemRoot | 배경 교체 (단색 4종 기본 제공) |
| `ScreenshotSaver.cs` | SystemRoot | PreviewContainer 영역 PNG 저장 |
| `GridOverlayController.cs` | GridOverlay | GridCell_Prefab 4×6 스폰 + 투명도 슬라이더 |
| `DragAndDropHandler.cs` | SystemRoot | Phase 2 예정 스텁 |

---

## 기능 상세

### PNG 파일 로드
- `Comdlg32.dll` Windows 네이티브 파일 대화상자 (STA 스레드 — Unity 프리징 없음)
- `Texture2D.LoadImage(bytes)` 런타임 로드
- 외부 플러그인 불필요

### 그리드 오버레이
- 실제 게임 `GridCell_Prefab` 24개(4×6) 스폰
- `GridCell` 게임 로직 컴포넌트는 `enabled = false` (시각만 사용)
- CanvasGroup alpha 슬라이더로 0~100% 투명도 조절
- **Play 모드에서만 스폰됨** (GridOverlayController.Start 실행 시점)

### 스크린샷 저장
- `WaitForEndOfFrame` → `ReadPixels` → `EncodeToPNG`
- PreviewContainer 영역만 캡처
- 바탕화면에 `ArtPreview_yyyyMMdd_HHmmss.png` 자동 저장

---

## 빌드 방법

```
File > Build Settings
  Platform : PC, Mac & Linux Standalone
  Target   : Windows
  Architecture : x86_64
  Scripting Backend : Mono  ← Comdlg32 P/Invoke 안정성
```

씬 목록에 `ArtPreviewScene` 추가 후 빌드.

---

## Phase 2 예정 기능

| 기능 | 구현 방향 |
|------|-----------|
| 드래그 앤 드롭 | `WM_DROPFILES` 메시지 훅 또는 C++ Native Plugin (IDropTarget) |
| 커스텀 배경 로드 | `BackgroundSelector`에 파일 대화상자 확장 |
| 멀티 스프라이트 비교 | SpriteDisplay 슬롯 추가 |
