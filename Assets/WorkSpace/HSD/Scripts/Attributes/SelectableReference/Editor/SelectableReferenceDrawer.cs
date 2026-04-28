using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
[CustomPropertyDrawer(typeof(SelectableReferenceAttribute))]
public class SelectableReferenceDrawer : PropertyDrawer
{
    private const string PREFS_KEY = "SelectableReference_BackupCache";
    static Dictionary<string, string> _backupCache = new Dictionary<string, string>();

    static SelectableReferenceDrawer()
    {
        LoadFromPrefs();
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect labelRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        string fullTypeName = property.managedReferenceFullTypename;
        string typeName = string.IsNullOrEmpty(fullTypeName) ? "None (Empty)" : fullTypeName.Split(' ').Last().Split('.').Last();

        Rect buttonRect = EditorGUI.PrefixLabel(labelRect, label);
        if (EditorGUI.DropdownButton(buttonRect, new GUIContent(typeName), FocusType.Passive))
        {
            ShowTypeSelectionMenu(property);
        }

        if (!string.IsNullOrEmpty(fullTypeName))
        {
            Rect contentRect = new Rect(position.x, position.y, position.width, position.height);
            EditorGUI.PropertyField(contentRect, property, GUIContent.none, true);
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, true);
    }

    private void ShowTypeSelectionMenu(SerializedProperty property)
    {
        GenericMenu menu = new GenericMenu();
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(property.serializedObject.targetObject));
        string path = property.propertyPath;

        menu.AddItem(new GUIContent("None"), string.IsNullOrEmpty(property.managedReferenceFullTypename), () =>
        {
            if (property.managedReferenceValue != null)
            {
                Type currentType = property.managedReferenceValue.GetType();
                string key = $"{guid}_{path}_{currentType.FullName}";
                _backupCache[key] = EditorJsonUtility.ToJson(property.managedReferenceValue);

                SaveToPrefs();
            }

            property.managedReferenceValue = null;
            property.serializedObject.ApplyModifiedProperties();
        });

        Type targetType = GetElementType(fieldInfo.FieldType);
        var types = TypeCache.GetTypesDerivedFrom(targetType)
            .Where(p => (p.IsClass || p.IsValueType) && !p.IsAbstract);

        foreach (var type in types)
        {
            menu.AddItem(new GUIContent(type.Name), false, () =>
            {
                property.serializedObject.Update();

                if (property.managedReferenceValue != null)
                {
                    Type currentType = property.managedReferenceValue.GetType();
                    string oldKey = $"{guid}_{path}_{currentType.FullName}";
                    _backupCache[oldKey] = EditorJsonUtility.ToJson(property.managedReferenceValue);

                    SaveToPrefs();
                }

                object newInstance = Activator.CreateInstance(type);

                string newKey = $"{guid}_{path}_{type.FullName}";
                if (_backupCache.TryGetValue(newKey, out string json))
                {
                    EditorJsonUtility.FromJsonOverwrite(json, newInstance);
                }

                Undo.RecordObject(property.serializedObject.targetObject, "Set Reference");
                property.managedReferenceValue = newInstance;
                property.serializedObject.ApplyModifiedProperties();
            });
        }
        menu.ShowAsContext();
    }

    private Type GetElementType(Type type)
    {
        if (type.IsArray)
            return type.GetElementType();

        if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
            return type.GetGenericArguments()[0];

        return type;
    }

    private static void SaveToPrefs()
    {
        BackupData data = new BackupData();
        foreach (var kvp in _backupCache)
        {
            data.keys.Add(kvp.Key);
            data.values.Add(kvp.Value);
        }
        string json = JsonUtility.ToJson(data);
        EditorPrefs.SetString(PREFS_KEY, json);
    }

    private static void LoadFromPrefs()
    {
        if (!EditorPrefs.HasKey(PREFS_KEY)) return;

        string json = EditorPrefs.GetString(PREFS_KEY);
        BackupData data = JsonUtility.FromJson<BackupData>(json);

        _backupCache.Clear();
        for (int i = 0; i < data.keys.Count; i++)
        {
            _backupCache[data.keys[i]] = data.values[i];
        }
    }
}

[Serializable]
public class BackupData
{
    public List<string> keys = new List<string>();
    public List<string> values = new List<string>();
}