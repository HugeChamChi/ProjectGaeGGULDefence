using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(KoreanLabelAttribute))]
public class KoreanLabelDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var korean = (KoreanLabelAttribute)attribute;
        EditorGUI.PropertyField(position, property, new GUIContent(korean.Label, label.tooltip), true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUI.GetPropertyHeight(property, label, true);
}
