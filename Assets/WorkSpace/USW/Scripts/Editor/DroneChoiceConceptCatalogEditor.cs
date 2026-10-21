using UnityEditor;

/// <summary>생성된 카탈로그를 Inspector에서 읽기 전용으로 표시한다.</summary>
[CustomEditor(typeof(DroneChoiceConceptCatalog))]
public sealed class DroneChoiceConceptCatalogEditor : Editor
{
    /// <summary>태그의 수동 Inspector 저작을 허용하지 않는다.</summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
        {
            var property = serializedObject.GetIterator();
            bool children = true;
            while (property.NextVisible(children))
            {
                EditorGUILayout.PropertyField(property, true); children = false;
            }
            foreach (string field in new[] { "_poolId", "_schemaVersion", "_dataVersion", "_sourceWorkbookSha256", "_contentHash", "_entries" })
                EditorGUILayout.PropertyField(serializedObject.FindProperty(field), true);
        }
    }
}
