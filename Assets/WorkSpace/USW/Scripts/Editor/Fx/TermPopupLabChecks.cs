using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 용어 팝업 실험실 자동 점검 (플레이 중인 FxLab_TermPopup에서 실행). 확정 B안 + 4개 키워드 기준.
/// 실제 입력 모듈처럼 레이캐스트 → 누름/뗌/클릭을 흉내 내서 확인한다:
///   용어 탭 → 시트 열림·카드 선택 안 됨·제목/수치(SO 값) 일치, 영상 용어는 시트 바로 위 필름 재생, 글 용어는 필름 없음·카드 안 가림,
///   같은 카드의 다른 용어 칩 전환, 바깥 탭은 닫기만, 링크 여유 범위, 링크 밖 글자는 카드 선택.
/// 스크린샷은 Temp/TermPopupLab/에 저장된다.
/// </summary>
public static class TermPopupLabChecks
{
    private const string ShotFolder = "Temp/TermPopupLab";
    private static int _pass, _fail;

    // (카드 번호, 용어 ID, 수치 줄에 있어야 할 글자) — 수치는 SO 현재값 기준.
    private static readonly (int Card, string Id, string Fact)[] Terms =
    {
        (0, "chief_skill", "80"),
        (1, "self_destruct_drone", "2.5"),
        (1, "hack_stack", "50"),
        (2, "time_manipulation", "0.3"),
    };

    [MenuItem("Tools/USW/Fx/Term Popup Lab Checks (Play Mode)")]
    public static void Run()
    {
        var lab = Object.FindAnyObjectByType<TermPopupLab>();
        if (!Application.isPlaying || lab == null) { Debug.LogError("[TermPopupLabChecks] 플레이 중인 FxLab_TermPopup에서만 실행한다"); return; }
        RunAsync(lab).Forget();
    }

    private static async UniTaskVoid RunAsync(TermPopupLab lab)
    {
        _pass = _fail = 0;
        Directory.CreateDirectory(ShotFolder);
        var token = lab.GetCancellationTokenOnDestroy();
        try
        {
            await Wait(0.5f, token);
            foreach (var (card, id, fact) in Terms)
            {
                await CheckTermAsync(lab, card, id, fact, token);
                await CheckOutsideTap(lab, id, token);
            }

            // 같은 카드의 다른 용어 칩: 자폭 드론(영상) → 해킹스택(글)으로 바뀌고 필름이 사라진다.
            if (TapTerm(lab, 1, "self_destruct_drone"))
            {
                await Wait(0.8f, token);
                var chip = GameObject.Find("LabCanvas/TermPopup/Sheet/Chips/Chip_hack_stack")?.transform as RectTransform;
                int before = lab.SelectionCount;
                Tap(ScreenOf(chip), "칩 해킹스택");
                await Wait(0.6f, token);
                Expect(lab.PopupOpen && lab.Popup.Title == Name(lab, "hack_stack") && !FilmShown(lab) && lab.SelectionCount == before,
                    "칩으로 같은 카드의 다른 용어 전환 (영상 → 글, 필름 닫힘)");
                Shot("C_chip_switch");
                await Wait(0.2f, token);
                await CheckOutsideTap(lab, "chip", token);
            }

            // 링크 여유 범위: 마지막 줄 단어 바로 아래(글자 상자 밖)를 눌러도 용어가 열리고 카드는 안 골라진다.
            var link = lab.GetLink(1);
            if (link != null && link.TryGetLinkWorldBounds(TermLinker.LinkPrefix + "hack_stack", out var min, out var max))
            {
                float pad = 10f * link.Text.rectTransform.lossyScale.y;
                int before = lab.SelectionCount;
                Tap(WorldToScreen(new Vector3((min.x + max.x) * 0.5f, min.y - pad, min.z)), "해킹 스택 글자 10단위 아래");
                await Wait(0.5f, token);
                Expect(lab.PopupOpen && lab.SelectionCount == before, "링크 여유 범위: 글자 살짝 아래를 눌러도 용어가 열리고 카드는 안 골라짐");
                await CheckOutsideTap(lab, "pad", token);
            }

            // 링크 밖 글자: 설명 첫 글자를 누르면 카드로 넘어가 선택된다 (글자 위 선택이 막히지 않음).
            var bottom = lab.GetLink(2);
            if (bottom != null && bottom.Text.textInfo.characterCount > 0)
            {
                var ch = bottom.Text.textInfo.characterInfo[0];
                var local = (ch.bottomLeft + ch.topRight) * 0.5f;
                int before = lab.SelectionCount;
                Tap(WorldToScreen(bottom.Text.rectTransform.TransformPoint(local)), "설명 첫 글자(링크 밖)");
                await Wait(0.3f, token);
                Expect(!lab.PopupOpen && lab.SelectionCount == before + 1, "링크 밖 설명 글자 탭 → 카드 선택으로 넘어감");
                Shot("X_offlink_select");
                await Wait(1.0f, token);
            }
        }
        catch (System.OperationCanceledException) { }
        Debug.Log($"[TermPopupLabChecks] 끝: PASS {_pass} / FAIL {_fail} (스크린샷 {Path.GetFullPath(ShotFolder)})");
    }

    private static async UniTask CheckTermAsync(TermPopupLab lab, int card, string id, string fact, System.Threading.CancellationToken token)
    {
        if (!lab.Glossary.TryFind(id, out var entry)) { Expect(false, $"사전에 {id} 있음"); return; }
        int before = lab.SelectionCount;
        if (!TapTerm(lab, card, id)) { Expect(false, $"{entry.DisplayName}: 카드 {card} 설명에 링크 걸림"); return; }
        await Wait(entry.Clip != null ? 1.5f : 0.6f, token);
        var popup = lab.Popup;
        Expect(lab.PopupOpen && lab.SelectionCount == before, $"{entry.DisplayName}: 탭 → 시트 열림, 카드 선택 안 됨");
        Expect(popup.Title == entry.DisplayName && popup.Facts.Contains(fact), $"{entry.DisplayName}: 제목·수치(SO) 표시 — \"{popup.Facts}\"");
        Expect(popup.BackdropShown, $"{entry.DisplayName}: 배경 스냅숏 블러 깔림");

        var s = new Vector3[4];
        popup.Sheet.GetWorldCorners(s);
        if (entry.Clip != null)
        {
            bool shown = FilmShown(lab);
            Expect(shown && popup.Film.IsPlaying && popup.Film.NoiseCleared, $"{entry.DisplayName}: 영상 재생, 노이즈 걷힘");
            if (shown)
            {
                var f = new Vector3[4];
                popup.Film.Rect.GetWorldCorners(f);
                float gap = f[0].y - s[1].y;
                float width = (f[2].x - f[0].x) / (s[2].x - s[0].x);
                float topFromScreen = 1f - WorldToScreen(f[1]).y / Screen.height;
                Expect(gap >= -0.001f && width > 0.75f && topFromScreen < 0.2f,
                    $"{entry.DisplayName}: 필름이 화면 위쪽에 크게, 시트와 안 겹침 (폭 {width:0.00}, 위 {topFromScreen:0.00}, 간격 {gap:0.000})");
            }
        }
        else
        {
            Expect(!FilmShown(lab), $"{entry.DisplayName}: 글 용어는 필름 없음");
            var c = new Vector3[4];
            ((RectTransform)lab.GetCard(card).transform).GetWorldCorners(c);
            Expect(c[0].y >= s[1].y - 0.001f, $"{entry.DisplayName}: 누른 카드가 시트에 안 가려짐");
        }
        Shot($"T_{card}_{id}");
        await Wait(0.2f, token);
    }

    private static bool TapTerm(TermPopupLab lab, int card, string id)
    {
        var link = lab.GetLink(card);
        if (link == null || !link.TryGetLinkWorldBounds(TermLinker.LinkPrefix + id, out var min, out var max))
        {
            var info = link != null ? link.Text.textInfo : null;
            var ids = new List<string>();
            if (info != null) for (int i = 0; i < info.linkCount; i++) ids.Add(info.linkInfo[i].GetLinkID());
            Debug.LogWarning($"[TermPopupLabChecks] 링크 못 찾음 card={card} id={id} link={(link != null)} " +
                             $"chars={info?.characterCount} links=[{string.Join(",", ids)}]");
            return false;
        }
        Tap(WorldToScreen((min + max) * 0.5f), id);
        return true;
    }

    private static bool FilmShown(TermPopupLab lab) => lab.Popup?.Film != null && lab.Popup.Film.Rect.gameObject.activeInHierarchy;
    private static string Name(TermPopupLab lab, string id) => lab.Glossary.TryFind(id, out var e) ? e.DisplayName : id;

    private static async UniTask CheckOutsideTap(TermPopupLab lab, string tag, System.Threading.CancellationToken token)
    {
        int before = lab.SelectionCount;
        // 카드 위쪽 빈 자리. 영상 필름은 누름을 받지 않으므로 그 위를 눌러도 방패가 받는다.
        Tap(new Vector2(Screen.width * 0.5f, Screen.height * 0.84f), "위쪽 빈 자리(바깥)");
        await Wait(0.45f, token);
        Expect(!lab.PopupOpen && lab.SelectionCount == before && !FilmShown(lab), $"{tag}: 바깥 탭 → 시트·필름 닫히기만, 카드 선택 안 됨");
    }

    // StandaloneInputModule의 누름 → 뗌 → 클릭 순서를 한 번에 흉내 낸다.
    private static void Tap(Vector2 screen, string label)
    {
        var es = EventSystem.current;
        var data = new PointerEventData(es)
        {
            position = screen, pressPosition = screen, pointerId = -1,
            button = PointerEventData.InputButton.Left, clickCount = 1, eligibleForClick = true,
        };
        var hits = new List<RaycastResult>();
        es.RaycastAll(data, hits);
        if (hits.Count == 0) { Debug.Log($"[TermPopupLabChecks] 탭 {label}: 맞은 것 없음"); return; }
        var go = hits[0].gameObject;
        data.pointerCurrentRaycast = data.pointerPressRaycast = hits[0];
        var press = ExecuteEvents.ExecuteHierarchy(go, data, ExecuteEvents.pointerDownHandler);
        var click = ExecuteEvents.GetEventHandler<IPointerClickHandler>(go);
        if (press == null) press = click;
        data.pointerPress = press;
        data.rawPointerPress = go;
        data.pointerClick = click;
        if (press != null) ExecuteEvents.Execute(press, data, ExecuteEvents.pointerUpHandler);
        if (click != null && click == press) ExecuteEvents.Execute(click, data, ExecuteEvents.pointerClickHandler);
        Debug.Log($"[TermPopupLabChecks] 탭 {label}: {go.name} → 누름 {(press != null ? press.name : "없음")}");
    }

    private static Vector2 ScreenOf(RectTransform rt)
    {
        if (rt == null) return new Vector2(-100f, -100f);
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return WorldToScreen((corners[0] + corners[2]) * 0.5f);
    }

    private static Vector2 WorldToScreen(Vector3 world) => RectTransformUtility.WorldToScreenPoint(Camera.main, world);

    private static void Expect(bool ok, string what)
    {
        if (ok) _pass++; else _fail++;
        if (ok) Debug.Log($"[TermPopupLabChecks] PASS {what}");
        else Debug.LogWarning($"[TermPopupLabChecks] FAIL {what}");
    }

    // 스크린샷은 프레임 끝에 찍힌다 — 찍은 뒤 같은 프레임에 닫지 않게 호출한 쪽에서 잠깐 기다린다.
    private static void Shot(string name) => ScreenCapture.CaptureScreenshot($"{ShotFolder}/{name}.png");

    private static UniTask Wait(float seconds, System.Threading.CancellationToken token) =>
        UniTask.Delay(System.TimeSpan.FromSeconds(seconds), DelayType.Realtime, cancellationToken: token);
}
