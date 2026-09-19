using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

/// <summary>
/// 토템 범위 편집 툴 — "그냥 그리드를 클릭하면 바로 반영"되는 단순한 흐름이다.
///
/// - 효과가 1개(EffectGroups 없음)면 그리드 한 개만 뜨고, 그냥 클릭하면 바로 반영된다.
/// - "+ 효과 추가"를 누르면 효과 그룹이 하나 늘고, 그 개수만큼 그리드가 그대로 늘어난다
///   (그룹 2개면 그리드 2개, 각자 자기 색으로 독립적으로 바로 클릭 가능). 타입을 고르거나
///   "그리드에서 편집"을 누르는 절차 없음 — 그룹 수 = 그리드 수.
/// - 각 그리드 위에는 그 효과 전용 버프 추가 UI가 같이 있다.
/// - 공격 불가 범위는 효과와 별개로 항상 그 아래 그리드 하나로 편집한다.
/// - "고급 설정"(기본 접힘)에는 그리드 클릭이 안 통하는 특수 범위 타입(고정/특정 방향/사각 고리)만
///   따로 추가·편집할 수 있다. 평소엔 열 필요 없음.
/// </summary>
public class TotemEditorWindow : EditorWindow
{
    // ── 그리드 상수 ────────────────────────────────────────────────
    private const int   Cols     = 6;
    private const int   Rows     = 6;
    private const float CellSize = 56f;
    private const float LabelW   = 24f;

    private static readonly Vector2Int TotemCell = new Vector2Int(3, 3);
    private static readonly string[] ColLabels = { "A", "B", "C", "D", "E", "F" };
    private static readonly string[] RowLabels = { "1", "2", "3", "4", "5", "6" };

    private static readonly Color ColTotem         = new Color(0x92 / 255f, 0xD0 / 255f, 0x50 / 255f);
    private static readonly Color ColEffect        = new Color(0xA2 / 255f, 0x00 / 255f, 0x00 / 255f);
    private static readonly Color ColAttackDisable = Color.black;
    private static readonly Color ColEmpty         = new Color(0.20f, 0.20f, 0.20f);
    private static readonly Color ColEmptyHover    = new Color(0.30f, 0.30f, 0.30f);

    private TotemData  _data;
    private Vector2    _scroll;
    private Vector2Int _hovered = new Vector2Int(-1, -1);
    private bool        _showAdvanced;

    /// <summary>지금 그려지고 있는 그리드가 클릭될 때 어느 range 리스트의 오프셋을 토글할지.
    /// 그리드를 그릴 때마다 그 그리드 전용으로 세팅한 뒤 바로 입력 처리를 하므로, 여러 그리드가
    /// 한 화면에 동시에 떠 있어도 서로 안 꼬인다(각 그리드는 자기 rect 안 클릭만 반응).</summary>
    private ITotemRange _activeRange;
    /// <summary>고급 설정에서 "그리드에서 편집"으로 명시적으로 활성화했는지.</summary>
    private bool _advancedRangeActive;

    private GUIStyle _centerStyle;
    private GUIStyle CenterStyle => _centerStyle ??= new GUIStyle(EditorStyles.label)
        { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

    // ── 메뉴 ──────────────────────────────────────────────────────
    [MenuItem("Tools/GGD_Editor/Totem Editor", false, 0)]
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
            DrawEffectSection();

            GUILayout.Space(14f);
            DrawAttackDisabledSection();

            GUILayout.Space(10f);
            DrawLegend();

            GUILayout.Space(14f);
            _showAdvanced = EditorGUILayout.Foldout(
                _showAdvanced, "고급 설정 (특수 범위 타입 — 고정 · 특정 방향 · 사각 고리)", true, EditorStyles.foldoutHeader);
            if (_showAdvanced)
            {
                GUILayout.Space(6f);
                DrawRangeSection("효과 범위 (effectRanges) — 효과가 1개(그룹 없음)일 때만 사용됨", _data.effectRanges);
                GUILayout.Space(10f);
                DrawRangeSection("공격 불가 범위 (attackDisabledRanges)", _data.attackDisabledRanges);
                GUILayout.Space(6f);
                EditorGUILayout.HelpBox(
                    "효과 그룹 하나의 특수 범위 타입이 필요하면 기본 인스펙터에서 그 그룹의 Ranges를 직접 편집하세요.",
                    MessageType.None);
            }

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
            _data                 = newData;
            _activeRange          = null;
            _advancedRangeActive  = false;
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

    // ── 효과 — 그룹 수만큼 그리드가 그대로 늘어남, 항상 바로 클릭 가능 ──
    private static readonly Color[] GroupPalette =
    {
        new Color(0xA2 / 255f, 0x00 / 255f, 0x00 / 255f), // 빨강
        new Color(0x00 / 255f, 0x5A / 255f, 0xA2 / 255f), // 파랑
        new Color(0x2E / 255f, 0x8B / 255f, 0x2E / 255f), // 초록
        new Color(0xB8 / 255f, 0x86 / 255f, 0x00 / 255f), // 황토
    };

    private void DrawEffectSection()
    {
        EditorGUILayout.LabelField("효과", EditorStyles.boldLabel);
        DrawSeparator();
        GUILayout.Space(4f);

        // 그룹(효과) 별 이름/색/버프. 범위는 아래 그리드 하나에서 전부 같이 편집한다.
        if (_data.EffectGroups.Count == 0)
        {
            DrawFunctionsSection("버프", _data.functions);
        }
        else
        {
            for (int i = 0; i < _data.EffectGroups.Count; i++)
            {
                DrawGroupHeaderAndBuffs(i);
                GUILayout.Space(6f);
            }
        }

        GUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ 효과 추가", GUILayout.Width(100f)))
                AddEffectGroup();

            if (_data.EffectGroups.Count > 0 && GUILayout.Button("− 마지막 효과 제거", GUILayout.Width(140f)))
            {
                Undo.RecordObject(_data, "Remove Effect Group");
                _data.EffectGroups.RemoveAt(_data.EffectGroups.Count - 1);
                EditorUtility.SetDirty(_data);
            }
        }

        GUILayout.Space(6f);

        string cycleHint = _data.EffectGroups.Count <= 1
            ? "아래 그리드를 클릭하면 바로 반영됩니다."
            : "아래 그리드를 클릭할 때마다 효과가 순서대로 바뀝니다 (없음 → 효과1 색 → 효과2 색 → ... → 없음).";
        EditorGUILayout.HelpBox(cycleHint, MessageType.None);
        GUILayout.Space(4f);

        DrawCombinedEffectGrid();
    }

    /// <summary>효과 그룹 하나의 이름/색/버프만. 범위는 여기서 안 다루고 아래 공용 그리드에서 다룬다.</summary>
    private void DrawGroupHeaderAndBuffs(int index)
    {
        var group = _data.EffectGroups[index];
        if (group == null) return;

        using (new EditorGUILayout.HorizontalScope())
        {
            var swatch = GUILayoutUtility.GetRect(16f, 16f, GUILayout.Width(16f), GUILayout.Height(16f));
            EditorGUI.DrawRect(swatch, group.Color);
            GUILayout.Space(4f);

            EditorGUI.BeginChangeCheck();
            var color = EditorGUILayout.ColorField(group.Color, GUILayout.Width(50f));
            var label = EditorGUILayout.TextField(group.Label);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_data, "Edit Effect Group");
                group.Color = color;
                group.Label = label;
                EditorUtility.SetDirty(_data);
            }

            if (GUILayout.Button("삭제", GUILayout.Width(50f)))
            {
                Undo.RecordObject(_data, "Remove Effect Group");
                _data.EffectGroups.RemoveAt(index);
                EditorUtility.SetDirty(_data);
                GUIUtility.ExitGUI();
            }
        }

        GUILayout.Space(4f);
        string groupTitle = string.IsNullOrEmpty(group.Label) ? $"효과 {index + 1}" : group.Label;
        DrawFunctionsSection($"버프 ({groupTitle})", group.Functions);
    }

    private void AddEffectGroup()
    {
        Undo.RecordObject(_data, "Add Effect Group");

        // 첫 그룹을 추가하는 거면, 기존 top-level 효과(functions/effectRanges)가 사라지지 않게
        // 그대로 그룹 1로 옮긴다 — 그룹이 하나라도 생기면 게임플레이가 top-level을 더 이상 안 보기 때문.
        if (_data.EffectGroups.Count == 0 && (_data.functions.Count > 0 || _data.effectRanges.Count > 0))
        {
            var migrated = new TotemEffectGroup { Label = "효과 1", Color = GroupPalette[0] };
            migrated.Functions.AddRange(_data.functions);
            migrated.Ranges.AddRange(_data.effectRanges);
            _data.EffectGroups.Add(migrated);
            _data.functions.Clear();
            _data.effectRanges.Clear();
        }

        var newColor = GroupPalette[_data.EffectGroups.Count % GroupPalette.Length];
        _data.EffectGroups.Add(new TotemEffectGroup { Label = $"효과 {_data.EffectGroups.Count + 1}", Color = newColor });
        EditorUtility.SetDirty(_data);
    }

    /// <summary>효과가 여러 개일 때 클릭 대상이 되는 range 리스트들. 그룹이 없으면 effectRanges
    /// 하나, 있으면 그룹 순서대로 각 그룹의 Ranges.</summary>
    private List<List<ITotemRange>> GetEffectTargets()
    {
        var targets = new List<List<ITotemRange>>();
        if (_data.EffectGroups.Count == 0)
        {
            targets.Add(_data.effectRanges);
        }
        else
        {
            foreach (var g in _data.EffectGroups)
                targets.Add(g?.Ranges);
        }
        return targets;
    }

    private Color GetEffectTargetColor(int index)
        => _data.EffectGroups.Count == 0 ? ColEffect : (_data.EffectGroups[index]?.Color ?? ColEffect);

    /// <summary>모든 효과(그룹 없으면 1개, 있으면 N개)를 한 그리드에 겹쳐 보여준다. 셀을 클릭할
    /// 때마다 "없음 → 효과1 → 효과2 → ... → 없음" 순서로 그 칸의 소속이 바뀐다.</summary>
    private void DrawCombinedEffectGrid()
    {
        var targets = GetEffectTargets();

        var display = new Color?[Cols, Rows];
        for (int t = 0; t < targets.Count; t++)
        {
            if (targets[t] == null) continue;
            PaintOffsets(display, CollectPreviewOffsets(targets[t]), GetEffectTargetColor(t));
        }
        display[TotemCell.x, TotemCell.y] = ColTotem;

        DrawGridWidget(display, CellSize, true, CycleEffectClick);
    }

    private void CycleEffectClick(int col, int row)
    {
        var offset  = new Vector2Int(col - TotemCell.x, row - TotemCell.y);
        var targets = GetEffectTargets();

        int currentIndex = -1;
        for (int t = 0; t < targets.Count; t++)
        {
            if (FindOffsetRangeContaining(targets[t], offset) != null) { currentIndex = t; break; }
        }

        Undo.RecordObject(_data, "Cycle Effect Cell");

        if (currentIndex >= 0)
        {
            var current = FindOffsetRangeContaining(targets[currentIndex], offset);
            current.offsets.Remove(offset);
        }

        int nextIndex = currentIndex + 1;
        if (nextIndex < targets.Count && targets[nextIndex] != null)
        {
            var next = EnsureQuickRangeIn(targets[nextIndex]) as TotemRelativeOffsetRange;
            if (next != null && !next.offsets.Contains(offset)) next.offsets.Add(offset);
        }

        EditorUtility.SetDirty(_data);
    }

    private static TotemRelativeOffsetRange FindOffsetRangeContaining(List<ITotemRange> list, Vector2Int offset)
    {
        if (list == null) return null;
        foreach (var r in list)
            if (r is TotemRelativeOffsetRange rel && rel.offsets.Contains(offset))
                return rel;
        return null;
    }

    private static List<Vector2Int> CollectPreviewOffsets(List<ITotemRange> ranges)
    {
        var result = new List<Vector2Int>();
        if (ranges == null) return result;
        foreach (var r in ranges)
            if (r != null) foreach (var o in r.GetPreviewOffsets())
                if (!result.Contains(o)) result.Add(o);
        return result;
    }

    // ── 공격 불가 범위 — 효과와 별개, 항상 그리드 하나로 바로 클릭 가능 ──
    private void DrawAttackDisabledSection()
    {
        EditorGUILayout.LabelField("공격 불가 범위", EditorStyles.boldLabel);
        DrawSeparator();
        GUILayout.Space(4f);
        EditorGUILayout.HelpBox("아래 그리드를 그냥 클릭하면 바로 반영됩니다.", MessageType.None);
        GUILayout.Space(4f);

        if (!_advancedRangeActive)
            _activeRange = EnsureQuickRangeIn(_data.attackDisabledRanges);

        var display = new Color?[Cols, Rows];
        PaintOffsets(display, _data.GetAttackDisabledPreviewOffsets(), ColAttackDisable);
        display[TotemCell.x, TotemCell.y] = ColTotem;
        DrawGridWidget(display, CellSize, true, (c, r) => ToggleActiveOffset(c, r));
    }

    /// <summary>주어진 리스트에서 클릭 편집용 TotemRelativeOffsetRange를 찾고, 없으면 하나 만들어
    /// 넣는다. 다른 특수 타입(고정/방향/고리)은 그대로 둔다.</summary>
    private ITotemRange EnsureQuickRangeIn(List<ITotemRange> list)
    {
        foreach (var r in list)
            if (r is TotemRelativeOffsetRange) return r;

        var created = new TotemRelativeOffsetRange();
        Undo.RecordObject(_data, "Add Range");
        list.Add(created);
        EditorUtility.SetDirty(_data);
        return created;
    }

    // ── 버프 (functions) ────────────────────────────────────────
    private void DrawFunctionsSection(string label, List<ITotemFunction> functions)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        DrawSeparator();
        GUILayout.Space(4f);

        for (int i = 0; i < functions.Count; i++)
            DrawFunctionEntry(functions, i);

        if (GUILayout.Button("+ 버프 추가 ▾", GUILayout.Width(140f)))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("단순 버프"), false, () => AddFunction(functions, new SimpleBuffFunction()));
            menu.AddItem(new GUIContent("조건부 버프"), false, () => AddFunction(functions, new ConditionalBuffFunction { condition = new PositionThresholdCondition() }));
            menu.AddItem(new GUIContent("식량 생성 버프"), false, () => AddFunction(functions, new FoodGeneratorFunction()));
            menu.ShowAsContext();
        }
    }

    private void AddFunction(List<ITotemFunction> functions, ITotemFunction fn)
    {
        Undo.RecordObject(_data, "Add Buff");
        functions.Add(fn);
        EditorUtility.SetDirty(_data);
    }

    private void DrawFunctionEntry(List<ITotemFunction> functions, int index)
    {
        var fn = functions[index];

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{index + 1}. {FunctionTypeName(fn)}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("삭제", GUILayout.Width(50f)))
                {
                    Undo.RecordObject(_data, "Remove Buff");
                    functions.RemoveAt(index);
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
            var kind        = (StatKind)EditorGUILayout.EnumPopup(simple.kind, GUILayout.Width(100f));
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

    // ── 범위 (effectRanges / attackDisabledRanges — 특수 타입 전용, 고급 설정) ────
    private void DrawRangeSection(string label, List<ITotemRange> ranges)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        DrawSeparator();
        GUILayout.Space(4f);

        for (int i = 0; i < ranges.Count; i++)
            DrawRangeEntry(ranges, i);

        if (GUILayout.Button("+ 범위 추가 ▾", GUILayout.Width(140f)))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("토템 기준 지정"), false, () => AddRange(ranges, new TotemRelativeOffsetRange()));
            menu.AddItem(new GUIContent("고정"), false, () => AddRange(ranges, new FixedOffsetRange()));
            menu.AddItem(new GUIContent("특정 방향으로 쭉"), false, () => AddRange(ranges, new DirectionalLineRange()));
            menu.AddItem(new GUIContent("사각 고리 범위 (거리 기준)"), false, () => AddRange(ranges, new TotemSquareRingRange()));
            menu.ShowAsContext();
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
        var  range         = ranges[index];
        bool isActive      = ReferenceEquals(_activeRange, range) && _advancedRangeActive;
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
                    string btnLabel = isActive ? "그리드 편집 종료 (위 빠른 편집으로 복귀)" : "이 항목을 그리드에서 편집";
                    if (GUILayout.Button(btnLabel, GUILayout.Width(220f)))
                    {
                        if (isActive) { _activeRange = null; _advancedRangeActive = false; }
                        else          { _activeRange = range; _advancedRangeActive = true; }
                    }
                }

                if (GUILayout.Button("삭제", GUILayout.Width(50f)))
                {
                    Undo.RecordObject(_data, "Remove Range");
                    if (isActive) { _activeRange = null; _advancedRangeActive = false; }
                    ranges.RemoveAt(index);
                    EditorUtility.SetDirty(_data);
                    GUIUtility.ExitGUI();
                }
            }

            if (range is DirectionalLineRange dir)
                DrawDirectionalFields(dir);
            else if (range is TotemSquareRingRange ring)
                DrawSquareRingFields(ring);
            else if (isActive)
                EditorGUILayout.HelpBox("위 '범위 편집' 그리드를 클릭해 이 항목의 칸을 추가/제거하세요.", MessageType.None);
        }

        GUILayout.Space(2f);
    }

    private static string RangeTypeName(ITotemRange range) => range switch
    {
        TotemRelativeOffsetRange => "토템 기준 지정",
        FixedOffsetRange         => "고정",
        DirectionalLineRange     => "특정 방향으로 쭉",
        TotemSquareRingRange     => "사각 고리 범위 (거리 기준)",
        _                        => range?.GetType().Name ?? "None"
    };

    private static string RangeSummary(ITotemRange range) => range switch
    {
        TotemRelativeOffsetRange rel       => $"{rel.offsets.Count}칸",
        FixedOffsetRange         fixedRng  => $"{fixedRng.offsets.Count}칸",
        DirectionalLineRange     dir       => $"{dir.direction} 방향 끝까지{(dir.rotationAware ? "" : " (회전 무시)")}",
        TotemSquareRingRange     ring      => ring.MinRadius <= 0
            ? $"중심 포함, 거리 {ring.MaxRadius}까지"
            : $"거리 {ring.MinRadius}~{ring.MaxRadius} (중심 제외)",
        _                                  => ""
    };

    /// <summary>중심(토템)에서 체비쇼프 거리로 정의되는 사각 고리 범위. 그리드 클릭이 아니라
    /// 숫자 두 개(MinRadius/MaxRadius)로 편집한다 — 거리 1은 인접 8칸, 거리 2는 그 바깥 16칸.</summary>
    private void DrawSquareRingFields(TotemSquareRingRange ring)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("최소 거리", GUILayout.Width(55f));
            var minRadius = Mathf.Max(0, EditorGUILayout.IntField(ring.MinRadius, GUILayout.Width(40f)));

            GUILayout.Space(6f);

            EditorGUILayout.LabelField("최대 거리", GUILayout.Width(55f));
            var maxRadius = Mathf.Max(minRadius, EditorGUILayout.IntField(ring.MaxRadius, GUILayout.Width(40f)));

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_data, "Edit Square Ring Range");
                ring.MinRadius = minRadius;
                ring.MaxRadius = maxRadius;
                EditorUtility.SetDirty(_data);
            }
        }
        EditorGUILayout.HelpBox("최소 거리 0 = 중심 칸 포함. 거리 1 = 인접 8칸, 거리 2 = 그 바깥 16칸 (그리드 클릭 대신 숫자로 편집).", MessageType.None);
    }

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

    // ── 그리드 그리기/입력 공용 ────────────────────────────────────
    private void DrawGridWidget(Color?[,] display, float cellSize, bool showAxisLabels, Action<int, int> onCellClick)
    {
        float labelW  = showAxisLabels ? LabelW : 0f;
        float totalW  = labelW + Cols * cellSize;
        float totalH  = labelW + Rows * cellSize;

        Rect baseRect = GUILayoutUtility.GetRect(totalW, totalH);
        if (showAxisLabels)
            baseRect.x = Mathf.Max(8f, (EditorGUIUtility.currentViewWidth - totalW) * 0.5f);

        HandleGridInput(baseRect, cellSize, labelW, onCellClick);

        if (showAxisLabels)
        {
            for (int col = 0; col < Cols; col++)
            {
                var r = new Rect(baseRect.x + labelW + col * cellSize, baseRect.y, cellSize, labelW);
                EditorGUI.LabelField(r, ColLabels[col], CenterStyle);
            }
        }

        for (int row = 0; row < Rows; row++)
        {
            float y = baseRect.y + labelW + row * cellSize;
            if (showAxisLabels)
                EditorGUI.LabelField(new Rect(baseRect.x, y, labelW, cellSize), RowLabels[row], CenterStyle);

            for (int col = 0; col < Cols; col++)
            {
                float x        = baseRect.x + labelW + col * cellSize;
                var   cellRect = new Rect(x + 1f, y + 1f, cellSize - 2f, cellSize - 2f);
                bool  isTotem  = col == TotemCell.x && row == TotemCell.y;
                bool  isHov    = _hovered.x == col && _hovered.y == row;

                DrawCell(cellRect, display[col, row], isTotem, isHov);
            }
        }
    }

    private static void PaintOffsets(Color?[,] display, List<Vector2Int> offsets, Color color)
    {
        foreach (var offset in offsets)
        {
            int col = TotemCell.x + offset.x;
            int row = TotemCell.y + offset.y;
            if (col >= 0 && col < Cols && row >= 0 && row < Rows) display[col, row] = color;
        }
    }

    private void DrawCell(Rect rect, Color? cellColor, bool isTotem, bool isHover)
    {
        Color bg = cellColor ?? (isHover && !isTotem ? ColEmptyHover : ColEmpty);

        EditorGUI.DrawRect(rect, bg);

        Color border = isTotem
            ? new Color(1f, 1f, 1f, 0.5f)
            : cellColor.HasValue ? new Color(1f, 1f, 1f, 0.12f) : new Color(0.38f, 0.38f, 0.38f);
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

    private void HandleGridInput(Rect baseRect, float cellSize, float labelW, Action<int, int> onCellClick)
    {
        var ev = Event.current;
        if (ev.type == EventType.Layout) return;

        _hovered = new Vector2Int(-1, -1);

        for (int row = 0; row < Rows; row++)
        for (int col = 0; col < Cols; col++)
        {
            float x = baseRect.x + labelW + col * cellSize + 1f;
            float y = baseRect.y + labelW + row * cellSize + 1f;
            var   r = new Rect(x, y, cellSize - 2f, cellSize - 2f);

            if (!r.Contains(ev.mousePosition)) continue;

            bool isTotem = col == TotemCell.x && row == TotemCell.y;
            if (!isTotem) _hovered = new Vector2Int(col, row);

            if (!isTotem && ev.type == EventType.MouseDown && ev.button == 0)
            {
                onCellClick?.Invoke(col, row);
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
            LegendItem(ColTotem,         "T", "토템 위치");
            GUILayout.Space(16f);
            LegendItem(ColEffect,        "",  "효과 범위 (그룹 있으면 그룹별 색상)");
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
        EditorGUILayout.LabelField(label, GUILayout.Width(160f));
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
                          $"버프 {_data.functions.Count}개 / 효과 {_data.effectRanges.Count}개 / " +
                          $"공격불가 {_data.attackDisabledRanges.Count}개");
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
