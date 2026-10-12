using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Copies production presentation into TutorialScene while retaining its authored lesson setup.</summary>
public static class TutorialPresentationSync
{
    [MenuItem("Tools/USW/Tutorial/Sync Ingame Presentation")]
    public static void Apply()
    {
        var target = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || target.isDirty ||
            target.path != "Assets/WorkSpace/USW/Tutorial/Scenes/TutorialScene.unity")
            throw new InvalidOperationException("Open the saved TutorialScene outside Play Mode first.");
        var source = EditorSceneManager.OpenPreviewScene("Assets/Scenes/IngameScene.unity");
        try
        {
            Transform Find(Scene scene, string path) => scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => Path(t) == path);
            UnityEngine.Object Remap(UnityEngine.Object value)
            {
                if (value is Component c && c.gameObject.scene == source)
                    return Find(target, Path(c.transform))?.GetComponent(c.GetType());
                if (value is GameObject g && g.scene == source) return Find(target, Path(g.transform))?.gameObject;
                return value;
            }
            void EnsureObject(string path)
            {
                if (Find(target, path) != null) return;
                var original = Find(source, path);
                if (original == null) throw new InvalidOperationException("Missing production object: " + path);
                var copy = UnityEngine.Object.Instantiate(original.gameObject);
                copy.name = original.name;
                SceneManager.MoveGameObjectToScene(copy, target);
                if (original.parent != null) copy.transform.SetParent(Find(target, Path(original.parent)), false);
                foreach (var component in copy.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;
                    var so = new SerializedObject(component);
                    var property = so.GetIterator();
                    while (property.NextVisible(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference)
                            property.objectReferenceValue = Remap(property.objectReferenceValue);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            void CopyFields<T>(string path, params string[] names) where T : Component
            {
                var from = new SerializedObject(Find(source, path).GetComponent<T>());
                var to = new SerializedObject(Find(target, path).GetComponent<T>());
                foreach (var name in names)
                {
                    var property = from.FindProperty(name);
                    to.CopyFromSerializedProperty(property);
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                        to.FindProperty(name).objectReferenceValue = Remap(property.objectReferenceValue);
                }
                to.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.IsPartOfPrefabInstance(to.targetObject))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(to.targetObject);
            }
            SyncHudLayout(source, target);
            EnsureObject("BossDamageNumbersCanvas");
            EnsureObject("# MainUI/CenterToast");
            CopyFields<BossDamageNumbers>("BossDamageNumbersCanvas", "_settings", "_cookieSettings", "_fontIndex");
            CopyFields<InGameInstaller>("/// InGameInstaller", "_centerToast", "_bossClearPresentation");
            CopyFields<UIManager>("@ Managers/UIManager", "_resultScreen");
            CopyFields<WaveManager>("@ Managers/WaveManager", "_penaltyFx");
            CopyFields<UnityEngine.UI.Image>("# MainUI/MainUI_SafeAreaRoot/MainUI_InteractableBar/Buttons/Summon/SummonButton/Summon_MaskBackground/Summon_Background (1)", "m_Material");
            var level = new SerializedObject(Find(target, "@ Managers/LevelUpManager").GetComponent<LevelUpManager>());
            var game = new SerializedObject(Find(target, "@ Managers/GameManager").GetComponent<GameManager>());
            level.FindProperty("_gameConfig").objectReferenceValue = game.FindProperty("config").objectReferenceValue;
            level.ApplyModifiedPropertiesWithoutUndo();
            var timerIcon = Find(target, "# MainUI/MainUI_SafeAreaRoot/MainUI_TopBar/Main_Topbar_TimerPanel/Main_Topbar_TimerIcon");
            if (timerIcon != null) timerIcon.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(target);
            EditorSceneManager.SaveScene(target);
        }
        finally { EditorSceneManager.ClosePreviewScene(source); }
    }

    /// <summary>Synchronizes shared HUD geometry without copying tutorial visibility or gameplay wiring.</summary>
    public static void SyncHudLayout(Scene source, Scene target)
    {
        const string top = "# MainUI/MainUI_SafeAreaRoot/MainUI_TopBar";
        const string buttons = "# MainUI/MainUI_SafeAreaRoot/MainUI_InteractableBar/Buttons";
        const string boss = top + "/Main_TopBar_BossInfomation/";
        var originals = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RectTransform>(true));
        var copies = target.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RectTransform>(true)).ToArray();
        foreach (var original in originals)
        {
            string path = Path(original);
            if (path != top && !path.StartsWith(top + "/", StringComparison.Ordinal) &&
                path != buttons && !path.StartsWith(buttons + "/", StringComparison.Ordinal)) continue;
            // Keep the tutorial's icon coverage, artwork clipping and line-count placement.
            if (path == boss + "Boss_Icon" || path == boss + "BossHpBar" ||
                path.StartsWith(boss + "BossHpBar/", StringComparison.Ordinal)) continue;
            var copy = copies.FirstOrDefault(t => Path(t) == path);
            if (copy == null) continue;
            copy.anchorMin = original.anchorMin; copy.anchorMax = original.anchorMax;
            copy.pivot = original.pivot; copy.sizeDelta = original.sizeDelta;
            copy.anchoredPosition3D = original.anchoredPosition3D;
            copy.localScale = original.localScale; copy.localRotation = original.localRotation;
            if (PrefabUtility.IsPartOfPrefabInstance(copy)) PrefabUtility.RecordPrefabInstancePropertyModifications(copy);
        }
        // Keep a visible gap below the large sortie button, including its frame outline.
        var summon = copies.FirstOrDefault(t => Path(t) == buttons + "/Summon");
        if (summon != null)
        {
            summon.anchoredPosition += Vector2.up * 32f;
            summon.sizeDelta = new Vector2(summon.sizeDelta.x, 230f);
            if (PrefabUtility.IsPartOfPrefabInstance(summon)) PrefabUtility.RecordPrefabInstancePropertyModifications(summon);
        }
        var food = copies.FirstOrDefault(t => Path(t) == "# MainUI/MainUI_SafeAreaRoot/MainUI_InteractableBar/MainUI_InteractableBar_Food");
        if (food != null && summon != null)
        {
            food.anchoredPosition = new Vector2(food.anchoredPosition.x, summon.anchoredPosition.y + summon.sizeDelta.y * 0.5f + 42f);
            if (PrefabUtility.IsPartOfPrefabInstance(food)) PrefabUtility.RecordPrefabInstancePropertyModifications(food);
        }
        // Geometry alone does not copy TMP typography. Keep the complete authored timer style.
        const string timerPath = top + "/Main_Topbar_TimerPanel/Main_Topbar_TimerText(TMP)";
        var fromTimer = originals.First(t => Path(t) == timerPath).GetComponent<TMPro.TextMeshProUGUI>();
        var toTimer = copies.First(t => Path(t) == timerPath).GetComponent<TMPro.TextMeshProUGUI>();
        var fromStyle = new SerializedObject(fromTimer);
        var toStyle = new SerializedObject(toTimer);
        foreach (var field in new[] { "m_fontAsset", "m_sharedMaterial", "m_fontSize", "m_fontSizeBase", "m_enableAutoSizing", "m_fontSizeMin", "m_fontSizeMax", "m_fontStyle", "m_fontColor", "m_HorizontalAlignment", "m_VerticalAlignment", "m_characterSpacing", "m_wordSpacing", "m_lineSpacing", "m_paragraphSpacing", "m_margin" })
        {
            var property = fromStyle.FindProperty(field);
            if (property != null) toStyle.CopyFromSerializedProperty(property);
        }
        toStyle.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(toTimer)) PrefabUtility.RecordPrefabInstancePropertyModifications(toTimer);
        var menu = copies.FirstOrDefault(t => t.GetComponent<IngameHudMenu>() != null);
        if (menu != null)
        {
            menu.gameObject.SetActive(false);
            if (PrefabUtility.IsPartOfPrefabInstance(menu.gameObject)) PrefabUtility.RecordPrefabInstancePropertyModifications(menu.gameObject);
        }
    }

    private static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
}
