using GaeGGUL.Tutorial;
using UnityEditor;
using UnityEngine;

/// <summary>Validates designer recipe ordering directly in the sequence Inspector.</summary>
[CustomEditor(typeof(IngameTutorialPlan))]
public sealed class IngameTutorialPlanEditor : Editor
{
    /// <summary>Draws the reorderable asset list and actionable authoring diagnostics.</summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var error = ((IngameTutorialPlan)target).Validate();
        EditorGUILayout.HelpBox(error ?? "구성 검증 통과. 대상 Key 연결은 TutorialScene의 Binding에서 확인하세요.",
            error == null ? MessageType.Info : MessageType.Error);
    }
}
