using System.Collections.Generic;
using System.IO;
using GaeGGUL.Animation;
using HSD.UI.Setting;
using HSD.UI.Utils;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 인게임 설정창 프리팹(InGame_Setting_Popup)을 시안(design/설정창이렇게일단만들어주세요.png)대로 만든다.
/// 기존 UI_SettingPanel_InGame·UI_SoundSettingSlot·UI_SlidingToggle·UI_Popup_Confirm을 그대로 연결한다.
/// 그림은 설정창 전용 아트 KHJ_Artwork/Setting(SPR_UI1100~1104)만 쓴다. 원본 메타(9-slice 등)는 건드리지 않는다.
/// 좌표는 시안(1080×1920 기준) 실측값. 다시 실행하면 프리팹을 새로 만든다.
/// </summary>
public static class SettingPopupBuilder
{
    private const string PrefabPath = "Assets/WorkSpace/HSD/Prefab/UI/InGame/InGame_Setting_Popup.prefab";
    private const string PreviewScenePath = "Assets/WorkSpace/USW/Scene/FxLab_SettingPopup.unity";
    private const string Art = "Assets/Imports/GGD_ArtWork/KHJ_Artwork/Setting";
    private const string FontGuid = "8863727b6c787ba4a910762f78e8fd55"; // KCC-Ganpan SDF (검은 외곽선 내장)
    private const string BgPath = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Battlefield_BG/Battlefield_BG_Extended.jpg";

    // ── 시안 실측(캔버스 1080×1920, 화면 중앙 기준) ──
    private static readonly Vector2 WindowPos = new Vector2(0f, -61f);
    private static readonly Vector2 WindowSize = new Vector2(928f, 1336f);
    private static readonly Vector2 InnerPos = new Vector2(0f, 27f);   // 창 기준
    private static readonly Vector2 InnerSize = new Vector2(837f, 957f);
    private const float TitleY = 585f;
    private const float RowIconX = -305f;
    private const float SliderX = -32f;
    private const float SliderWidth = 383f;
    private const float BoxX = 203f;
    private const float StateTextX = 282f;
    private const float LabelLeftX = -213f;
    private const float ButtonsY = -546f;
    private static readonly Vector2 ButtonSize = new Vector2(372f, 92f);
    // 그림마다 투명 여백이 달라서(예: 패널 845×1091만 보임/1031×1238), 아래 값은 "보이는 부분"의 시안 실측이다. Fit()이 여백을 보정한다.
    private static readonly Vector2 GearPos = new Vector2(5f, 738f);
    private static readonly Vector2 GearVisible = new Vector2(280f, 190f);
    private static readonly Vector2 TabVisible = new Vector2(225f, 82f);
    private const float IconVisible = 80f;
    private const float BoxVisible = 48f;
    private static readonly Vector2 CheckVisible = new Vector2(82f, 66f);
    private static readonly Vector2 CheckOffset = new Vector2(7f, 15f);
    private static readonly Vector2 ArrowVisible = new Vector2(46f, 54f);
    private const float KnobVisible = 50f;
    private static readonly Dictionary<Texture2D, Rect> VisibleCache = new();
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);

    [MenuItem("Tools/USW/UI/Build Setting Popup Prefab")]
    public static void BuildMenu() => Debug.Log(Build());

    /// <summary>스프라이트 가져오기 설정 → 프리팹 생성. 결과 문자열을 돌려준다.</summary>
    public static string Build()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));

        var root = new GameObject("InGame_Setting_Popup", typeof(RectTransform));
        var rootRt = (RectTransform)root.transform;
        Stretch(rootRt);
        var panel = root.AddComponent<UI_SettingPanel_InGame>();

        var dim = Img("Dim", rootRt, null, DimColor, Vector2.zero, Vector2.zero);
        Stretch(dim.rectTransform);
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.transition = Selectable.Transition.None;

        var window = Rect("Window", rootRt, WindowPos, WindowSize);
        Fit(Img("Gear", window, Sprite(Art, "SPR_UI1100_GearDecoration"), Color.white, Vector2.zero, Vector2.zero), GearPos, GearVisible, true); // 창 뒤(먼저 그림)
        Fit(Img("Frame", window, Sprite(Art, "SPR_UI1100_Background"), Color.white, Vector2.zero, Vector2.zero), Vector2.zero, WindowSize, false);
        Txt("Title", window, font, "설정", 86f, new Vector2(0f, TitleY), new Vector2(500f, 110f));

        var innerImg = Img("Inner", window, Sprite(Art, "SPR_UI1100_interior"), Color.white, Vector2.zero, Vector2.zero);
        Fit(innerImg, InnerPos, InnerSize, false); // 여백이 상하좌우 대칭이라 중심은 그대로
        var inner = innerImg.rectTransform;
        Tab(inner, font, "Tab_Sound", "사운드", new Vector2(-263f, 399f));
        var bgm = SoundRow(inner, font, "BGM_Row", AudioGroup.BGM, Sprite(Art, "SPR_UI1101_BackgroundmusicIcon_Nomal"), 277f);
        var sfx = SoundRow(inner, font, "SFX_Row", AudioGroup.SFX, Sprite(Art, "SPR_UI1101_SoundeffectIcon_Nomal"), 133f);
        Tab(inner, font, "Tab_Game", "게임", new Vector2(-253f, 0f));
        var vibration = ToggleRow(inner, font, "Vibration_Row", "진동 효과", Sprite(Art, "SPR_UI1102_VibrationeffectIcon"), -131f);
        var damage = ToggleRow(inner, font, "DamageFloater_Row", "데미지 플로터", Sprite(Art, "SPR_UI1102_damagefloaterIcon"), -270f);
        FrameRow(inner, font, -392f);

        var back = BigButton(window, font, "Back_Button", "뒤로가기", "SPR_UI1100_BackButton", new Vector2(-216f, ButtonsY), ButtonSize);
        var lobby = BigButton(window, font, "Lobby_Button", "로비이동", "SPR_UI1100_LobbyButton", new Vector2(215f, ButtonsY), ButtonSize);

        var anim = window.gameObject.AddComponent<Anim_UI_Scale>();
        Configure(anim, a =>
        {
            a.FindProperty("animationTarget").objectReferenceValue = window;
            a.FindProperty("triggerType").enumValueIndex = 0; // None — UI_Base가 직접 재생
            a.FindProperty("ignoreTimeScale").boolValue = true; // 설정창은 게임 시간을 멈춘다
            a.FindProperty("startMultiplier").vector2Value = new Vector2(0.85f, 0.85f);
            a.FindProperty("endMultiplier").vector2Value = new Vector2(0.9f, 0.9f);
            a.FindProperty("durationIn").floatValue = 0.22f;
            a.FindProperty("durationOut").floatValue = 0.12f;
        });

        var confirm = ConfirmPopup(rootRt, font);

        Configure(panel, p =>
        {
            var slots = p.FindProperty("_soundSlots");
            slots.arraySize = 2;
            slots.GetArrayElementAtIndex(0).objectReferenceValue = bgm;
            slots.GetArrayElementAtIndex(1).objectReferenceValue = sfx;
            p.FindProperty("toggle_DamageFloater").objectReferenceValue = damage;
            p.FindProperty("toggle_Vibration").objectReferenceValue = vibration;
            p.FindProperty("confirmPopup").objectReferenceValue = confirm;
            p.FindProperty("btn_Close").objectReferenceValue = back;
            p.FindProperty("btn_BackgroundClose").objectReferenceValue = dimButton;
            p.FindProperty("targetAnim").objectReferenceValue = anim;
            p.FindProperty("btn_GoToLobby").objectReferenceValue = lobby;
        });

        root.SetActive(false); // 설정창은 열릴 때 켜진다(켜지는 순간 게임 시간 정지)
        EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return $"[SettingPopup] 프리팹 저장: {PrefabPath}";
    }

    // ── 줄 구성 ──────────────────────────────────────────────────────

    private static void Tab(RectTransform parent, TMP_FontAsset font, string name, string label, Vector2 pos)
    {
        var tab = Img(name, parent, Sprite(Art, "SPR_UI1100_Separatorline"), Color.white, Vector2.zero, Vector2.zero);
        var center = Fit(tab, pos, TabVisible, true);
        Txt("Label", tab.rectTransform, font, label, 44f, center + new Vector2(0f, 2f), TabVisible);
    }

    private static UI_SoundSettingSlot SoundRow(RectTransform parent, TMP_FontAsset font, string name, AudioGroup group, Sprite icon, float y)
    {
        var row = Rect(name, parent, new Vector2(0f, y), new Vector2(760f, 110f));
        Icon(row, icon, new Vector2(RowIconX, 0f), IconVisible);

        var sliderRt = Rect("Slider", row, new Vector2(SliderX, 0f), new Vector2(SliderWidth, 44f));
        var bg = Img("Background", sliderRt, Sprite(Art, "SPR_UI1101_SoundBarFrame"), Color.white, Vector2.zero, Vector2.zero);
        Fit(bg, Vector2.zero, new Vector2(SliderWidth, 44f), false);
        var fillArea = Rect("Fill Area", sliderRt, Vector2.zero, Vector2.zero);
        Stretch(fillArea, new Vector2(6f, 6f), new Vector2(-6f, -6f));
        var fill = Img("Fill", fillArea, Sprite(Art, "SPR_UI1101_SoundBar"), Color.white, Vector2.zero, Vector2.zero);
        fill.preserveAspect = false;
        Stretch(fill.rectTransform);
        var handleArea = Rect("Handle Slide Area", sliderRt, Vector2.zero, Vector2.zero);
        Stretch(handleArea, new Vector2(22f, 0f), new Vector2(-22f, 0f));
        var handle = Img("Handle", handleArea, Sprite(Art, "SPR_UI1101_SoundBarHandle"), Color.white, Vector2.zero, new Vector2(64f, 0f));
        handle.rectTransform.anchorMin = new Vector2(0f, 0f);
        handle.rectTransform.anchorMax = new Vector2(0f, 1f);
        handle.rectTransform.sizeDelta = new Vector2(KnobVisible, KnobVisible - 44f); // 세로는 슬라이더 높이(44) 기준 늘림
        handle.raycastTarget = true;
        var slider = sliderRt.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.transition = Selectable.Transition.None;

        var toggleRt = Rect("Mute_Toggle", row, new Vector2((BoxX + StateTextX) * 0.5f + 12f, 0f), new Vector2(170f, 90f));
        var hit = Img("HitArea", toggleRt, null, new Color(1f, 1f, 1f, 0f), Vector2.zero, Vector2.zero);
        Stretch(hit.rectTransform);
        float local = BoxX - toggleRt.anchoredPosition.x;
        var box = Img("Box", toggleRt, Sprite(Art, "SPR_UI1101_Mute_ToggleOn"), Color.white, Vector2.zero, Vector2.zero);
        Fit(box, new Vector2(local, 0f), Vector2.one * BoxVisible, true);
        var on = Rect("SoundOn", toggleRt, Vector2.zero, Vector2.zero);
        Stretch(on);
        Txt("ON", on, font, "ON", 42f, new Vector2(StateTextX - toggleRt.anchoredPosition.x, 0f), new Vector2(110f, 60f));
        var off = Rect("SoundOff", toggleRt, Vector2.zero, Vector2.zero);
        Stretch(off);
        Fit(Img("Check", off, Sprite(Art, "SPR_UI1101_Mute_ToggleOff"), Color.white, Vector2.zero, Vector2.zero), new Vector2(local, 0f) + CheckOffset, CheckVisible, true);
        Txt("OFF", off, font, "OFF", 42f, new Vector2(StateTextX - toggleRt.anchoredPosition.x + 6f, 0f), new Vector2(110f, 60f));
        off.gameObject.SetActive(false);
        var toggle = toggleRt.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = box;
        toggle.graphic = null;
        toggle.transition = Selectable.Transition.None;
        toggle.isOn = true; // 켜짐 = 소리 남

        var slot = row.gameObject.AddComponent<UI_SoundSettingSlot>();
        Configure(slot, s =>
        {
            s.FindProperty("_group").enumValueIndex = (int)group;
            s.FindProperty("_slider").objectReferenceValue = slider;
            s.FindProperty("_muteToggle").objectReferenceValue = toggle;
            s.FindProperty("_objSoundOn").objectReferenceValue = on.gameObject;
            s.FindProperty("_objSoundOff").objectReferenceValue = off.gameObject;
        });
        return slot;
    }

    private static UI_SlidingToggle ToggleRow(RectTransform parent, TMP_FontAsset font, string name, string label, Sprite icon, float y)
    {
        var row = Rect(name, parent, new Vector2(0f, y), new Vector2(760f, 110f));
        Icon(row, icon, new Vector2(RowIconX + 3f, 0f), IconVisible + 6f);
        var text = Txt("Label", row, font, label, 44f, Vector2.zero, new Vector2(380f, 70f));
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.rectTransform.pivot = new Vector2(0f, 0.5f);
        text.rectTransform.anchoredPosition = new Vector2(LabelLeftX, 0f);

        var toggleRt = Rect("Toggle", row, new Vector2((BoxX + StateTextX) * 0.5f + 12f, 0f), new Vector2(170f, 90f));
        var hit = Img("HitArea", toggleRt, null, new Color(1f, 1f, 1f, 0f), Vector2.zero, Vector2.zero);
        Stretch(hit.rectTransform);
        float local = BoxX - toggleRt.anchoredPosition.x;
        Fit(Img("Box", toggleRt, Sprite(Art, "SPR_UI1101_Mute_ToggleOn"), Color.white, Vector2.zero, Vector2.zero), new Vector2(local, 0f), Vector2.one * BoxVisible, true);
        var on = Rect("On", toggleRt, Vector2.zero, Vector2.zero);
        Stretch(on);
        Txt("ON", on, font, "ON", 42f, new Vector2(StateTextX - toggleRt.anchoredPosition.x, 0f), new Vector2(110f, 60f));
        var off = Rect("Off", toggleRt, Vector2.zero, Vector2.zero);
        Stretch(off);
        Fit(Img("Check", off, Sprite(Art, "SPR_UI1101_Mute_ToggleOff"), Color.white, Vector2.zero, Vector2.zero), new Vector2(local, 0f) + CheckOffset, CheckVisible, true);
        Txt("OFF", off, font, "OFF", 42f, new Vector2(StateTextX - toggleRt.anchoredPosition.x + 6f, 0f), new Vector2(110f, 60f));
        off.gameObject.SetActive(false);

        var slider = toggleRt.gameObject.AddComponent<Slider>(); // UI_SlidingToggle 필수 컴포넌트(핸들 없음)
        slider.transition = Selectable.Transition.None;
        slider.interactable = false;
        var sliding = toggleRt.gameObject.AddComponent<UI_SlidingToggle>();
        Configure(sliding, s =>
        {
            s.FindProperty("backgroundOn").objectReferenceValue = on.gameObject;
            s.FindProperty("backgroundOff").objectReferenceValue = off.gameObject;
        });
        return sliding;
    }

    private static void FrameRow(RectTransform parent, TMP_FontAsset font, float y)
    {
        var row = Rect("FrameRate_Row", parent, new Vector2(0f, y), new Vector2(760f, 110f));
        Txt("Label", row, font, "프레임", 44f, new Vector2(-280f, 0f), new Vector2(200f, 70f));
        var arrow = Sprite(Art, "SPR_UI1103_ArrowButton"); // 왼쪽 화살표 — 다음 버튼은 좌우 반전
        var prev = ArrowButton(row, "Prev", arrow, new Vector2(-105f, 0f), false);
        var value = Txt("Value", row, font, "60", 46f, new Vector2(93f, 0f), new Vector2(160f, 70f));
        var next = ArrowButton(row, "Next", arrow, new Vector2(292f, 0f), true);
        var selector = row.gameObject.AddComponent<UI_FrameRateSelector>();
        Configure(selector, s =>
        {
            s.FindProperty("btn_Prev").objectReferenceValue = prev;
            s.FindProperty("btn_Next").objectReferenceValue = next;
            s.FindProperty("txt_Value").objectReferenceValue = value;
        });
    }

    private static Button ArrowButton(RectTransform parent, string name, Sprite sprite, Vector2 pos, bool flip)
    {
        var img = Img(name, parent, sprite, Color.white, Vector2.zero, Vector2.zero);
        img.raycastTarget = true;
        Fit(img, pos, ArrowVisible, true);
        if (flip)
        {
            img.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            img.rectTransform.anchoredPosition = pos + (pos - img.rectTransform.anchoredPosition); // 뒤집어도 보이는 중심 유지
        }
        var button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        button.transition = Selectable.Transition.None;
        return button;
    }

    /// <summary>"…_Nomal"/"…_Pressed" 그림 한 쌍으로 눌림 상태를 바꾸는 버튼.</summary>
    private static Button BigButton(RectTransform parent, TMP_FontAsset font, string name, string label, string spriteBase, Vector2 pos, Vector2 size)
    {
        var img = Img(name, parent, Sprite(Art, spriteBase + "_Nomal"), Color.white, Vector2.zero, Vector2.zero);
        img.raycastTarget = true;
        var center = Fit(img, pos, size, false);
        Txt("Label", img.rectTransform, font, label, 50f, center + new Vector2(0f, 4f), size);
        var button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        button.transition = Selectable.Transition.SpriteSwap;
        var state = button.spriteState;
        state.pressedSprite = Sprite(Art, spriteBase + "_Pressed");
        button.spriteState = state;
        return button;
    }

    private static UI_Popup_Confirm ConfirmPopup(RectTransform root, TMP_FontAsset font)
    {
        var popupRt = Rect("Confirm_Popup", root, Vector2.zero, Vector2.zero);
        Stretch(popupRt);
        var dim = Img("Dim", popupRt, null, DimColor, Vector2.zero, Vector2.zero);
        Stretch(dim.rectTransform);
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        // SPR_UI1104 나가기 팝업 세트: 배경 933×557, 글상자 815×234, 잎 장식. 버튼은 설정창 버튼 그림을 다시 쓴다.
        var window = Rect("Window", popupRt, new Vector2(0f, 60f), new Vector2(933f, 557f));
        var frame = Img("Frame", window, Sprite(Art, "SPR_UI1104_ExitPopupBG"), Color.white, Vector2.zero, Vector2.zero);
        frame.preserveAspect = false;
        Stretch(frame.rectTransform);
        Img("Decoration", window, Sprite(Art, "SPR_UI1104_ExitPopupDecoration"), Color.white, new Vector2(-430f, 240f), Vector2.one * 150f);
        var title = Txt("Title", window, font, "로비로 이동", 64f, new Vector2(0f, 200f), new Vector2(760f, 90f));
        var box = Img("TextBox", window, Sprite(Art, "SPR_UI1104_ExitPopupTextBox"), Color.white, new Vector2(0f, 20f), new Vector2(815f, 234f));
        box.preserveAspect = false;
        var desc = Txt("Description", box.rectTransform, font, "진행중인 게임이 종료되며\n로비로 돌아갑니다.", 40f, Vector2.zero, new Vector2(780f, 210f));
        var no = BigButton(window, font, "No_Button", "아니오", "SPR_UI1100_BackButton", new Vector2(-200f, -190f), ButtonSize);
        var yes = BigButton(window, font, "Yes_Button", "예", "SPR_UI1100_LobbyButton", new Vector2(200f, -190f), ButtonSize);
        var popup = popupRt.gameObject.AddComponent<UI_Popup_Confirm>();
        var anim = window.gameObject.AddComponent<Anim_UI_Scale>();
        Configure(anim, a =>
        {
            a.FindProperty("animationTarget").objectReferenceValue = window;
            a.FindProperty("triggerType").enumValueIndex = 0;
            a.FindProperty("ignoreTimeScale").boolValue = true;
            a.FindProperty("startMultiplier").vector2Value = new Vector2(0.85f, 0.85f);
            a.FindProperty("endMultiplier").vector2Value = new Vector2(0.9f, 0.9f);
            a.FindProperty("durationIn").floatValue = 0.2f;
            a.FindProperty("durationOut").floatValue = 0.12f;
        });
        Configure(popup, p =>
        {
            p.FindProperty("txt_Title").objectReferenceValue = title;
            p.FindProperty("txt_Description").objectReferenceValue = desc;
            p.FindProperty("btn_Yes").objectReferenceValue = yes;
            p.FindProperty("btn_No").objectReferenceValue = no;
            p.FindProperty("btn_BackgroundClose").objectReferenceValue = dimButton;
            p.FindProperty("targetAnim").objectReferenceValue = anim;
        });
        popupRt.gameObject.SetActive(false);
        return popup;
    }

    // ── 스프라이트 ───────────────────────────────────────────────────

    /// <summary>
    /// 그림의 "보이는 부분"(알파 경계)이 target 중심·크기에 오도록 RectTransform을 키우고 옮긴다.
    /// keepAspect면 보이는 부분이 target 안에 비율대로 들어가게 한다. 돌려주는 값은 rect 기준 target 중심(자식 글자 위치용).
    /// </summary>
    private static Vector2 Fit(Image img, Vector2 center, Vector2 size, bool keepAspect)
    {
        var rt = img.rectTransform;
        img.preserveAspect = false;
        var tex = img.sprite != null ? img.sprite.texture : null;
        Rect vis = tex != null ? VisibleRect(tex) : new Rect(0, 0, 1, 1); // 0~1, y 위쪽이 1
        Vector2 rectSize;
        if (keepAspect && tex != null)
        {
            float scale = Mathf.Min(size.x / (vis.width * tex.width), size.y / (vis.height * tex.height));
            rectSize = new Vector2(tex.width, tex.height) * scale;
        }
        else rectSize = new Vector2(size.x / vis.width, size.y / vis.height);
        var visCenter = new Vector2((vis.center.x - 0.5f) * rectSize.x, (vis.center.y - 0.5f) * rectSize.y);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = rectSize;
        rt.anchoredPosition = center - visCenter;
        return visCenter;
    }

    /// <summary>PNG 원본을 읽어 알파가 있는 영역을 0~1 비율로 구한다(가져오기 설정과 무관).</summary>
    private static Rect VisibleRect(Texture2D tex)
    {
        if (VisibleCache.TryGetValue(tex, out var cached)) return cached;
        var copy = new Texture2D(2, 2);
        copy.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(tex)));
        var px = copy.GetPixels32();
        int w = copy.width, h = copy.height, x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 8) { x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
        Object.DestroyImmediate(copy);
        var r = x1 < 0 ? new Rect(0, 0, 1, 1) : new Rect(x0 / (float)w, y0 / (float)h, (x1 - x0 + 1) / (float)w, (y1 - y0 + 1) / (float)h);
        VisibleCache[tex] = r;
        return r;
    }

    private static Sprite Sprite(string folder, string name)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{name}.png");
        if (sprite == null) Debug.LogError($"[SettingPopup] 스프라이트 없음: {folder}/{name}.png");
        return sprite;
    }

    // ── 도우미 ───────────────────────────────────────────────────────

    private static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static Image Img(string name, Transform parent, Sprite sprite, Color color, Vector2 pos, Vector2 size, bool sliced = false)
    {
        var rt = Rect(name, parent, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (sliced && sprite != null) img.type = Image.Type.Sliced;
        else if (sprite != null) img.preserveAspect = true;
        return img;
    }

    private static void Icon(RectTransform parent, Sprite sprite, Vector2 pos, float size)
    {
        Fit(Img("Icon", parent, sprite, Color.white, Vector2.zero, Vector2.zero), pos, Vector2.one * size, true); // 설정 전용 아이콘은 외곽선이 그려져 있다
    }

    private static TextMeshProUGUI Txt(string name, Transform parent, TMP_FontAsset font, string value, float size, Vector2 pos, Vector2 box)
    {
        var rt = Rect(name, parent, pos, box);
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    private static void Stretch(RectTransform rt) => Stretch(rt, Vector2.zero, Vector2.zero);

    private static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    private static void Configure(Object target, System.Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // ── 미리보기 씬 ──────────────────────────────────────────────────

    /// <summary>실험실 미리보기 씬: 배경 + 1080×1920 캔버스 + 프리팹. Play하면 바로 열린다.</summary>
    public static string BuildPreviewScene()
    {
        var active = EditorSceneManager.GetActiveScene();
        if (active.isDirty) return "현재 씬에 저장 안 된 변경이 있어 중단: " + active.path;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = new GameObject("LabCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = true;
        cam.orthographicSize = 10f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.07f, 0.1f, 1f);
        cam.transform.position = new Vector3(0f, 0f, -10f);
        var bg = AssetDatabase.LoadAssetAtPath<Sprite>(BgPath);
        if (bg != null) DisigmanRailgunFxLabBuilder.NewSprite("Battlefield_BG", bg, new Vector3(0f, 0f, 20f), 1.6f, -100);
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 5f;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGo.transform);
        new GameObject("SettingPopupPreview").AddComponent<SettingPopupPreview>().Configure(instance.GetComponent<UI_SettingPanel_InGame>());
        EditorSceneManager.SaveScene(scene, PreviewScenePath);
        return $"[SettingPopup] 미리보기 씬: {PreviewScenePath}";
    }
}
