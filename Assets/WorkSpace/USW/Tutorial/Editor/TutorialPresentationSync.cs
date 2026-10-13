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
            EnsureObject("# MainUI/MainUI_ModalFrame/CenterToast");
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
        SyncSafeFrames(source, target);
        const string top = "# MainUI/MainUI_SafeAreaRoot/MainUI_TopBar";
        const string bottom = "# MainUI/MainUI_SafeAreaRoot/MainUI_InteractableBar";
        const string buttons = bottom + "/Buttons";
        const string boss = top + "/Main_TopBar_BossInfomation/";
        var originals = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RectTransform>(true));
        var copies = target.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RectTransform>(true)).ToArray();
        foreach (var original in originals)
        {
            string path = Path(original);
            if (path != top && !path.StartsWith(top + "/", StringComparison.Ordinal) &&
                path != bottom && !path.StartsWith(bottom + "/", StringComparison.Ordinal)) continue;
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
            var fromGraphic = original.GetComponent<UnityEngine.UI.Graphic>();
            var toGraphic = copy.GetComponent<UnityEngine.UI.Graphic>();
            if (fromGraphic != null && toGraphic != null)
            {
                toGraphic.raycastPadding = fromGraphic.raycastPadding;
                if (PrefabUtility.IsPartOfPrefabInstance(toGraphic)) PrefabUtility.RecordPrefabInstancePropertyModifications(toGraphic);
            }
        }
        // Preserve tutorial target components while matching the production button artwork.
        var fromUpgrade = originals.First(t => Path(t) == buttons + "/Enchant/EnchantButton");
        var toUpgrade = copies.First(t => Path(t) == buttons + "/Enchant/EnchantButton");
        var artwork = fromUpgrade.Find("Artwork");
        if (artwork != null && toUpgrade.Find("Artwork") == null)
        {
            var clone = UnityEngine.Object.Instantiate(artwork.gameObject, toUpgrade, false);
            clone.name = "Artwork";
            clone.transform.SetAsFirstSibling();
        }
        var fromImage = fromUpgrade.GetComponent<UnityEngine.UI.Image>();
        var toImage = toUpgrade.GetComponent<UnityEngine.UI.Image>();
        toImage.sprite = fromImage.sprite;
        toImage.color = fromImage.color;
        toImage.type = fromImage.type;
        if (PrefabUtility.IsPartOfPrefabInstance(toImage)) PrefabUtility.RecordPrefabInstancePropertyModifications(toImage);
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

    private static void SyncSafeFrames(Scene source, Scene target)
    {
        Transform Find(Scene scene, string path) => scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => Path(t) == path);
        void Pose(RectTransform from, RectTransform to)
        {
            to.anchorMin = from.anchorMin; to.anchorMax = from.anchorMax; to.pivot = from.pivot;
            to.sizeDelta = from.sizeDelta; to.anchoredPosition3D = from.anchoredPosition3D;
            to.localScale = from.localScale; to.localRotation = from.localRotation;
            if (PrefabUtility.IsPartOfPrefabInstance(to)) PrefabUtility.RecordPrefabInstancePropertyModifications(to);
        }
        void Move(Transform item, Transform parent)
        {
            var rect = (RectTransform)item;
            var min = rect.anchorMin; var max = rect.anchorMax; var pivot = rect.pivot;
            var size = rect.sizeDelta; var pos = rect.anchoredPosition3D;
            var scale = rect.localScale; var rotation = rect.localRotation;
            item.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
            rect.sizeDelta = size; rect.anchoredPosition3D = pos;
            rect.localScale = scale; rect.localRotation = rotation;
        }
        RectTransform Frame(string path)
        {
            var from = (RectTransform)Find(source, path);
            if (from == null) throw new InvalidOperationException("Missing production safe frame: " + path);
            var to = Find(target, path) as RectTransform;
            if (to == null)
            {
                var go = new GameObject(from.name, typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(go, target);
                to = (RectTransform)go.transform;
                to.SetParent(Find(target, Path(from.parent)), false);
            }
            var old = to.GetComponent<SafeAreaApplier>();
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var frame = to.GetComponent<SafeAreaContentFrame>();
            if (frame == null) frame = to.gameObject.AddComponent<SafeAreaContentFrame>();
            EditorUtility.CopySerialized(from.GetComponent<SafeAreaContentFrame>(), frame);
            Pose(from, to);
            return to;
        }
        Frame("# MainUI/MainUI_SafeAreaRoot");
        var tutorialCanvas = Find(target, "IngameTutorial/TutorialCanvas");
        if (tutorialCanvas != null)
        {
            var tutorialFrame = tutorialCanvas.Find("TutorialSafeContentFrame") as RectTransform;
            if (tutorialFrame == null)
            {
                var go = new GameObject("TutorialSafeContentFrame", typeof(RectTransform), typeof(SafeAreaContentFrame));
                SceneManager.MoveGameObjectToScene(go, target);
                tutorialFrame = (RectTransform)go.transform;
                tutorialFrame.SetParent(tutorialCanvas, false);
            }
            var from = (RectTransform)Find(source, "# MainUI/MainUI_SafeAreaRoot");
            Pose(from, tutorialFrame);
            EditorUtility.CopySerialized(from.GetComponent<SafeAreaContentFrame>(), tutorialFrame.GetComponent<SafeAreaContentFrame>());
            var dialogue = tutorialCanvas.GetComponentInChildren<GaeGGUL.Tutorial.IngameTutorialDialogue>(true);
            if (dialogue != null && dialogue.transform.parent != tutorialFrame) Move(dialogue.transform, tutorialFrame);
            // Spotlight and input blocker remain direct children of the full-screen canvas.
        }
        var sub = Find(target, "/// SubUI");
        var subChildren = sub.Cast<Transform>().Where(t => t.name != "SubUI_ContentFrame").ToArray();
        var subFrame = Frame("/// SubUI/SubUI_ContentFrame");
        foreach (var child in subChildren) Move(child, subFrame);
        var modal = Frame("# MainUI/MainUI_ModalFrame");
        foreach (Transform original in Find(source, "# MainUI/MainUI_ModalFrame"))
        {
            var child = Find(target, "# MainUI/" + original.name);
            if (child != null) Move(child, modal);
        }
        // Dims cover the full display even when the panel content fits the safe frame.
        foreach (var original in source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FullScreenUiBackdrop>(true)))
        {
            var copy = Find(target, Path(original.transform));
            if (copy != null && copy.GetComponent<FullScreenUiBackdrop>() == null)
                copy.gameObject.AddComponent<FullScreenUiBackdrop>();
        }
        var fromBg = Find(source, "/// Map/bg");
        var toBg = Find(target, "/// Map/bg");
        if (fromBg != null && toBg != null)
        {
            toBg.localPosition = fromBg.localPosition; toBg.localScale = fromBg.localScale;
            toBg.localRotation = fromBg.localRotation;
            var fromSprite = fromBg.GetComponent<SpriteRenderer>();
            var toSprite = toBg.GetComponent<SpriteRenderer>();
            toSprite.sprite = fromSprite.sprite; toSprite.color = fromSprite.color;
            if (PrefabUtility.IsPartOfPrefabInstance(toSprite)) PrefabUtility.RecordPrefabInstancePropertyModifications(toSprite);
        }
        var fromCamera = Find(source, "Main Camera").GetComponent<BattleViewportFitter>();
        var targetCamera = Find(target, "Main Camera");
        if (fromCamera != null)
        {
            var fitter = targetCamera.GetComponent<BattleViewportFitter>();
            if (fitter == null) fitter = targetCamera.gameObject.AddComponent<BattleViewportFitter>();
            EditorUtility.CopySerialized(fromCamera, fitter);
            var so = new SerializedObject(fitter);
            foreach (var name in new[] { "_topBoundary", "_bottomBoundary" })
            {
                var boundary = (RectTransform)so.FindProperty(name).objectReferenceValue;
                so.FindProperty(name).objectReferenceValue = Find(target, Path(boundary)) as RectTransform;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
}
