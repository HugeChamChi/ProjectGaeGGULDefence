using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 용어 팝업 실험실 자동 점검 (플레이 중인 FxLab_TermPopup에서 실행).
/// 실제 입력 모듈처럼 레이캐스트 → 누름/뗌/클릭을 흉내 내서, 단어 탭이 카드 선택으로 새지 않는지,
/// 바깥 탭이 닫기만 하는지, 링크 밖 글자 탭은 여전히 카드를 고르는지, 말풍선 위/아래 방향과 시트 밀어올리기를 확인한다.
/// 스크린샷은 Temp/TermPopupLab/에 저장된다.
/// </summary>
public static class TermPopupLabChecks
{
    private const string ShotFolder = "Temp/TermPopupLab";
    private static int _pass, _fail;

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
            foreach (var mode in new[] { TermPopupLabMode.Bubble, TermPopupLabMode.Sheet, TermPopupLabMode.BubbleThenSheet })
            {
                string tag = mode == TermPopupLabMode.Bubble ? "A" : mode == TermPopupLabMode.Sheet ? "B" : "AB";
                lab.SetMode(mode);
                await Wait(0.4f, token);

                await CheckWordTap(lab, tag, 0, "액티브 스킬", token);
                if (mode == TermPopupLabMode.Bubble)
                {
                    int before = lab.SelectionCount;
                    Tap(ScreenOf(GameObject.Find("LabCanvas/TermPopup/Bubble/Body")?.transform as RectTransform), "말풍선 본문");
                    await Wait(0.4f, token);
                    Expect(!lab.PopupOpen && lab.SelectionCount == before, $"{tag}: 말풍선을 누르면 닫히기만, 카드 선택 안 됨");
                }
                await CheckOutsideTap(lab, tag, token);
                await CheckWordTap(lab, tag, 2, "아머브레이크", token);
                if (mode == TermPopupLabMode.BubbleThenSheet)
                {
                    int before = lab.SelectionCount;
                    Tap(ScreenOf(GameObject.Find("LabCanvas/TermPopup/Bubble/More")?.transform as RectTransform), "자세히");
                    await Wait(0.5f, token);
                    var sheet = GameObject.Find("LabCanvas/TermPopup/Sheet");
                    Expect(sheet != null && sheet.activeInHierarchy && lab.PopupOpen && lab.SelectionCount == before, $"{tag}: 자세히 → 시트로 이어짐");
                    CheckCardAboveSheet(lab, tag, 2);
                    Shot($"{tag}_3_more_sheet");
                    await Wait(0.2f, token);
                }
                await CheckOutsideTap(lab, tag, token);
            }

            // 링크 판정 여유: 단어 글자 바로 아래(여유 범위 안)를 눌러도 용어가 열린다.
            lab.SetMode(TermPopupLabMode.Bubble);
            await Wait(0.3f, token);
            var link = lab.GetLink(1);
            if (link != null && link.TryGetLinkWorldBounds("term:화상", out var min, out var max))
            {
                float pad = 10f * link.Text.rectTransform.lossyScale.y;
                int before = lab.SelectionCount;
                Tap(WorldToScreen(new Vector3((min.x + max.x) * 0.5f, min.y - pad, min.z)), "화상 글자 10단위 아래");
                await Wait(0.4f, token);
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

    private static async UniTask CheckWordTap(TermPopupLab lab, string tag, int cardIndex, string term, System.Threading.CancellationToken token)
    {
        var link = lab.GetLink(cardIndex);
        if (link == null || !link.TryGetLinkWorldBounds("term:" + term, out var min, out var max))
        {
            Expect(false, $"{tag}: {term} 링크 위치 찾기");
            return;
        }
        int before = lab.SelectionCount;
        Tap(WorldToScreen((min + max) * 0.5f), term);
        await Wait(0.5f, token);
        Expect(lab.PopupOpen && lab.SelectionCount == before, $"{tag}: '{term}' 탭 → 팝업 열림, 카드 선택 안 됨");

        var bubble = GameObject.Find("LabCanvas/TermPopup/Bubble")?.transform as RectTransform;
        if (bubble != null && bubble.gameObject.activeInHierarchy)
        {
            var corners = new Vector3[4];
            bubble.GetWorldCorners(corners);
            bool upperWord = cardIndex == 0;
            bool ok = upperWord ? corners[1].y <= min.y + 0.001f : corners[0].y >= max.y - 0.001f;
            Expect(ok, $"{tag}: '{term}' 말풍선이 {(upperWord ? "단어 아래" : "단어 위")}에 뜸");
        }
        if (tag == "B") CheckCardAboveSheet(lab, tag, cardIndex);
        Shot($"{tag}_{(cardIndex == 0 ? "1_top" : "2_bottom")}");
        await Wait(0.2f, token);
    }

    private static void CheckCardAboveSheet(TermPopupLab lab, string tag, int cardIndex)
    {
        var sheet = GameObject.Find("LabCanvas/TermPopup/Sheet")?.transform as RectTransform;
        var link = lab.GetLink(cardIndex);
        if (sheet == null || link == null) return;
        var card = link.GetComponentInParent<LevelUpCardUI>();
        var s = new Vector3[4];
        var c = new Vector3[4];
        sheet.GetWorldCorners(s);
        ((RectTransform)card.transform).GetWorldCorners(c);
        Expect(c[0].y >= s[1].y - 0.001f, $"{tag}: 카드 {cardIndex}가 시트 위로 밀려 올라가 안 가려짐");
    }

    private static async UniTask CheckOutsideTap(TermPopupLab lab, string tag, System.Threading.CancellationToken token)
    {
        int before = lab.SelectionCount;
        // 카드 위쪽 빈 자리 (화면 가운데는 말풍선의 "자세히 보기"와 겹칠 수 있다).
        Tap(new Vector2(Screen.width * 0.5f, Screen.height * 0.84f), "위쪽 빈 자리(바깥)");
        await Wait(0.45f, token);
        Expect(!lab.PopupOpen && lab.SelectionCount == before, $"{tag}: 바깥 탭 → 닫히기만, 카드 선택 안 됨");
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

    private static void Shot(string name) => ScreenCapture.CaptureScreenshot($"{ShotFolder}/{name}.png");

    private static UniTask Wait(float seconds, System.Threading.CancellationToken token) =>
        UniTask.Delay(System.TimeSpan.FromSeconds(seconds), DelayType.Realtime, cancellationToken: token);
}
