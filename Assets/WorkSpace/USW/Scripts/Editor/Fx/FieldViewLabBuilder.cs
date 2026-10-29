using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Rebuilds only the field-view FxLab from the current authored game scene.</summary>
public static class FieldViewLabBuilder
{
    /// <summary>Scene authored exclusively for the field-view comparison.</summary>
    public const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_FieldView.unity";

    /// <summary>Creates or refreshes the standalone field-view lab controls.</summary>
    [MenuItem("Tools/USW/Fx/Build Field View Lab")]
    public static void Build() => Run();

    /// <summary>Automation entry; copies the game scene only on first creation.</summary>
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before rebuilding the lab.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("An open scene has unsaved changes; preserve it before rebuilding.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null && !AssetDatabase.CopyAsset("Assets/Scenes/IngameScene.unity", ScenePath))
            throw new InvalidOperationException("Could not copy the game scene.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var choices = UnityEngine.Object.FindFirstObjectByType<LevelUpUI>(FindObjectsInactive.Include);
        var source = new SerializedObject(choices);
        var panel = (GameObject)source.FindProperty("obj").objectReferenceValue;
        var group = panel.GetComponent<CanvasGroup>() ?? panel.AddComponent<CanvasGroup>();
        RectTransform lowerHud = null;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var candidate in root.GetComponentsInChildren<RectTransform>(true))
                if (candidate.name == "CombatHudPager") lowerHud = candidate;
        // Some scene revisions construct the pager at runtime; the lab binds it after boot.
        var lowerHudGroup = lowerHud != null ? lowerHud.GetComponent<CanvasGroup>() : null;
        if (lowerHud != null && lowerHudGroup == null) lowerHudGroup = lowerHud.gameObject.AddComponent<CanvasGroup>();
        var hint = panel.transform.Find("LevelUp_DeemTMP");
        if (hint != null) hint.gameObject.SetActive(false);
        foreach (var old in scene.GetRootGameObjects())
            if (old.name == "Field View Lab Controls" || old.name == "Field View Lab Driver") UnityEngine.Object.DestroyImmediate(old);
        var controls = new GameObject("Field View Lab Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = controls.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;
        var scaler = controls.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0;
        var shield = new GameObject("Field Input Shield", typeof(RectTransform), typeof(Image));
        shield.transform.SetParent(controls.transform, false);
        var shieldRt = (RectTransform)shield.transform;
        shieldRt.anchorMin = Vector2.zero; shieldRt.anchorMax = Vector2.one;
        shieldRt.sizeDelta = Vector2.zero;
        shield.GetComponent<Image>().color = Color.clear;
        var button = new GameObject("Field View Toggle", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
        button.transform.SetParent(controls.transform, false);
        var rt = (RectTransform)button.transform;
        rt.sizeDelta = new Vector2(600, 120);
        // Give the return action its own footer after the normal combat buttons tuck away.
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0, 210);
        var image = button.GetComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/WorkSpace/USW/Data/UI/BottomHudButtonSprite.asset");
        image.type = Image.Type.Sliced;
        image.color = new Color(0.70f, 0.80f, 0.89f, 1);
        var colors = button.colors;
        colors.pressedColor = new Color(0.75f, 0.82f, 0.90f);
        colors.highlightedColor = Color.white;
        colors.disabledColor = new Color(0.85f, 0.89f, 0.95f, 1);
        button.colors = colors;
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.rectTransform.SetParent(rt, false);
        label.rectTransform.sizeDelta = new Vector2(420, 86);
        label.rectTransform.anchoredPosition = new Vector2(25, 2);
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Imports/Font/NotoSansKR-Bold SDF.asset");
        label.fontSize = 36;
        label.color = new Color(0.98f, 0.96f, 0.89f);
        label.alignment = TextAlignmentOptions.Center;
        label.text = "필드 보기";
        label.raycastTarget = false;
        var eye = new GameObject("Eye", typeof(RectTransform), typeof(CanvasRenderer), typeof(FieldViewEye)).GetComponent<FieldViewEye>();
        eye.rectTransform.SetParent(rt, false);
        eye.rectTransform.sizeDelta = new Vector2(42, 42);
        eye.rectTransform.anchoredPosition = new Vector2(-194, 2);
        eye.color = label.color;
        eye.raycastTarget = false;
        var driver = new GameObject("Field View Lab Driver").AddComponent<FieldViewFxLab>();
        var so = new SerializedObject(driver);
        so.FindProperty("_choices").objectReferenceValue = choices;
        so.FindProperty("_panel").objectReferenceValue = group;
        so.FindProperty("_button").objectReferenceValue = button;
        so.FindProperty("_label").objectReferenceValue = label;
        so.FindProperty("_shield").objectReferenceValue = shield;
        so.FindProperty("_controls").objectReferenceValue = controls;
        so.FindProperty("_lowerHud").objectReferenceValue = lowerHud;
        so.FindProperty("_lowerHudGroup").objectReferenceValue = lowerHudGroup;
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(button.onClick, driver.Toggle);
        EditorSceneManager.SaveScene(scene);
        return ScenePath;
    }
}
