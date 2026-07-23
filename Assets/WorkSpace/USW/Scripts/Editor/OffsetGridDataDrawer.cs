using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// OffsetGridData.cells(중심 기준 상대 좌표 목록)를 TotemEditorWindow의 그리드 클릭 편집과
/// 동일한 방식으로 그려준다. OffsetGridData는 일반(Generic) 타입 필드라 Alchemy 인스펙터가
/// InternalAPIHelper.GetDrawerTypeForType으로 이 드로어를 찾아 표준 PropertyField(IMGUIContainer)로
/// 위임해주므로, [SerializeReference] 필드(BuffAllyInRangeSkillAction.buff 등)와 달리 인스펙터에
/// 정상적으로 인라인 표시된다.
/// </summary>
[CustomPropertyDrawer(typeof(OffsetGridData))]
public class OffsetGridDataDrawer : PropertyDrawer
{
    private const int   GridRadius = 3; // 중심 기준 -3 ~ +3
    private const float CellSize   = 22f;

    private static readonly Color ColCenter   = new Color(0.55f, 0.75f, 0.30f);
    private static readonly Color ColSelected = new Color(0.65f, 0.25f, 0.25f);
    private static readonly Color ColEmpty    = new Color(0.20f, 0.20f, 0.20f);
    private static readonly Color ColBorder   = new Color(0.38f, 0.38f, 0.38f);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var cellsProp = property.FindPropertyRelative("cells");

        float y = position.y;
        string title = string.IsNullOrEmpty(label.text) ? "범위" : label.text;
        var labelRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.LabelField(labelRect, $"{title} (중심 기준, {cellsProp.arraySize}칸 지정됨)");
        y += EditorGUIUtility.singleLineHeight + 2f;

        int   size      = GridRadius * 2 + 1;
        float gridWidth = size * CellSize;
        float startX    = position.x + Mathf.Max(0f, (position.width - gridWidth) * 0.5f);

        var cells = ReadCells(cellsProp);

        for (int row = 0; row < size; row++)
        {
            int gy = row - GridRadius;
            for (int col = 0; col < size; col++)
            {
                int gx = col - GridRadius;
                var cellRect = new Rect(startX + col * CellSize + 1f, y + row * CellSize + 1f,
                    CellSize - 2f, CellSize - 2f);

                bool isCenter   = gx == 0 && gy == 0;
                var  offset     = new Vector2Int(gx, gy);
                bool isSelected = cells.Contains(offset);

                Color bg = isCenter ? ColCenter : isSelected ? ColSelected : ColEmpty;
                EditorGUI.DrawRect(cellRect, bg);
                DrawBorder(cellRect, ColBorder);

                if (!isCenter && GUI.Button(cellRect, GUIContent.none, GUIStyle.none))
                    ToggleCell(cellsProp, offset);
            }
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int size = GridRadius * 2 + 1;
        return EditorGUIUtility.singleLineHeight + 4f + size * CellSize;
    }

    private static List<Vector2Int> ReadCells(SerializedProperty cellsProp)
    {
        var list = new List<Vector2Int>(cellsProp.arraySize);
        for (int i = 0; i < cellsProp.arraySize; i++)
            list.Add(cellsProp.GetArrayElementAtIndex(i).vector2IntValue);
        return list;
    }

    private static void ToggleCell(SerializedProperty cellsProp, Vector2Int offset)
    {
        for (int i = 0; i < cellsProp.arraySize; i++)
        {
            if (cellsProp.GetArrayElementAtIndex(i).vector2IntValue == offset)
            {
                cellsProp.DeleteArrayElementAtIndex(i);
                cellsProp.serializedObject.ApplyModifiedProperties();
                return;
            }
        }

        cellsProp.arraySize++;
        cellsProp.GetArrayElementAtIndex(cellsProp.arraySize - 1).vector2IntValue = offset;
        cellsProp.serializedObject.ApplyModifiedProperties();
    }

    private static void DrawBorder(Rect rect, Color color)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
    }
}
