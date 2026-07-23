using UnityEngine;
using UnityEditor;

/// <summary>
/// BuffAllyInRangeSkillAction.offsets(캐스터 중심 기준 상대 좌표)를 그리드 클릭으로 편집하는 툴.
///
/// Inspector에 CustomPropertyDrawer로 내장하는 방식은 이 프로젝트에서 ScriptableObject 기본
/// 에디터로 쓰이는 Alchemy(Alchemy.Editor.ScriptableObjectEditor)가 [SerializeReference] 필드를
/// 항상 자체 UI Toolkit 컴포넌트(SerializeReferenceField)로 그리며 클래식 PropertyDrawer 체인을
/// 전혀 타지 않아 동작하지 않는다 — TotemEditorWindow가 같은 이유로 별도 창인 것과 동일한 사정이라
/// 그 패턴을 그대로 따른다(SerializedProperty를 거치지 않고 Undo.RecordObject + 직접 필드 대입).
/// </summary>
public class BuffRangeEditorWindow : EditorWindow
{
    private const int   GridRadius = 3; // 캐스터 기준 -3 ~ +3
    private const float CellSize   = 32f;

    private static readonly Color ColCenter      = new Color(0x92 / 255f, 0xD0 / 255f, 0x50 / 255f);
    private static readonly Color ColSelected    = new Color(0xA2 / 255f, 0x00 / 255f, 0x00 / 255f);
    private static readonly Color ColEmpty       = new Color(0.20f, 0.20f, 0.20f);
    private static readonly Color ColEmptyHover  = new Color(0.30f, 0.30f, 0.30f);

    private SkillData  _data;
    private Vector2     _scroll;
    private Vector2Int  _hovered = new Vector2Int(-1, -1);

    [MenuItem("Tools/Buff Range Editor")]
    public static void Open()
    {
        var win = GetWindow<BuffRangeEditorWindow>("Buff Range Editor");
        win.minSize        = new Vector2(360f, 480f);
        win.wantsMouseMove = true;
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawColorHeader("Buff Range Editor", new Color(0.18f, 0.28f, 0.48f));
        GUILayout.Space(8f);

        EditorGUI.BeginChangeCheck();
        var newData = (SkillData)EditorGUILayout.ObjectField("SkillData", _data, typeof(SkillData), false);
        if (EditorGUI.EndChangeCheck())
            _data = newData;

        if (_data == null)
        {
            EditorGUILayout.EndScrollView();
            return;
        }

        GUILayout.Space(6f);

        if (_data.action is not BuffAllyInRangeSkillAction action)
        {
            EditorGUILayout.HelpBox(
                "이 SkillData의 action이 BuffAllyInRangeSkillAction이 아닙니다.\n" +
                "(Inspector 드롭다운에서 먼저 BuffAllyInRangeSkillAction으로 선택하세요.)",
                MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        DrawSeparator();
        GUILayout.Space(6f);

        EditorGUI.BeginChangeCheck();
        var buff = (BuffData)EditorGUILayout.ObjectField("버프", action.buff, typeof(BuffData), false);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_data, "Change Buff");
            action.buff = buff;
            EditorUtility.SetDirty(_data);
        }

        GUILayout.Space(10f);
        EditorGUILayout.LabelField($"버프 대상 칸 (중심 기준, {action.range.cells.Count}칸)", EditorStyles.boldLabel);
        DrawSeparator();
        GUILayout.Space(6f);

        DrawGrid(action);

        GUILayout.Space(8f);
        DrawLegend();

        GUILayout.Space(10f);
        DrawSeparator();
        GUILayout.Space(8f);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("저장 (디스크에 기록)", GUILayout.Width(170f), GUILayout.Height(30f)))
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[BuffRangeEditor] '{_data.skillName}' 저장 완료 — {action.range.cells.Count}칸.");
            }
        }

        EditorGUILayout.EndScrollView();
    }

    // ── 그리드 ────────────────────────────────────────────────────
    private void DrawGrid(BuffAllyInRangeSkillAction action)
    {
        int   size   = GridRadius * 2 + 1;
        float totalW = size * CellSize;

        Rect baseRect = GUILayoutUtility.GetRect(totalW, size * CellSize);
        baseRect.x = Mathf.Max(8f, (EditorGUIUtility.currentViewWidth - totalW) * 0.5f);

        HandleGridInput(baseRect, action, size);

        for (int row = 0; row < size; row++)
        {
            int gy = row - GridRadius;
            for (int col = 0; col < size; col++)
            {
                int gx = col - GridRadius;
                var cellRect = new Rect(baseRect.x + col * CellSize + 1f, baseRect.y + row * CellSize + 1f,
                    CellSize - 2f, CellSize - 2f);

                bool isCenter   = gx == 0 && gy == 0;
                bool isSelected = action.range.cells.Contains(new Vector2Int(gx, gy));
                bool isHover    = _hovered.x == col && _hovered.y == row;

                DrawCell(cellRect, isCenter, isSelected, isHover);
            }
        }
    }

    private void DrawCell(Rect rect, bool isCenter, bool isSelected, bool isHover)
    {
        Color bg = isCenter ? ColCenter : isSelected ? ColSelected : (isHover ? ColEmptyHover : ColEmpty);
        EditorGUI.DrawRect(rect, bg);
        DrawBorder(rect, isCenter ? new Color(1f, 1f, 1f, 0.5f) : new Color(0.38f, 0.38f, 0.38f), 1f);

        if (isCenter)
        {
            GUI.Label(rect, "C", new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(0f, 0f, 0f, 0.65f) },
                fontSize  = 16
            });
        }
    }

    private void HandleGridInput(Rect baseRect, BuffAllyInRangeSkillAction action, int size)
    {
        var ev = Event.current;
        if (ev.type == EventType.Layout) return;

        _hovered = new Vector2Int(-1, -1);

        for (int row = 0; row < size; row++)
        for (int col = 0; col < size; col++)
        {
            var r = new Rect(baseRect.x + col * CellSize + 1f, baseRect.y + row * CellSize + 1f,
                CellSize - 2f, CellSize - 2f);
            if (!r.Contains(ev.mousePosition)) continue;

            int  gx       = col - GridRadius;
            int  gy       = row - GridRadius;
            bool isCenter = gx == 0 && gy == 0;
            if (!isCenter) _hovered = new Vector2Int(col, row);

            if (!isCenter && ev.type == EventType.MouseDown && ev.button == 0)
            {
                ToggleOffset(action, new Vector2Int(gx, gy));
                ev.Use();
                Repaint();
            }
            goto Done;
        }
        Done:

        if (ev.type == EventType.MouseMove) Repaint();
    }

    private void ToggleOffset(BuffAllyInRangeSkillAction action, Vector2Int offset)
    {
        Undo.RecordObject(_data, "Edit Buff Range");
        int idx = action.range.cells.IndexOf(offset);
        if (idx >= 0) action.range.cells.RemoveAt(idx);
        else action.range.cells.Add(offset);
        EditorUtility.SetDirty(_data);
    }

    // ── 범례 ──────────────────────────────────────────────────────
    private void DrawLegend()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(8f);
            LegendItem(ColCenter, "C", "캐스터 위치");
            GUILayout.Space(16f);
            LegendItem(ColSelected, "", "버프 대상 칸");
            GUILayout.Space(16f);
            LegendItem(ColEmpty, "", "비어있음");
        }
    }

    private void LegendItem(Color color, string symbol, string label)
    {
        var box = GUILayoutUtility.GetRect(22f, 22f, GUILayout.Width(22f), GUILayout.Height(22f));
        EditorGUI.DrawRect(box, color);
        DrawBorder(box, new Color(0.5f, 0.5f, 0.5f), 1f);

        if (!string.IsNullOrEmpty(symbol))
            GUI.Label(box, symbol, new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(1f, 1f, 1f, 0.65f) },
                fontSize  = 12
            });

        GUILayout.Space(4f);
        EditorGUILayout.LabelField(label, GUILayout.Width(90f));
    }

    // ── 유틸 ──────────────────────────────────────────────────────
    private static void DrawColorHeader(string title, Color color)
    {
        var rect = EditorGUILayout.GetControlRect(false, 26f);
        EditorGUI.DrawRect(rect, color);
        EditorGUI.LabelField(rect, title, new GUIStyle(EditorStyles.boldLabel)
        {
            normal   = { textColor = Color.white },
            padding  = new RectOffset(10, 0, 0, 0),
            fontSize = 13
        });
    }

    private static void DrawSeparator()
    {
        var rect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(rect, new Color(0.35f, 0.35f, 0.35f));
    }

    private static void DrawBorder(Rect rect, Color color, float t)
    {
        EditorGUI.DrawRect(new Rect(rect.x,        rect.y,        rect.width, t),          color);
        EditorGUI.DrawRect(new Rect(rect.x,        rect.yMax - t, rect.width, t),          color);
        EditorGUI.DrawRect(new Rect(rect.x,        rect.y,        t,          rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - t, rect.y,        t,          rect.height), color);
    }
}
