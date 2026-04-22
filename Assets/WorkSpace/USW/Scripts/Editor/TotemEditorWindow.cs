using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 토템 효과 범위 시각 편집 툴
///
/// 6×4 그리드에서 D3를 토템 고정 위치로 두고
/// 클릭으로 효과 범위 / 공격불가 범위를 지정 → TotemData SO에 저장.
///
/// 저장 형식: 토템 위치(D3) 기준 상대 오프셋 Vector2Int
///   예) E3 → offset(+1, 0) / D2 → offset(0, -1)
///
/// [셀 상태 순환]  빈칸 → 효과범위(빨강) → 공격불가(검정) → 빈칸
/// </summary>
public class TotemEditorWindow : EditorWindow
{
    // ── 상수 ──────────────────────────────────────────────────────
    private const int   Cols     = 6;
    private const int   Rows     = 4;
    private const float CellSize = 68f;
    private const float LabelW   = 28f;

    // D3: col D = index 3,  row 3 = index 2 (0-based, 위에서부터)
    private static readonly Vector2Int TotemCell = new Vector2Int(3, 2);

    private static readonly string[] ColLabels = { "A", "B", "C", "D", "E", "F" };
    private static readonly string[] RowLabels = { "1", "2", "3", "4" };

    // 지정 색상
    private static readonly Color ColTotem         = new Color(0x92 / 255f, 0xD0 / 255f, 0x50 / 255f); // #92d050
    private static readonly Color ColEffect        = new Color(0xA2 / 255f, 0x00 / 255f, 0x00 / 255f); // #a20000
    private static readonly Color ColAttackDisable = Color.black;                                        // #000000
    private static readonly Color ColEmpty         = new Color(0.20f, 0.20f, 0.20f);
    private static readonly Color ColEmptyHover    = new Color(0.30f, 0.30f, 0.30f);

    // ── 상태 ──────────────────────────────────────────────────────
    // 0=빈칸  1=토템(고정)  2=효과범위  3=공격불가
    private int[,] _grid = new int[Cols, Rows];

    private TotemData   _data;
    private Vector2     _scroll;
    private Vector2Int  _hovered = new Vector2Int(-1, -1);

    // 캐시드 스타일
    private GUIStyle _centerStyle;
    private GUIStyle CenterStyle => _centerStyle ??= new GUIStyle(EditorStyles.label)
        { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

    // ── 메뉴 ──────────────────────────────────────────────────────
    [MenuItem("Game/Totem Editor")]
    public static void Open()
    {
        var win = GetWindow<TotemEditorWindow>("Totem Editor");
        win.minSize        = new Vector2(500f, 540f);
        win.wantsMouseMove = true;
    }

    private void OnEnable() => ResetGrid();

    // ── OnGUI ─────────────────────────────────────────────────────
    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawHeader();
        GUILayout.Space(14f);
        DrawGrid();
        GUILayout.Space(12f);
        DrawLegend();
        GUILayout.Space(6f);
        DrawRangeInfo();
        GUILayout.Space(14f);
        DrawActions();

        EditorGUILayout.EndScrollView();
    }

    // ── 헤더 ──────────────────────────────────────────────────────
    private void DrawHeader()
    {
        DrawColorHeader("Totem Range Editor", new Color(0.18f, 0.28f, 0.48f));
        GUILayout.Space(8f);

        EditorGUI.BeginChangeCheck();
        var newData = (TotemData)EditorGUILayout.ObjectField(
            "TotemData", _data, typeof(TotemData), false);
        if (EditorGUI.EndChangeCheck())
        {
            _data = newData;
            ResetGrid();
            if (_data != null) LoadFromData();
            Repaint();
        }

        if (_data == null) return;

        GUILayout.Space(4f);
        DrawSeparator();
        GUILayout.Space(4f);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("이름", GUILayout.Width(50f));
            EditorGUILayout.LabelField(_data.totemName, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField("희귀도", GUILayout.Width(50f));
            EditorGUI.BeginChangeCheck();
            var r = (TotemRarity)EditorGUILayout.EnumPopup(_data.rarity, GUILayout.Width(90f));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_data, "Change Rarity");
                _data.rarity = r;
                EditorUtility.SetDirty(_data);
            }
        }
    }

    // ── 그리드 ────────────────────────────────────────────────────
    private void DrawGrid()
    {
        float totalW  = LabelW + Cols * CellSize;
        float totalH  = LabelW + Rows * CellSize;
        float offsetX = Mathf.Max(8f, (EditorGUIUtility.currentViewWidth - totalW) * 0.5f);

        Rect baseRect = GUILayoutUtility.GetRect(totalW, totalH);
        baseRect.x = offsetX;

        // Layout 이벤트 전에 hover/input을 갱신 — Repaint와 같은 좌표계 보장
        HandleGridInput(baseRect);

        // 열 레이블 (A–F)
        for (int col = 0; col < Cols; col++)
        {
            var r = new Rect(baseRect.x + LabelW + col * CellSize, baseRect.y, CellSize, LabelW);
            EditorGUI.LabelField(r, ColLabels[col], CenterStyle);
        }

        // 행 레이블 + 셀
        for (int row = 0; row < Rows; row++)
        {
            float y = baseRect.y + LabelW + row * CellSize;

            // 행 레이블 (1–4)
            EditorGUI.LabelField(
                new Rect(baseRect.x, y, LabelW, CellSize), RowLabels[row], CenterStyle);

            for (int col = 0; col < Cols; col++)
            {
                float x       = baseRect.x + LabelW + col * CellSize;
                var   cellRect = new Rect(x + 1f, y + 1f, CellSize - 2f, CellSize - 2f);
                bool  isTotem = col == TotemCell.x && row == TotemCell.y;
                bool  isHov   = _hovered.x == col  && _hovered.y == row;

                DrawCell(cellRect, col, row, isTotem, isHov);
            }
        }
    }

    private void DrawCell(Rect rect, int col, int row, bool isTotem, bool isHover)
    {
        int   state = _grid[col, row];
        Color bg    = state switch
        {
            1 => ColTotem,
            2 => ColEffect,
            3 => ColAttackDisable,
            _ => isHover && !isTotem ? ColEmptyHover : ColEmpty
        };

        EditorGUI.DrawRect(rect, bg);

        // 테두리
        Color border = isTotem
            ? new Color(1f, 1f, 1f, 0.5f)
            : state != 0 ? new Color(1f, 1f, 1f, 0.12f) : new Color(0.38f, 0.38f, 0.38f);
        DrawBorder(rect, border, 1f);

        // 아이콘 / 텍스트
        if (isTotem)
        {
            GUI.Label(rect, "T", new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(0f, 0f, 0f, 0.65f) },
                fontSize  = 20
            });
        }
    }

    private void HandleGridInput(Rect baseRect)
    {
        var ev = Event.current;
        if (ev.type == EventType.Layout) return;

        _hovered = new Vector2Int(-1, -1);

        for (int row = 0; row < Rows; row++)
        for (int col = 0; col < Cols; col++)
        {
            float x = baseRect.x + LabelW + col * CellSize + 1f;
            float y = baseRect.y + LabelW + row * CellSize + 1f;
            var   r = new Rect(x, y, CellSize - 2f, CellSize - 2f);

            if (!r.Contains(ev.mousePosition)) continue;

            bool isTotem = col == TotemCell.x && row == TotemCell.y;
            if (!isTotem) _hovered = new Vector2Int(col, row);

            if (!isTotem && ev.type == EventType.MouseDown && ev.button == 0)
            {
                CycleCell(col, row);
                ev.Use();
                Repaint();
            }
            goto Done;
        }
        Done:

        if (ev.type == EventType.MouseMove) Repaint();
    }

    private void CycleCell(int col, int row)
    {
        _grid[col, row] = _grid[col, row] switch
        {
            0 => 2,
            2 => 3,
            3 => 0,
            _ => 0
        };
    }

    // ── 범례 ──────────────────────────────────────────────────────
    private void DrawLegend()
    {
        EditorGUILayout.LabelField("범례", EditorStyles.boldLabel);
        DrawSeparator();
        GUILayout.Space(6f);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(8f);
            LegendItem(ColTotem,         "T",  "토템 위치 (D3 고정)");
            GUILayout.Space(20f);
            LegendItem(ColEffect,        "",   "효과 범위");
            GUILayout.Space(20f);
            LegendItem(ColAttackDisable, "",   "공격 불가");
            GUILayout.Space(20f);
            LegendItem(ColEmpty,         "",   "비어있음");
        }

        GUILayout.Space(6f);
        EditorGUILayout.LabelField(
            "클릭: 비어있음 → 효과범위 → 공격불가 → 비어있음  순환",
            EditorStyles.centeredGreyMiniLabel);
    }

    private void LegendItem(Color color, string symbol, string label)
    {
        var box = GUILayoutUtility.GetRect(26f, 26f, GUILayout.Width(26f), GUILayout.Height(26f));
        EditorGUI.DrawRect(box, color);
        DrawBorder(box, new Color(0.5f, 0.5f, 0.5f), 1f);

        if (!string.IsNullOrEmpty(symbol))
            GUI.Label(box, symbol, new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(1f, 1f, 1f, 0.65f) },
                fontSize  = 14
            });

        GUILayout.Space(4f);
        EditorGUILayout.LabelField(label, GUILayout.Width(130f));
    }

    // ── 범위 요약 ─────────────────────────────────────────────────
    private void DrawRangeInfo()
    {
        int effectCnt  = CountState(2);
        int disableCnt = CountState(3);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(8f);
            var style = new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } };
            EditorGUILayout.LabelField($"효과 범위: {effectCnt}칸", style, GUILayout.Width(120f));
            EditorGUILayout.LabelField($"공격 불가: {disableCnt}칸", style, GUILayout.Width(120f));
        }
    }

    private int CountState(int state)
    {
        int n = 0;
        for (int c = 0; c < Cols; c++)
        for (int r = 0; r < Rows; r++)
            if (_grid[c, r] == state) n++;
        return n;
    }

    // ── 액션 ──────────────────────────────────────────────────────
    private void DrawActions()
    {
        DrawSeparator();
        GUILayout.Space(8f);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("초기화", GUILayout.Width(80f), GUILayout.Height(32f)))
            {
                ResetGrid();
                if (_data != null) LoadFromData();
            }

            GUILayout.FlexibleSpace();

            bool canSave = _data != null;
            GUI.enabled         = canSave;
            GUI.backgroundColor = canSave ? new Color(0.25f, 0.65f, 0.3f) : Color.gray;

            if (GUILayout.Button("저장  →  ScriptableObject", GUILayout.Width(210f), GUILayout.Height(32f)))
                SaveToData();

            GUI.backgroundColor = Color.white;
            GUI.enabled         = true;
        }

        if (_data == null)
        {
            GUILayout.Space(6f);
            EditorGUILayout.HelpBox("상단에서 TotemData SO를 연결하세요.", MessageType.Info);
        }
    }

    // ── 데이터 I/O ────────────────────────────────────────────────
    private void SaveToData()
    {
        Undo.RecordObject(_data, "Save Totem Range");

        _data.effectRange.Clear();
        _data.attackDisabledRange.Clear();

        for (int col = 0; col < Cols; col++)
        for (int row = 0; row < Rows; row++)
        {
            var offset = new Vector2Int(col - TotemCell.x, row - TotemCell.y);
            if      (_grid[col, row] == 2) _data.effectRange.Add(offset);
            else if (_grid[col, row] == 3) _data.attackDisabledRange.Add(offset);
        }

        EditorUtility.SetDirty(_data);
        AssetDatabase.SaveAssets();

        Debug.Log($"[TotemEditor] '{_data.totemName}' 저장 — " +
                  $"효과 {_data.effectRange.Count}칸 / 공격불가 {_data.attackDisabledRange.Count}칸");
    }

    private void LoadFromData()
    {
        foreach (var offset in _data.effectRange)
        {
            int col = TotemCell.x + offset.x;
            int row = TotemCell.y + offset.y;
            if (InBounds(col, row)) _grid[col, row] = 2;
        }

        foreach (var offset in _data.attackDisabledRange)
        {
            int col = TotemCell.x + offset.x;
            int row = TotemCell.y + offset.y;
            if (InBounds(col, row)) _grid[col, row] = 3;
        }
    }

    // ── 유틸 ──────────────────────────────────────────────────────
    private void ResetGrid()
    {
        _grid = new int[Cols, Rows];
        _grid[TotemCell.x, TotemCell.y] = 1;
    }

    private bool InBounds(int col, int row)
        => col >= 0 && col < Cols && row >= 0 && row < Rows;

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
