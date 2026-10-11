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
            EnsureObject("BossDamageNumbersCanvas");
            EnsureObject("# MainUI/CenterToast");
            CopyFields<BossDamageNumbers>("BossDamageNumbersCanvas", "_settings", "_cookieSettings", "_fontIndex");
            CopyFields<InGameInstaller>("/// InGameInstaller", "_centerToast", "_bossClearPresentation");
            CopyFields<UIManager>("@ Managers/UIManager", "_resultScreen");
            CopyFields<WaveManager>("@ Managers/WaveManager", "_penaltyFx");
            CopyFields<TMPro.TextMeshProUGUI>("# MainUI/MainUI_SafeAreaRoot/MainUI_TopBar/Main_Topbar_TimerPanel/Main_Topbar_TimerText(TMP)", "m_fontSize", "m_fontSizeBase");
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

    private static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
}
