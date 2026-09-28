using GaeGGUL.Tutorial;
using UnityEditor;
using UnityEngine;

/// <summary>Shows only fields relevant to the selected designer building block.</summary>
[CustomEditor(typeof(IngameTutorialLesson))]
public sealed class IngameTutorialLessonEditor : Editor
{
    /// <summary>Draws recipe-specific or configurable UI step settings.</summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Kind"));
        var kind = (IngameTutorialLesson.LessonKind)serializedObject.FindProperty("Kind").enumValueIndex;
        if (kind == IngameTutorialLesson.LessonKind.GameplayRecipe)
        {
            var gameplay = serializedObject.FindProperty("Gameplay");
            foreach (var field in new[] { "Stage", "Instruction", "delayBeforeExecute" })
                EditorGUILayout.PropertyField(gameplay.FindPropertyRelative(field));
        }
        else
        {
            foreach (var field in new[] { "Instruction", "StartWhen", "TargetKey", "DelaySeconds", "AllowWorldInput" })
                EditorGUILayout.PropertyField(serializedObject.FindProperty(field));
            if (kind != IngameTutorialLesson.LessonKind.WaitForSignal)
                foreach (var field in new[] { "PauseGameplay", "Dim", "ShowHand" })
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(field));
            else serializedObject.FindProperty("PauseGameplay").boolValue = false;
            if (serializedObject.FindProperty("StartWhen").enumValueIndex == (int)IngameTutorialLesson.StartCondition.Signal)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("StartSignal"));
            if (kind == IngameTutorialLesson.LessonKind.WaitForSignal)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("CompletionSignal"));
        }
        serializedObject.ApplyModifiedProperties();
        string error = ((IngameTutorialLesson)target).Validate();
        if (error != null) EditorGUILayout.HelpBox(error, MessageType.Error);
        EditorGUILayout.HelpBox("Sequence의 Lessons 목록에 이 SO를 넣고 순서를 조립하세요. 전투 블록에는 선행 조건이 있습니다. README를 확인하세요.", MessageType.Info);
    }
}
