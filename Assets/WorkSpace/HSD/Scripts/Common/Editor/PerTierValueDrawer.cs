using System;
using UnityEditor;
using UnityEngine;

/// <summary>PerTierFloat/PerTierInt의 normal/rare/epic/legend 4필드를 등급별 색상 열로 나란히 그린다.</summary>
public static class PerTierValueDrawer
{
    private static readonly Color ColNormal = new Color(0.75f, 0.75f, 0.75f);
    private static readonly Color ColRare = new Color(0.30f, 0.55f, 0.95f);
    private static readonly Color ColEpic = new Color(0.65f, 0.35f, 0.90f);
    private static readonly Color ColLegend = new Color(0.95f, 0.65f, 0.15f);

    private static readonly (string label, string field, Color color)[] Tiers =
    {
        ("노말", "normal", ColNormal),
        ("레어", "rare", ColRare),
        ("에픽", "epic", ColEpic),
        ("전설", "legend", ColLegend),
    };

    public static bool IsPerTierType(Type type) => type == typeof(PerTierFloat) || type == typeof(PerTierInt);

    public static float GetFieldsHeight() => EditorGUIUtility.singleLineHeight * 2f + 2f;

    public static void DrawFields(Rect position, SerializedProperty property)
    {
        float lineHeight = EditorGUIUtility.singleLineHeight;
        const float gap = 2f;
        float colWidth = (position.width - gap * (Tiers.Length - 1)) / Tiers.Length;

        var headerStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
        };

        for (int i = 0; i < Tiers.Length; i++)
        {
            var (tierLabel, field, color) = Tiers[i];
            float x = position.x + i * (colWidth + gap);

            Rect headerRect = new Rect(x, position.y, colWidth, lineHeight);
            Rect fieldRect = new Rect(x, position.y + lineHeight + 1f, colWidth, lineHeight);

            EditorGUI.DrawRect(headerRect, color);
            EditorGUI.LabelField(headerRect, tierLabel, headerStyle);
            EditorGUI.DrawRect(fieldRect, new Color(color.r, color.g, color.b, 0.18f));
            EditorGUI.PropertyField(fieldRect, property.FindPropertyRelative(field), GUIContent.none);
        }
    }
}
