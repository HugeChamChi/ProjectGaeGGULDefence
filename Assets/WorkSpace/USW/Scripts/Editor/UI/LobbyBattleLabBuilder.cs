using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 로비 전투 탭 시안 실험실(FxLab_LobbyBattle)을 만든다 — 사용자 10-16 "로비예시1처럼 가운데 로비만, 가져올 건 가져오고 버릴 건 버리고".
/// 현재 LobbyScene을 복사해 상단 HUD·하단 5탭은 그대로 두고, 전투 페이지(MainUI_StagePanel)의 기존 가운데 내용만 숨긴 뒤 시안을 올린다.
/// 원본 LobbyScene은 저장하지 않는다. 다시 실행하면 실험실 씬을 새로 만든다. Build Settings에는 넣지 않는다.
/// </summary>
public static class LobbyBattleLabBuilder
{
    /// <summary>실험실 씬 경로.</summary>
    public const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_LobbyBattle.unity";
    private const string LobbyPath = "Assets/Scenes/LobbyScene.unity";
    private const string LabelFontPath = "Assets/Imports/Font/NotoSansKR-Bold SDF.asset";
    private const string NumberFontPath = "Assets/Imports/Font/LilitaOne-Regular SDF.asset";
    private const string BackdropPath = "Assets/Imports/Background2.png";
    private const string ChiefPath = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Drone_Deck/Character_Designs/Alphang+HoverPad_1.png";
    private const string ChiefPortraitPath = "Assets/Imports/GGD_ArtWork/LHH_Artwork/Drone_Deck/Character_Designs/Alphang_1.png";
    private const string LobbyUiRoot = "Assets/Imports/GGD_ArtWork/KHJ_Artwork/LOBBY_UI/";
    private static readonly string[] BossPaths =
    {
        "Assets/Imports/GGD_ArtWork/JSY_Artwork/BOSS/SPR_Boss1_001.png",
        "Assets/Imports/GGD_ArtWork/JSY_Artwork/BOSS/SPR_Boss2_001.png",
        "Assets/Imports/GGD_ArtWork/JSY_Artwork/BOSS/SPR_Boss3_001.png",
        "Assets/Imports/GGD_ArtWork/LHH_Artwork/Boss/Boss_Designs/Boss_Crocodile.png",
    };

    private const float TopHudHeight = 213f;
    private const float BottomTabHeight = 250f;
    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

    [MenuItem("Tools/USW/UI/Build Lobby Battle Lab")]
    public static void Build() => BuildScene();

    /// <summary>실험실 씬을 새로 만들고 경로를 돌려준다.</summary>
    public static string BuildScene()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene; preserve it first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null && !AssetDatabase.DeleteAsset(ScenePath))
            throw new InvalidOperationException("Old lab delete failed.");
        if (!AssetDatabase.CopyAsset(LobbyPath, ScenePath)) throw new InvalidOperationException("Lobby copy failed.");
        var scene = EditorSceneManager.OpenScene(ScenePath);

        var controller = Object.FindFirstObjectByType<BottomPanelController>(FindObjectsInactive.Include);
        if (controller == null) throw new InvalidOperationException("BottomPanelController not found.");
        StripApplication(scene, controller);

        var stage = Find(scene, "MainUI_StagePanel");
        foreach (Transform child in stage) child.gameObject.SetActive(false);

        var labelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LabelFontPath);
        var numberFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NumberFontPath);
        var rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        var goldIcon = Find(scene, "MainUI_TopPanel_GoldIcon").GetComponent<Image>();
        var diamondIcon = Find(scene, "MainUI_TopPanel_DiamondIcon").GetComponent<Image>();
        var staminaIcon = Find(scene, "MainUI_TopPanel_StaminaIcon").GetComponent<Image>();
        var staminaText = Find(scene, "MainUI_TopPanel_StaminaText").GetComponent<TextMeshProUGUI>();

        // ── 페이지 안: 배경 · 길 · 하단/측면 ──
        var root = Rect("BattleLab", stage);
        Stretch(root);
        root.SetAsFirstSibling();

        var backdrop = Img("Backdrop", root, Load(BackdropPath), Color.white);
        backdrop.preserveAspect = false;
        var fitter = backdrop.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 2000f / 3000f;
        var shade = Img("Shade", root, null, new Color(0.08f, 0.06f, 0.14f, 0.35f));
        Stretch(shade.rectTransform);

        var track = Img("Track", root, null, new Color(0f, 0f, 0f, 0f));
        track.raycastTarget = true;
        var trackRect = track.rectTransform;
        trackRect.anchorMin = Vector2.zero;
        trackRect.anchorMax = Vector2.one;
        trackRect.offsetMin = new Vector2(0f, BottomTabHeight);
        trackRect.offsetMax = new Vector2(0f, -TopHudHeight);
        track.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Content", trackRect);
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(1f, 0f);
        content.pivot = new Vector2(0.5f, 0f);
        content.sizeDelta = new Vector2(0f, 2000f);
        var scroll = track.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = trackRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.elasticity = 0.1f;
        scroll.decelerationRate = 0.135f;
        scroll.scrollSensitivity = 30f;

        var overlay = Rect("Overlay", root);
        Stretch(overlay);

        // 최고 기록 배지 (왼쪽 아래) + 받을 보상 수
        var best = Img("BestBadge", overlay, rounded, new Color(0.1f, 0.08f, 0.16f, 0.88f));
        Sliced(best, 0.3f);
        best.raycastTarget = true;
        Place(best.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32f, BottomTabHeight + 300f), new Vector2(240f, 180f));
        var bestButton = best.gameObject.AddComponent<Button>();
        var bestValue = Txt("Value", best.rectTransform, "0", numberFont, 104f, new Color(1f, 0.79f, 0.24f));
        Place(bestValue.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -66f), new Vector2(230f, 110f));
        var bestLabel = Txt("Label", best.rectTransform, "최고 라운드", labelFont, 28f, new Color(1f, 1f, 1f, 0.85f));
        Place(bestLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 34f), new Vector2(230f, 40f));
        var claimBadge = Img("ClaimBadge", best.rectTransform, rounded, new Color(0.92f, 0.25f, 0.3f));
        Sliced(claimBadge, 0.3f);
        Place(claimBadge.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-10f, 6f), new Vector2(124f, 48f));
        var claimText = Txt("Label", claimBadge.rectTransform, "보상 0", labelFont, 24f, Color.white);
        Stretch(claimText.rectTransform);

        // 편성 버튼 (예시의 '친구랑 하기' 자리)
        var party = Img("PartyButton", overlay, rounded, new Color(0.29f, 0.24f, 0.48f, 0.96f));
        Sliced(party, 0.3f);
        party.raycastTarget = true;
        Place(party.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, BottomTabHeight + 205f), new Vector2(520f, 100f));
        var partyButton = party.gameObject.AddComponent<Button>();
        var portraitMask = Img("Portrait", party.rectTransform, circle, Color.white);
        portraitMask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        Place(portraitMask.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(60f, 0f), new Vector2(80f, 80f));
        var portrait = Img("Face", portraitMask.rectTransform, Load(ChiefPortraitPath), Color.white);
        portrait.preserveAspect = true;
        Place(portrait.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -14f), new Vector2(120f, 120f));
        var partyTitle = Txt("Title", party.rectTransform, "편성", labelFont, 36f, Color.white);
        partyTitle.alignment = TextAlignmentOptions.MidlineLeft;
        Place(partyTitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(120f, 14f), new Vector2(360f, 44f));
        var partySub = Txt("Sub", party.rectTransform, "드론 덱 · 족장 알팡", labelFont, 24f, new Color(0.8f, 0.75f, 1f));
        partySub.alignment = TextAlignmentOptions.MidlineLeft;
        Place(partySub.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(120f, -22f), new Vector2(360f, 34f));

        // 게임 시작 (스태미나 비용 표시)
        var startShadow = Img("StartShadow", overlay, rounded, new Color(0.72f, 0.48f, 0.1f));
        Sliced(startShadow, 0.25f);
        Place(startShadow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, BottomTabHeight + 30f), new Vector2(640f, 150f));
        var start = Img("StartButton", overlay, rounded, new Color(1f, 0.78f, 0.24f));
        Sliced(start, 0.25f);
        start.raycastTarget = true;
        Place(start.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, BottomTabHeight + 40f), new Vector2(640f, 150f));
        var startButton = start.gameObject.AddComponent<Button>();
        var startLabel = Txt("Label", start.rectTransform, "게임 시작", labelFont, 56f, new Color(0.23f, 0.16f, 0.07f));
        Place(startLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(600f, 70f));
        var chip = Img("StaminaChip", start.rectTransform, rounded, new Color(0.23f, 0.16f, 0.07f, 0.88f));
        Sliced(chip, 0.3f);
        Place(chip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(130f, 44f));
        var chipIcon = Img("Icon", chip.rectTransform, staminaIcon.sprite, Color.white);
        chipIcon.preserveAspect = true;
        Place(chipIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-28f, 0f), new Vector2(36f, 36f));
        var cost = Txt("Cost", chip.rectTransform, "5", numberFont, 32f, Color.white);
        Place(cost.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(18f, 0f), new Vector2(60f, 40f));

        // 측면 아이콘: 우편(현재 시스템 있음) + 후보(출석·퀘스트, 시스템 없음)
        var mail = SideIcon("Mail", overlay, Load(LobbyUiRoot + "mailbox.png"), new Vector2(0f, 1f), new Vector2(28f, -TopHudHeight - 26f), circle, labelFont, "3");
        var attendance = SideIcon("Candidate_Attendance", overlay, Load(LobbyUiRoot + "Attendance_Check_icon.png"), new Vector2(0f, 1f), new Vector2(28f, -TopHudHeight - 196f), circle, labelFont, null);
        var quest = SideIcon("Candidate_Quest", overlay, Load(LobbyUiRoot + "Quest_icon.png"), new Vector2(1f, 1f), new Vector2(-28f, -TopHudHeight - 26f), circle, labelFont, null);
        mail.SetActive(true);

        // ── 획득 연출 캔버스 ──
        var fxCanvas = OverlayCanvas("BattleLabFx", 200, false);
        var fxLayer = Rect("FxLayer", fxCanvas.transform);
        Stretch(fxLayer);

        // ── 실험 버튼 캔버스 ──
        var labCanvas = OverlayCanvas("BattleLabControls", 300, true);
        var lab = root.gameObject.AddComponent<LobbyBattleLab>();
        var toggle = LabButton(labCanvas.transform, "LAB", labelFont, rounded, new Vector2(1f, 0.5f), new Vector2(-12f, 360f), new Vector2(110f, 76f));
        UnityEventTools.AddPersistentListener(toggle.onClick, new UnityAction(lab.ToggleLabPanel));
        var panel = Img("LabPanel", labCanvas.transform, rounded, new Color(0.05f, 0.04f, 0.09f, 0.9f));
        Sliced(panel, 0.3f);
        Place(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-132f, 200f), new Vector2(360f, 640f));
        panel.gameObject.SetActive(false);
        var entries = new List<(string label, Action<Button> wire)>
        {
            ("최고 0", b => UnityEventTools.AddIntPersistentListener(b.onClick, new UnityAction<int>(lab.SetBestRound), 0)),
            ("최고 8", b => UnityEventTools.AddIntPersistentListener(b.onClick, new UnityAction<int>(lab.SetBestRound), 8)),
            ("최고 23", b => UnityEventTools.AddIntPersistentListener(b.onClick, new UnityAction<int>(lab.SetBestRound), 23)),
            ("최고 57", b => UnityEventTools.AddIntPersistentListener(b.onClick, new UnityAction<int>(lab.SetBestRound), 57)),
            ("런 끝 +12", b => UnityEventTools.AddIntPersistentListener(b.onClick, new UnityAction<int>(lab.AdvanceBy), 12)),
            ("런 끝 +25", b => UnityEventTools.AddIntPersistentListener(b.onClick, new UnityAction<int>(lab.AdvanceBy), 25)),
            ("보상 초기화", b => UnityEventTools.AddPersistentListener(b.onClick, new UnityAction(lab.ResetClaims))),
            ("스태미나 전환", b => UnityEventTools.AddPersistentListener(b.onClick, new UnityAction(lab.ToggleStaminaEmpty))),
            ("후보 아이콘", b => UnityEventTools.AddPersistentListener(b.onClick, new UnityAction(lab.ToggleCandidates))),
        };
        for (int i = 0; i < entries.Count; i++)
        {
            var button = LabButton(panel.rectTransform, entries[i].label, labelFont, rounded, new Vector2(0.5f, 1f), new Vector2(0f, -46f - i * 66f), new Vector2(320f, 56f));
            entries[i].wire(button);
        }
        var toast = Img("StatusToast", labCanvas.transform, rounded, new Color(0.05f, 0.04f, 0.09f, 0.85f));
        Sliced(toast, 0.3f);
        Place(toast.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -TopHudHeight - 8f), new Vector2(760f, 64f));
        var toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.blocksRaycasts = false;
        var status = Txt("Status", toast.rectTransform, "", labelFont, 24f, new Color(1f, 1f, 1f, 0.9f));
        Stretch(status.rectTransform);

        var so = new SerializedObject(lab);
        Set(so, "_scroll", scroll);
        Set(so, "_content", content);
        Set(so, "_labelFont", labelFont);
        Set(so, "_numberFont", numberFont);
        Set(so, "_roundedSprite", rounded);
        Set(so, "_circleSprite", circle);
        Set(so, "_chiefSprite", Load(ChiefPath));
        var bosses = so.FindProperty("_bossSprites");
        bosses.arraySize = BossPaths.Length;
        for (int i = 0; i < BossPaths.Length; i++) bosses.GetArrayElementAtIndex(i).objectReferenceValue = Load(BossPaths[i]);
        Set(so, "_goldIcon", goldIcon.sprite);
        Set(so, "_diamondIcon", diamondIcon.sprite);
        Set(so, "_bestValue", bestValue);
        Set(so, "_bestButton", bestButton);
        Set(so, "_claimBadge", claimBadge.gameObject);
        Set(so, "_claimBadgeText", claimText);
        Set(so, "_startButton", startButton);
        Set(so, "_staminaCostText", cost);
        Set(so, "_partyButton", partyButton);
        var candidates = so.FindProperty("_candidateIcons");
        candidates.arraySize = 2;
        candidates.GetArrayElementAtIndex(0).objectReferenceValue = attendance;
        candidates.GetArrayElementAtIndex(1).objectReferenceValue = quest;
        Set(so, "_hudStaminaText", staminaText);
        Set(so, "_fxLayer", fxLayer);
        Set(so, "_goldTarget", goldIcon.rectTransform);
        Set(so, "_diamondTarget", diamondIcon.rectTransform);
        Set(so, "_currencyATarget", controller.panelButtons[1]._mainRect);
        Set(so, "_currencyBTarget", controller.panelButtons[3]._mainRect);
        Set(so, "_labStatus", status);
        Set(so, "_labStatusGroup", toastGroup);
        Set(so, "_labPanel", panel.gameObject);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[LobbyBattleLab] built {ScenePath}");
        return ScenePath;
    }

    // ── 복사본에서 앱 동작 제거 (하단 탭 이동만 남김) ──
    private static void StripApplication(UnityEngine.SceneManagement.Scene scene, BottomPanelController keep)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
                    PrefabUtility.UnpackPrefabInstance(t.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (var root in scene.GetRootGameObjects())
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour == keep) continue;
                string ns = behaviour.GetType().Namespace ?? "";
                if (ns.StartsWith("UnityEngine.UI") || ns.StartsWith("TMPro") || ns.StartsWith("UnityEngine.EventSystems")
                    || ns.StartsWith("UnityEngine.InputSystem") || ns.StartsWith("UnityEngine.Rendering")) continue;
                Object.DestroyImmediate(behaviour);
            }
        foreach (var root in scene.GetRootGameObjects())
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                    UnityEventTools.RemovePersistentListener(button.onClick, i);
    }

    // ── 부품 ──
    private static Transform Find(UnityEngine.SceneManagement.Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
        throw new InvalidOperationException($"{name} not found in lobby copy.");
    }

    private static Sprite Load(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) Debug.LogWarning($"[LobbyBattleLab] sprite missing: {path}");
        return sprite;
    }

    private static GameObject SideIcon(string name, RectTransform parent, Sprite sprite, Vector2 corner, Vector2 position, Sprite circle, TMP_FontAsset font, string badge)
    {
        var icon = Img(name, parent, sprite, Color.white);
        icon.preserveAspect = true;
        icon.raycastTarget = true;
        Place(icon.rectTransform, corner, corner, position, new Vector2(150f, 150f));
        icon.gameObject.AddComponent<Button>();
        if (badge != null)
        {
            var dot = Img("Badge", icon.rectTransform, circle, new Color(0.92f, 0.25f, 0.3f));
            Place(dot.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-14f, -14f), new Vector2(44f, 44f));
            var label = Txt("Count", dot.rectTransform, badge, font, 26f, Color.white);
            Stretch(label.rectTransform);
        }
        return icon.gameObject;
    }

    private static Canvas OverlayCanvas(string name, int order, bool raycast)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0f;
        if (raycast) go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static Button LabButton(Transform parent, string label, TMP_FontAsset font, Sprite sprite, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var image = Img($"Lab_{label}", parent, sprite, new Color(0.3f, 0.26f, 0.42f, 0.95f));
        Sliced(image, 0.3f);
        image.raycastTarget = true;
        var pivot = new Vector2(anchor.x, anchor.y == 1f ? 1f : 0.5f);
        Place(image.rectTransform, anchor, pivot, position, size);
        var text = Txt("Label", image.rectTransform, label, font, 26f, Color.white);
        Stretch(text.rectTransform);
        return image.gameObject.AddComponent<Button>();
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image Img(string name, Transform parent, Sprite sprite, Color color)
    {
        var rect = Rect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI Txt(string name, Transform parent, string text, TMP_FontAsset font, float size, Color color)
    {
        var rect = Rect(name, parent);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    private static void Sliced(Image image, float cornerScale)
    {
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = cornerScale;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Set(SerializedObject so, string field, Object value)
    {
        var property = so.FindProperty(field);
        if (property == null) throw new InvalidOperationException($"field {field} missing");
        property.objectReferenceValue = value;
    }
}
