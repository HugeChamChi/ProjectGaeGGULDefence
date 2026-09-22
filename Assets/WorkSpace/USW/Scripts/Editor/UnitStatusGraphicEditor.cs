using UnityEditor;
using UnityEditor.UI;

/// <summary>프리팹 크기·배치·패시브 미리보기 옵션을 기본 Graphic 설정과 함께 표시합니다.</summary>
[CustomEditor(typeof(UnitStatusGraphic)), CanEditMultipleObjects]
public sealed class UnitStatusGraphicEditor : GraphicEditor
{
    /// <inheritdoc />
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        serializedObject.Update();
        EditorGUILayout.HelpBox("전체 크기: Rect Transform Width/Height 또는 Scale. 셀보다 커지면 안전 영역 안으로 축소됩니다. 게이지 색상: Material. Preview는 프리팹 편집 전용입니다.", MessageType.Info);
        foreach (string field in new[] { "_badgeSize", "_badgeGap", "_gaugeHeight", "_cellWidthFraction", "_cellBottomInset", "_frame", "_badge", "_legend", "_previewTier", "_previewProgress", "_previewPassive" })
            EditorGUILayout.PropertyField(serializedObject.FindProperty(field));
        serializedObject.ApplyModifiedProperties();
    }
}
