using System;
using UnityEditor;
using UnityEngine;

/// <summary>Authors composition cards and keeps archived fields out of the normal Inspector.</summary>
[CustomEditor(typeof(LevelUpData)), CanEditMultipleObjects]
public sealed class LevelUpDataEditor : Editor
{
    /// <summary>Creates a new composition card through the existing asset menu.</summary>
    [MenuItem("Assets/Create/Game/LevelUpData")]
    public static void CreateCard()
    {
        var card = CreateInstance<LevelUpData>();
        card.EffectSchemaVersion = 1;
        ProjectWindowUtil.CreateAsset(card, "LevelUpData.asset");
    }

    /// <summary>Draws editable composition and reports invalid or unmigrated definitions.</summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("EffectSchemaVersion"));
        DrawPropertiesExcluding(serializedObject, "m_Script", "EffectSchemaVersion");
        serializedObject.ApplyModifiedProperties();
        foreach (UnityEngine.Object item in targets)
        {
            try { SelectionDefinitionReader.CopyDefinition((LevelUpData)item); }
            catch (ArgumentException exception) { EditorGUILayout.HelpBox(exception.Message, MessageType.Error); }
        }
    }
}
