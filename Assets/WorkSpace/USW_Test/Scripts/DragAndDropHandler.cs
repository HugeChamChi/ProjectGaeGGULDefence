// DragAndDropHandler.cs — Phase 2 예정 스텁
//
// ── Phase 2 구현 전략 ──────────────────────────────────────────────
//
//  Windows 드래그 앤 드롭은 WM_DROPFILES(0x0233) 메시지로 전달됩니다.
//
//  구현 순서:
//   1. GetActiveWindow()  (User32.dll)   — Unity 창 HWND 획득
//   2. DragAcceptFiles(hwnd, true)  (Shell32.dll) — 드롭 수신 등록
//   3. SetWindowsHookEx 또는 WndProc 서브클래싱 — WM_DROPFILES 수신
//   4. DragQueryFile()  — 드롭된 파일 경로 추출
//   5. _loader.LoadTextureFromPath(path) 호출
//
//  주의사항:
//   - Unity Player WndProc 서브클래싱 시 Unity 내부 메시지 처리와 충돌 가능
//   - 가장 안전한 방법은 C++ Native Plugin에서 IDropTarget COM 인터페이스 구현
//   - 또는 StandaloneFileBrowser 플러그인(오픈소스)의 드롭 기능 활용
//     → https://github.com/gkngkc/UnityStandaloneFileBrowser
//
// ──────────────────────────────────────────────────────────────────

using UnityEngine;

public class DragAndDropHandler : MonoBehaviour
{
    [SerializeField] private RuntimeSpriteLoader _loader;

    // Phase 2에서 구현 예정
    // private void RegisterDropTarget() { }
    // private void OnFilesDropped(string[] paths) { }
    // private void UnregisterDropTarget() { }
}
