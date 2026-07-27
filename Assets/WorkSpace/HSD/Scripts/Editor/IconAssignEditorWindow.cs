using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>ScriptableObject 스크립트에 커스텀 아이콘을 지정하는 에디터.
/// Gizmos 폴더 자동인식이 적용되지 않는 환경에서 MonoImporter.SetIcon으로 직접 지정한다.</summary>
public class IconAssignEditorWindow : EditorWindow
{
    [Serializable]
    private class IconEntry
    {
        public MonoScript Script;
        public Texture2D Icon;
    }

    private const string GizmosDir = "Assets/WorkSpace/HSD/Gizmos";
    private readonly List<IconEntry> _entries = new List<IconEntry>();

    [MenuItem("Tools/GGD_Editor/Icon Assigner", false, 0)]
    public static void Open()
    {
        GetWindow<IconAssignEditorWindow>("Icon Assigner");
    }

    private void OnEnable()
    {
        if (_entries.Count > 0) return;

        AddDefaultEntry("SkillData");
        AddDefaultEntry("BuffData");
        AddDefaultEntry("UnitData");
    }

    private void AddDefaultEntry(string className)
    {
        var script = MonoImporter.GetAllRuntimeMonoScripts()
            .FirstOrDefault(s => s.GetClass()?.Name == className);
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GizmosDir}/{className}.png");
        _entries.Add(new IconEntry { Script = script, Icon = icon });
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("스크립트에 커스텀 아이콘을 지정합니다.", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        for (int i = 0; i < _entries.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            _entries[i].Script = (MonoScript)EditorGUILayout.ObjectField(_entries[i].Script, typeof(MonoScript), false);
            _entries[i].Icon = (Texture2D)EditorGUILayout.ObjectField(_entries[i].Icon, typeof(Texture2D), false, GUILayout.Width(150));
            if (GUILayout.Button("X", GUILayout.Width(24)))
            {
                _entries.RemoveAt(i);
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("+ 항목 추가"))
        {
            _entries.Add(new IconEntry());
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("아이콘 적용", GUILayout.Height(30)))
        {
            ApplyAll();
        }
    }

    private void ApplyAll()
    {
        int count = 0;
        foreach (var entry in _entries)
        {
            if (entry.Script == null || entry.Icon == null) continue;

            string scriptPath = AssetDatabase.GetAssetPath(entry.Script);
            var importer = (MonoImporter)AssetImporter.GetAtPath(scriptPath);
            if (importer == null) continue;

            importer.SetIcon(entry.Icon);
            importer.SaveAndReimport();
            count++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[IconAssignEditorWindow] {count}개 스크립트에 아이콘을 적용했습니다.");
    }
}
