using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 토템 기능/범위 편집 툴 — TotemData의 실제 구조(functions / effectRanges / attackDisabledRanges,
/// 전부 여러 개를 담을 수 있는 SelectableReference 리스트)를 그대로 반영한다.
///
/// - 버프(functions): 항목별로 [단순 버프 / 조건부 버프] 타입을 골라 추가·삭제하고, 파라미터를 인라인으로 편집.
/// - 효과 범위 / 공격 불가 범위(effectRanges / attackDisabledRanges): 항목별로
///   [토템 기준 지정 / 고정 / 특정 방향으로 쭉] 타입을 골라 여러 개 추가·삭제 가능.
///   오프셋 기반 타입(토템 기준 지정, 고정)은 "그리드에서 편집" 버튼으로 활성화한 뒤
///   하단의 통합 미리보기 그리드를 클릭해 칸을 추가/제거한다.
/// - 하단 그리드는 항상 모든 항목을 합친 통합 미리보기(D3 = 토템 위치 고정).
/// </summary>
public class TotemEditorWindow : EditorWindow
{
    // ── 그리드 상수 ────────────────────────────────────────────────
    private const int   Cols     = 6;
    private const int   Rows     = 4;
    private const float CellSize = 56f;
    private const float LabelW   = 24f;

    private static readonly Vector2Int TotemCell = new Vector2Int(3, 2);
    private static readonly string[] ColLabels = { "A", "B", "C", "D", "E", "F" };
    private static readonly string[] RowLabels = { "1", "2", "3", "4" };

    private static readonly Color ColTotem         = new Color(0x92 / 255f, 0xD0 / 255f, 0x50 / 255f);
    private static readonly Color ColEffect        = new Color(0xA2 / 255f, 0x00 / 255f, 0x00 / 255f);
    private static readonly Color ColAttackDisable = Color.black;
    private static readonly Color ColEmpty         = new Color(0.20f, 0.20f, 0.20f);
    private static readonly Color ColEmptyHover    = new Color(0.30f, 0.30f, 0.30f);

    private TotemData  _data;
    private Vector2    _scroll;
    private Vector2Int _hovered = new Vector2Int(-1, -1);

    /// <summary>그리드 클릭으로 오프셋을 편집 중인 range 항목 (토템 기준 지정 / 고정 타입만 가능).</summary>
    private ITotemRange _activeRange;

    private GUIStyle _centerStyle;
    private GUIStyle CenterStyle => _centerStyle ??= new GUIStyle(EditorStyles.label)
        { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

    // ── 메뉴 ──────────────────────────────────────────────────────
    [MenuItem("Tools/Totem Editor")]
    public static void Open()
    {
        var win = GetWindow<TotemEditorWindow>("Totem Editor");
        win.minSize        = new Vector2(560f, 700f);
        win.wantsMouseMove = true;
    }

    // ── OnGUI ─────────────────────────────────────────────────────
    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawHeader();

        if (_data != null)
        {
            GUILayout.Space(10f);
            DrawFunctionsSection();
            GUILayout.Space(10f);
            DrawRangeSection("효과 범위 (effectRanges)", _data.effectRanges);
            GUILayout.Space(10f);
            DrawRangeSection("공격 불가 범위 (attackDisabledRanges)", _data.attackDisabledRanges);
            GUILayout.Space(14f);
            DrawPreviewGrid();
            GUILayout.Space(10f);
            DrawFooterActions();
        }

        EditorGUILayout.EndScrollView();
    }

    // ── 헤더 ──────────────────────────────────────────────────────
    private void DrawHeader()
    {
        DrawColorHeader("Totem Editor", new Color(0.18f, 0.28f, 0.48f));
        GUILayout.Space(8f);

        EditorGUI.BeginChangeCheck();
        var newData = (TotemData)EditorGUILayout.ObjectField(
            "TotemData", _data, typeof(TotemData), false);
        if (EditorGUI.EndChangeCheck())
        {
            _data        = newData;
            _activeRange = null;
        }

        if (_data == null) return;

        GUILayout.Space(4f);
        DrawSeparator();
        GUILayout.Space(4f);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("이름", GUILayout.Width(50f));
            EditorGUI.BeginChangeCheck();
            var name = EditorGUILayout.TextField(_data.totemName);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_data, "Change Totem Name");
                _data.totemName = name;
                EditorUtility.SetDirty(_data);
            }

            GUILayout.Space(10f);

            EditorGUILayout.LabelField("희귀도", GUILayout.Width(50f));
            EditorGUI.BeginChangeCheck();
            var r = (Tier)EditorGUILayout.EnumPopup(_data.tier, GUILayout.Width(90f));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_data, "Change Rarity");
                _data.tier = r;
                EditorUtility.SetDirty(_data);
            }
        }

        GUILayout.Space(4f);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("설명", GUILayout.Width(50f));
            EditorGUI.BeginChangeCheck();
            var description = EditorGUILayout.TextArea(_data.description, GUILayout.Height(40f));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_data, "Change Totem Description");
                _data.description = description;
                EditorUtility.SetDirty(_data);
            }
        }
    }

    // ── 버프 (functions) ──────────────────────────────────────────
    private void DrawFunctionsSection()
    {
        EditorGUILayout.LabelField("버프 (functions)", EditorStyles.boldLabel);
        DrawSeparator();
        GUILayout.Space(4f);

        for (int i = 0; i < _data.functions.Count; i++)
            DrawFunctionEntry(i);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ 단순 버프 추가", GUILayout.Width(140f)))
            {
                Undo.RecordObject(_data, "Add Simple Buff");
                _data.functions.Add(new SimpleBuffFunction());
                EditorUtility.SetDirty(_data);
            }
            if (GUILayout.Button("+ 조건부 버프 추가", GUILayout.Width(140f)))
            {
                Undo.RecordObject(_data, "Add Conditional Buff");
                _data.functions.Add(new ConditionalBuffFunction { condition = new PositionThresholdCondition() });
                EditorUtility.SetDirty(_data);
            }
            if (GUILayout.Button("+ 식량 생성 버프 추가", GUILayout.Width(150f)))
            {
                Undo.RecordObject(_data, "Add Food Generator");
                _data.functions.Add(new FoodGeneratorFunction());
                EditorUtility.SetDirty(_data);
            }
        }
    }

    private void DrawFunctionEntry(int index)
    {
        var fn = _data.functions[index];

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{index + 1}. {FunctionTypeName(fn)}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("삭제", GUILayout.Width(50f)))
                {
                    Undo.RecordObject(_data, "Remove Buff");
                    _data.functions.RemoveAt(index);
                    EditorUtility.SetDirty(_data);
                    GUIUtility.ExitGUI();
                }
            }

            switch (fn)
            {
                case SimpleBuffFunction simple:
                    DrawSimpleBuffFields(simple);
                    break;
                case ConditionalBuffFunction cond:
                    DrawConditionalBuffFields(cond);
                    break;
                case FoodGeneratorFunction gen:
                    DrawFoodGeneratorFields(gen);
                    break;
            }
        }

        GUILayout.Space(2f);
    }

    private static string FunctionTypeName(ITotemFunction fn) => fn switch
    {
        SimpleBuffFunction      => "단순 버프",
        ConditionalBuffFunction => "조건부 버프",
        FoodGeneratorFunction   => "식량 생성",
        _                       => fn?.GetType().Name ?? "None"
    };

    /// <summary>amount는 0.1 = 10% 형태로 저장되지만, 편집은 "10"처럼 퍼센트 값으로 한다.</summary>
    private void DrawSimpleBuffFields(SimpleBuffFunction simple)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("종류", GUILayout.Width(35f));
            EditorGUI.BeginChangeCheck();
            var kind        = (TotemBuffKind)EditorGUILayout.EnumPopup(simple.kind, GUILayout.Width(100f));
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("수치(%)", GUILayout.Width(45f));
            var amountPercent = EditorGUILayout.FloatField(simple.amount * 100f, GUILayout.Width(60f));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_data, "Edit Buff");
                simple.kind   = kind;
                simple.amount = amountPercent / 100f;
                EditorUtility.SetDirty(_data);
            }
        }
    }

    private void DrawFoodGeneratorFields(FoodGeneratorFunction gen)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("주기(초)", GUILayout.Width(50f));
            var interval = Mathf.Max(0.1f, EditorGUILayout.FloatField(gen.interval, GUILayout.Width(50f)));
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("생성량", GUILayout.Width(45f));
            var amount = EditorGUILayout.FloatField(gen.amount, GUILayout.Width(50f));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_data, "Edit Food Generator");
                gen.interval = interval;
                gen.amount   = amount;
                EditorUtility.SetDirty(_data);
            }
        }
    }

    private void DrawConditionalBuffFields(ConditionalBuffFunction cond)
    {
        if (cond.buff == null)
        {
            Undo.RecordObject(_data, "Init Buff");
            cond.buff = new SimpleBuffFunction();
            EditorUtility.SetDirty(_data);
        }

        EditorGUILayout.LabelField("조건", EditorStyles.miniBoldLabel);
        if (cond.condition is PositionThresholdCondition pos)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var axis       = (PositionThresholdCondition.Axis)EditorGUILayout.EnumPopup(pos.axis, GUILayout.Width(45f));
                var comparison = (PositionThresholdCondition.Comparison)EditorGUILayout.EnumPopup(pos.comparison, GUILayout.Width(120f));
                var threshold  = EditorGUILayout.IntField(pos.threshold, GUILayout.Width(40f));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_data, "Edit Condition");
                    pos.axis       = axis;
                    pos.comparison = comparison;
                    pos.threshold  = threshold;
                    EditorUtility.SetDirty(_data);
                }
            }
        }
        else if (GUILayout.Button("+ 위치 조건 추가", GUILayout.Width(120f)))
        {
            Undo.RecordObject(_data, "Add Condition");
            cond.condition = new PositionThresholdCondition();
            EditorUtility.SetDirty(_data);
        }

        GUILayout.Space(2f);
        EditorGUILayout.LabelField("만족 시 버프", EditorStyles.miniBoldLabel);
        DrawSimpleBuffFields(cond.buff);
    }

    // ── 범위 (effectRanges / attackDisabledRanges) ────────────────
    private void DrawRangeSection(string label, List<ITotemRange> ranges)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        DrawSeparator();
        GUILayout.Space(4f);

        for (int i = 0; i < ranges.Count; i++)
            DrawRangeEntry(ranges, i);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ 토템 기준 지정", GUILayout.Width(120f)))
                AddRange(ranges, new TotemRelativeOffsetRange());
            if (GUILayout.Button("+ 고정", GUILayout.Width(70f)))
                AddRange(ranges, new FixedOffsetRange());
            if (GUILayout.Button("+ 방향", GUILayout.Width(70f)))
                AddRange(ranges, new DirectionalLineRange());
        }
    }

    private void AddRange(List<ITotemRange> ranges, ITotemRange range)
    {
        Undo.RecordObject(_data, "Add Range");
        ranges.Add(range);
        EditorUtility.SetDirty(_data);
    }

    private void DrawRangeEntry(List<ITotemRange> ranges, int index)
    {
        var  range        = ranges[index];
        bool isActive     = ReferenceEquals(_activeRange, range);
        bool isOffsetBased = range is TotemRelativeOffsetRange || range is FixedOffsetRange;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    $"{index + 1}. {RangeTypeName(range)}  —  {RangeSummary(range)}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                if (isOffsetBased)
                {
                    string btnLabel = isActive ? "그리드 편집 종료" : "그리드에서 편집";
                    if (GUILayout.Button(btnLabel, GUILayout.Width(110f)))
                        _activeRange = isActive ? null : range;
                }

                if (GUILayout.Button("삭제", GUILayout.Width(50f)))
                {
                    Undo.RecordObject(_data, "Remove Range");
                    if (isActive) _activeRange = null;
                    ranges.RemoveAt(index);
                    EditorUtility.SetDirty(_data);
                    GUIUtility.ExitGUI();
                }
            }

            if (range is DirectionalLineRange dir)
                DrawDirectionalFields(dir);
            else if (isActive)
                EditorGUILayout.HelpBox("아래 통합 미리보기 그리드를 클릭해 이 항목의 칸을 추가/제거하세요.", MessageType.None);
        }

        GUILayout.Space(2f);
    }

    private static string RangeTypeName(ITotemRange range) => range switch
    {
        TotemRelativeOffsetRange => "토템 기준 지정",
        FixedOffsetRange         => "고정",
        DirectionalLineRange     => "특정 방향으로 쭉",
        _                        => range?.GetType().Name ?? "None"
    };

    private static string RangeSummary(ITotemRange range) => range switch
    {
        TotemRelativeOffsetRange rel       => $"{rel.offsets.Count}칸",
        FixedOffsetRange         fixedRng  => $"{fixedRng.offsets.Count}칸",
        DirectionalLineRange     dir       => $"{dir.direction} 방향 끝까지{(dir.rotationAware ? "" : " (회전 무시)")}",
        _                                  => ""
    };

    private void DrawDirectionalFields(DirectionalLineRange dir)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("방향", GUILayout.Width(30f));
            var direction = (TotemDirection)EditorGUILayout.EnumPopup(dir.direction, GUILayout.Width(90f));

            GUILayout.Space(6f);
            var rotationAware = GUILayout.Toggle(dir.rotationAware, "회전 반영", GUILayout.Width(80f));

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_data, "Edit Directional Range");
                dir.direction     = direction;
                dir.rotationAware = rotationAware;
                EditorUtility.SetDirty(_data);
            }
        }
    }

    // ── 통합 미리보기 그리드 ──────────────────────────────────────
    private void DrawPreviewGrid()
    {
        EditorGUILayout.LabelField("통합 미리보기", EditorStyles.boldLabel);
        DrawSeparator();
        GUILayout.Space(6f);

        var display = BuildDisplayGrid();

        float totalW  = LabelW + Cols * CellSize;
        float totalH  = LabelW + Rows * CellSize;
        float offsetX = Mathf.Max(8f, (EditorGUIUtility.currentViewWidth - totalW) * 0.5f);

        Rect baseRect = GUILayoutUtility.GetRect(totalW, totalH);
        baseRect.x = offsetX;

        HandleGridInput(baseRect);

        for (int col = 0; col < Cols; col++)
        {
            var r = new Rect(baseRect.x + LabelW + col * CellSize, baseRect.y, CellSize, LabelW);
            EditorGUI.LabelField(r, ColLabels[col], CenterStyle);
        }

        for (int row = 0; row < Rows; row++)
        {
            float y = baseRect.y + LabelW + row * CellSize;
            EditorGUI.LabelField(new Rect(baseRect.x, y, LabelW, CellSize), RowLabels[row], CenterStyle);

            for (int col = 0; col < Cols; col++)
            {
                float x        = baseRect.x + LabelW + col * CellSize;
                var   cellRect = new Rect(x + 1f, y + 1f, CellSize - 2f, CellSize - 2f);
                bool  isTotem  = col == TotemCell.x && row == TotemCell.y;
                bool  isHov    = _hovered.x == col && _hovered.y == row;

                DrawCell(cellRect, display[col, row], isTotem, isHov);
            }
        }

        GUILayout.Space(6f);
        if (_activeRange != null)
            EditorGUILayout.HelpBox("그리드를 클릭하면 활성화된 range 항목에 칸이 추가/제거됩니다.", MessageType.Info);

        GUILayout.Space(6f);
        DrawLegend();
    }

    /// <summary>모든 functions/ranges와 무관하게, 두 range 리스트의 통합 미리보기 오프셋만 그린다.</summary>
    private int[,] BuildDisplayGrid()
    {
        var display = new int[Cols, Rows];
        display[TotemCell.x, TotemCell.y] = 1;

        PaintOffsets(display, _data.GetEffectPreviewOffsets(), 2);
        PaintOffsets(display, _data.GetAttackDisabledPreviewOffsets(), 3);

        return display;
    }

    private static void PaintOffsets(int[,] display, List<Vector2Int> offsets, int state)
    {
        foreach (var offset in offsets)
        {
            int col = TotemCell.x + offset.x;
            int row = TotemCell.y + offset.y;
            if (col >= 0 && col < Cols && row >= 0 && row < Rows) display[col, row] = state;
        }
    }

    private void DrawCell(Rect rect, int state, bool isTotem, bool isHover)
    {
        Color bg = state switch
        {
            1 => ColTotem,
            2 => ColEffect,
            3 => ColAttackDisable,
            _ => isHover && !isTotem && _activeRange != null ? ColEmptyHover : ColEmpty
        };

        EditorGUI.DrawRect(rect, bg);

        Color border = isTotem
            ? new Color(1f, 1f, 1f, 0.5f)
            : state != 0 ? new Color(1f, 1f, 1f, 0.12f) : new Color(0.38f, 0.38f, 0.38f);
        DrawBorder(rect, border, 1f);

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

            if (!isTotem && ev.type == EventType.MouseDown && ev.button == 0 && _activeRange != null)
            {
                ToggleActiveOffset(col, row);
                ev.Use();
                Repaint();
            }
            goto Done;
        }
        Done:

        if (ev.type == EventType.MouseMove) Repaint();
    }

    private void ToggleActiveOffset(int col, int row)
    {
        List<Vector2Int> offsets = _activeRange switch
        {
            TotemRelativeOffsetRange rel      => rel.offsets,
            FixedOffsetRange         fixedRng => fixedRng.offsets,
            _                                 => null
        };
        if (offsets == null) return;

        var offset = new Vector2Int(col - TotemCell.x, row - TotemCell.y);

        Undo.RecordObject(_data, "Edit Range Offsets");
        int idx = offsets.IndexOf(offset);
        if (idx >= 0) offsets.RemoveAt(idx);
        else offsets.Add(offset);
        EditorUtility.SetDirty(_data);
    }

    // ── 범례 ──────────────────────────────────────────────────────
    private void DrawLegend()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(8f);
            LegendItem(ColTotem,         "T", "토템 위치 (D3 고정)");
            GUILayout.Space(16f);
            LegendItem(ColEffect,        "",  "효과 범위");
            GUILayout.Space(16f);
            LegendItem(ColAttackDisable, "",  "공격 불가");
            GUILayout.Space(16f);
            LegendItem(ColEmpty,         "",  "비어있음");
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
        EditorGUILayout.LabelField(label, GUILayout.Width(110f));
    }

    // ── 액션 ──────────────────────────────────────────────────────
    private void DrawFooterActions()
    {
        DrawSeparator();
        GUILayout.Space(8f);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("저장 (디스크에 기록)", GUILayout.Width(170f), GUILayout.Height(30f)))
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[TotemEditor] '{_data.totemName}' 저장 완료 — " +
                          $"버프 {_data.functions.Count}개 / 효과 {_data.effectRanges.Count}개 / 공격불가 {_data.attackDisabledRanges.Count}개");
            }
        }
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
