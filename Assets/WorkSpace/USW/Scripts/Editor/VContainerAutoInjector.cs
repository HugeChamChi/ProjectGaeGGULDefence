using UnityEngine;
using UnityEditor;
using VContainer;
using VContainer.Unity;
using System.Reflection;
using System.Collections.Generic;

public class VContainerAutoInjector : EditorWindow
{
    [MenuItem("Tools/VContainer_AutoInject/Inject 자동 스캔 및 등록")]
    public static void AutoAssign()
    {
        // 1. 씬에 있는 LifetimeScope 찾기
        var scope = FindFirstObjectByType<LifetimeScope>(FindObjectsInactive.Include);
        if (scope == null)
        {
            Debug.LogError("[VContainer] 씬에 LifetimeScope가 없습니다.");
            return;
        }

        // 2. 씬에 있는 모든 MonoBehaviour 찾기 (비활성화된 오브젝트 포함)
        var allMonoBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var injectGameObjects = new HashSet<GameObject>();

        foreach (var mono in allMonoBehaviours)
        {
            if (mono == null) continue;
            // LifetimeScope 본인이나 VContainer 내부 클래스는 무시
            if (mono is LifetimeScope || mono.GetType().Namespace?.StartsWith("VContainer") == true) continue;

            bool hasInject = false;
            var type = mono.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

            while (type != null && type != typeof(MonoBehaviour) && type != typeof(Behaviour) && type != typeof(Component) && type != typeof(UnityEngine.Object))
            {
                foreach (var field in type.GetFields(flags))
                {
                    if (field.GetCustomAttribute<InjectAttribute>() != null) { hasInject = true; break; }
                }

                if (!hasInject)
                {
                    foreach (var prop in type.GetProperties(flags))
                    {
                        if (prop.GetCustomAttribute<InjectAttribute>() != null) { hasInject = true; break; }
                    }
                }

                if (!hasInject)
                {
                    foreach (var method in type.GetMethods(flags))
                    {
                        if (method.GetCustomAttribute<InjectAttribute>() != null) { hasInject = true; break; }
                    }
                }
                
                if (hasInject) break;
                type = type.BaseType;
            }

            // 해당 컴포넌트가 Inject를 쓴다면 게임오브젝트를 수집
            if (hasInject)
            {
                injectGameObjects.Add(mono.gameObject);
            }
        }

        // 3. LifetimeScope의 autoInjectGameObjects 리스트 갱신 (Undo 지원)
        Undo.RecordObject(scope, "Auto Assign VContainer Inject GameObjects");

        SerializedObject so = new SerializedObject(scope);
        SerializedProperty propList = so.FindProperty("autoInjectGameObjects");
        
        propList.ClearArray();
        int idx = 0;
        foreach (var go in injectGameObjects)
        {
            propList.InsertArrayElementAtIndex(idx);
            propList.GetArrayElementAtIndex(idx).objectReferenceValue = go;
            idx++;
        }
        
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(scope);
        
        Debug.Log($"[VContainer 딸깍 완료!] 씬을 스캔하여 총 {injectGameObjects.Count}개의 오브젝트를 LifetimeScope에 자동 등록했습니다! 🎉");
    }
}
