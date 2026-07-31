using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>SelectableReferenceDrawer가 [SerializeReference] 객체(예: MultiShotSkillAction)의 필드를 그릴 때 쓰는
/// classic-IMGUI 전용 그룹 렌더러. 최상위 ScriptableObject 필드는 Alchemy의 ColorFoldoutGroupDrawer가 처리하지만,
/// SerializeReference로 중첩된 값의 필드는 Alchemy 그룹 시스템이 아예 개입하지 않는 클래식 재귀 경로를 타므로
/// (Assets/WorkSpace/HSD/Scripts/Attributes/SelectableReference/Editor/SelectableReferenceAlchemyDrawer.cs 참고),
/// 같은 [ColorFoldoutGroup] attribute를 재사용해 이 경로 전용으로 등급별값 에디터와 같은 색 배지 헤더 + 얇은 테두리 박스로 묶어 그린다.
/// 선언 순서상 인접한 필드끼리만 하나의 묶음으로 합친다.</summary>
public static class ColorFieldGroupDrawer
{
    private const float BadgeHeight = 16f;
    private const float BoxPadding = 3f;
    private const float ClusterGap = 4f;

    private struct Run
    {
        public ColorFoldoutGroupAttribute group;
        public List<FieldInfo> fields;
    }

    public static bool HasGroups(Type type) =>
        type != null && GetFields(type).Any(f => f.GetCustomAttribute<ColorFoldoutGroupAttribute>() != null);

    public static float GetHeight(SerializedProperty property, Type type)
    {
        float total = 0f;
        foreach (var run in BuildRuns(property, type))
        {
            total += RunFieldsHeight(run.fields, property);
            if (run.group != null) total += BadgeHeight + BoxPadding * 2f;
            total += ClusterGap;
        }
        return total > 0f ? total - ClusterGap : 0f;
    }

    public static void Draw(Rect position, SerializedProperty property, Type type)
    {
        float y = position.y;
        var runs = BuildRuns(property, type);

        for (int r = 0; r < runs.Count; r++)
        {
            var run = runs[r];
            float fieldsHeight = RunFieldsHeight(run.fields, property);

            if (run.group != null && ColorUtility.TryParseHtmlString(run.group.HexColor, out var color))
            {
                float boxHeight = BadgeHeight + BoxPadding * 2f + fieldsHeight;
                DrawBoxBorder(new Rect(position.x, y, position.width, boxHeight), new Color(color.r, color.g, color.b, 0.4f));

                var badgeRow = new Rect(position.x + BoxPadding, y + BoxPadding, position.width - BoxPadding * 2f, BadgeHeight);
                DrawBadge(badgeRow, run.group.GroupPath, color);

                float fy = y + BoxPadding + BadgeHeight;
                DrawFieldsSequential(position.x + BoxPadding, ref fy, position.width - BoxPadding * 2f, run.fields, property);

                y += boxHeight;
            }
            else
            {
                float fy = y;
                DrawFieldsSequential(position.x, ref fy, position.width, run.fields, property);
                y = fy;
            }

            if (r < runs.Count - 1) y += ClusterGap;
        }
    }

    private static FieldInfo[] GetFields(Type type) =>
        type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => !f.IsNotSerialized && (f.IsPublic || f.GetCustomAttribute<SerializeField>() != null || f.GetCustomAttribute<SerializeReference>() != null))
            .ToArray();

    private static List<Run> BuildRuns(SerializedProperty property, Type type)
    {
        var runs = new List<Run>();
        Run current = default;

        foreach (var field in GetFields(type))
        {
            if (property.FindPropertyRelative(field.Name) == null) continue;

            var group = field.GetCustomAttribute<ColorFoldoutGroupAttribute>();
            bool sameRun = current.fields != null &&
                ((group == null && current.group == null) ||
                 (group != null && current.group != null && group.GroupPath == current.group.GroupPath));

            if (!sameRun)
            {
                if (current.fields != null) runs.Add(current);
                current = new Run { group = group, fields = new List<FieldInfo>() };
            }
            current.fields.Add(field);
        }
        if (current.fields != null) runs.Add(current);
        return runs;
    }

    private static float RunFieldsHeight(List<FieldInfo> fields, SerializedProperty parent)
    {
        float h = 0f;
        for (int i = 0; i < fields.Count; i++)
        {
            h += EditorGUI.GetPropertyHeight(parent.FindPropertyRelative(fields[i].Name), true);
            h += EditorGUIUtility.standardVerticalSpacing;
        }
        return h;
    }

    private static void DrawFieldsSequential(float x, ref float y, float width, List<FieldInfo> fields, SerializedProperty parent)
    {
        foreach (var field in fields)
        {
            var child = parent.FindPropertyRelative(field.Name);
            float h = EditorGUI.GetPropertyHeight(child, true);
            EditorGUI.PropertyField(new Rect(x, y, width, h), child, true);
            y += h + EditorGUIUtility.standardVerticalSpacing;
        }
    }

    private static void DrawBadge(Rect row, string groupPath, Color color)
    {
        int splitIndex = groupPath.IndexOf(" (", StringComparison.Ordinal);
        string badgeText = splitIndex >= 0 ? groupPath.Substring(0, splitIndex) : groupPath;
        string restText = splitIndex >= 0 ? groupPath.Substring(splitIndex + 1) : "";

        var badgeStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
        };
        float badgeWidth = Mathf.Min(row.width * 0.6f, badgeStyle.CalcSize(new GUIContent(badgeText)).x + 12f);

        var badgeRect = new Rect(row.x, row.y, badgeWidth, row.height);
        EditorGUI.DrawRect(badgeRect, color);
        EditorGUI.LabelField(badgeRect, badgeText, badgeStyle);

        if (!string.IsNullOrEmpty(restText))
        {
            var restRect = new Rect(row.x + badgeWidth + 4f, row.y, row.width - badgeWidth - 4f, row.height);
            var restStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
            EditorGUI.LabelField(restRect, restText, restStyle);
        }
    }

    private static void DrawBoxBorder(Rect rect, Color color)
    {
        const float t = 1f;
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, t), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - t, rect.width, t), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, t, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - t, rect.y, t, rect.height), color);
    }
}
