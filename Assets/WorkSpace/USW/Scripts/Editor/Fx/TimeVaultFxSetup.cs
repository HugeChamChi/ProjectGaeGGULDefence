using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 비상 전력(9136) 유예 연출(TimeVaultFx, 시스템 오버라이드)을 IngameScene에 단다 (2026-11-05).
/// 전용 Screen Space Overlay 캔버스 + TimeVaultFx를 만들고 실제 HUD(타이머 패널·글자, 하단 버튼 묶음, SafeArea 루트)를 연결한다.
/// 하단 버튼 묶음의 CanvasGroup은 CombatHudPager가 넘김 알파로 쓰므로 버튼마다 CanvasGroup을 달아 잠금 대상으로 넣는다.
/// 다시 실행하면 이전에 단 캔버스를 지우고 새로 단다. InGameLifetimeScope의 선택 등록(RegisterOptional&lt;TimeVaultFx&gt;)이 주입한다.
/// </summary>
public static class TimeVaultFxSetup
{
    private const string IngameScenePath = "Assets/Scenes/IngameScene.unity";
    private const string SettingsPath = "Assets/WorkSpace/USW/Data/Fx/TimeVault/TimeVaultFxSettings.asset";
    private const string CanvasName = "TimeVaultFxCanvas";
    private const int SortingOrder = 50;   // MainUI(1)/SubUI(4)/CurrencyCanvas(2) 위, # Effects(100) 아래
    private static readonly Vector2 Reference = new Vector2(1080f, 1920f);

    /// <summary>IngameScene에 연출을 달고 저장한다.</summary>
    [MenuItem("Tools/USW/Fx/Apply Time Vault Fx To IngameScene")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit Mode에서 실행하세요.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("열린 씬에 저장하지 않은 변경이 있습니다. 먼저 보존해야 합니다.");

        var scene = EditorSceneManager.OpenScene(IngameScenePath, OpenSceneMode.Single);
        // 에셋은 씬을 연 뒤에 읽는다 — 먼저 읽으면 OpenScene이 그 에셋을 내려 빈 참조가 저장된다.
        var settings = AssetDatabase.LoadAssetAtPath<TimeVaultFxSettings>(SettingsPath);
        if (settings == null) throw new InvalidOperationException("설정 SO 없음: " + SettingsPath);

        foreach (var root in scene.GetRootGameObjects())
            if (root.name == CanvasName) UnityEngine.Object.DestroyImmediate(root);

        var safeRoot = Find<RectTransform>(scene, "MainUI_SafeAreaRoot");
        var timerPanel = Find<RectTransform>(scene, "Main_Topbar_TimerPanel");
        var timerText = Find<TMP_Text>(scene, "Main_Topbar_TimerText(TMP)");
        var buttons = safeRoot != null ? safeRoot.Find("MainUI_InteractableBar/Buttons") as RectTransform : null;
        if (safeRoot == null || timerPanel == null || timerText == null || buttons == null)
            throw new InvalidOperationException("IngameScene HUD(SafeArea 루트, 타이머 패널/글자, MainUI_InteractableBar/Buttons)를 찾지 못했습니다.");

        var go = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Reference; scaler.matchWidthOrHeight = 0.5f;
        SceneManager.MoveGameObjectToScene(go, scene);

        var fxGo = new GameObject("TimeVaultFx", typeof(RectTransform));
        var fxRect = (RectTransform)fxGo.transform;
        fxRect.SetParent(go.transform, false);
        fxRect.anchorMin = Vector2.zero; fxRect.anchorMax = Vector2.one;
        fxRect.offsetMin = Vector2.zero; fxRect.offsetMax = Vector2.zero;
        var fx = fxGo.AddComponent<TimeVaultFx>();

        var so = new SerializedObject(fx);
        so.FindProperty("_settings").objectReferenceValue = settings;
        var locks = so.FindProperty("_lockTargets");
        locks.arraySize = buttons.childCount;
        for (int i = 0; i < buttons.childCount; i++)
        {
            var child = buttons.GetChild(i).gameObject;
            var group = child.GetComponent<CanvasGroup>();
            if (group == null) group = Undo.AddComponent<CanvasGroup>(child);
            locks.GetArrayElementAtIndex(i).objectReferenceValue = group;
        }
        so.FindProperty("_denyArea").objectReferenceValue = buttons;
        so.FindProperty("_timerAnchor").objectReferenceValue = timerPanel;
        so.FindProperty("_timerText").objectReferenceValue = timerText;
        so.FindProperty("_shakeTarget").objectReferenceValue = safeRoot;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TimeVaultFxSetup] IngameScene에 TimeVaultFx 연결 (잠금 버튼 " + buttons.childCount + "개)");
    }

    private static T Find<T>(Scene scene, string name) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var c in root.GetComponentsInChildren<T>(true))
                if (c.name == name) return c;
        return null;
    }
}
