using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 스테이지/웨이브/보스 편집 툴 창
///
/// [주요 메서드]
///   OnGUI()                 매 프레임 전체 UI 갱신, 드래그 고스트 렌더링, 드롭 처리
///   DrawLeftPanel()         BossBase 프리팹 자동 스캔 목록 + HP 입력
///   DrawRightPanel()        StageData 선택 + 웨이브 목록 + 선택 해제 감지
///   DrawBossSlots()         웨이브별 보스 슬롯 그리드 렌더링
///   DrawBossSlot()          개별 슬롯 렌더링 + 클릭(배치/HP수정/제거) 처리
///   HandleDragEnd()         MouseUp 시 드롭 위치 슬롯 감지 → PlaceBossToSlot
///   PlaceBossToSlot()       선택된 보스를 슬롯에 배치 + 선택 해제
///   ScanBossPrefabs()       프로젝트 내 BossBase 프리팹 전체 스캔 및 정렬
///
/// [문제 사항들]
///   문제1 — 슬롯 Rect 좌표 불일치
///     GUILayoutUtility.GetRect()는 스크롤뷰 내부 로컬 좌표를 반환
///     ev.mousePosition은 윈도우 전체 기준 좌표 → 직접 비교 시 항상 불일치
///     해결: 슬롯 Rect 캐시 시 GUIUtility.GUIToScreenPoint()로 스크린 좌표 변환
///           HandleDragEnd()에서도 mousePosition을 동일하게 스크린 좌표로 변환 후 비교
///
///   문제2 — 슬롯 Rect 캐시 타이밍
///     EventType.Repaint에서만 캐시하면 EventType.MouseUp과 같은 프레임에 오지 않아
///     _slotRects가 비어있는 상태로 드롭 감지 실패
///     해결: EventType.Layout 타이밍에 _slotRects.Clear(), 이후 모든 이벤트에서 갱신
///
///   문제3 — 클릭 선택과 드롭 배치 충돌
///     좌측 보스 선택 상태에서 우측 슬롯 클릭 시
///     DrawRightPanel()의 선택 해제 로직이 슬롯 클릭보다 먼저 실행되어 배치 불가
///     해결: 선택 해제는 슬롯 Rect 위가 아닐 때만 동작하도록 _slotRects로 필터링
/// </summary>
public class StageEditorWindow : EditorWindow
{
    // ── 상수 ──────────────────────────────────────────────────────
    private const float LEFT_PANEL_WIDTH = 200f;
    private const float SLOT_SIZE = 72f;
    private const float SLOT_SPACING = 4f;
    private const int MAX_BOSSES_PER_WAVE = 6;

    // ── 상태 ──────────────────────────────────────────────────────
    private StageData _stage;
    private int _selectedWave = -1;

    // 좌측 패널
    private List<GameObject> _bossPrefabs = new();
    private int _selectedBossIndex = -1;
    private int _pendingHp = 500;
    private Vector2 _leftScroll;
    private string _searchFilter = "";

    // 우측 패널
    private Vector2 _rightScroll;

    // 드래그 상태
    private bool _isDragging = false;
    private Vector2 _dragPos;
    private Texture2D _dragThumb;

    // 슬롯 Rect 캐시 (드롭 & 선택 해제 감지용)
    private Dictionary<(WaveData, int), Rect> _slotRects = new();

    // 썸네일 캐시
    private Dictionary<GameObject, Texture2D> _thumbCache = new();

    // ── 메뉴 ───────────────────────────────────────────────────────
    [MenuItem("Tools/Stage Editor")]
    public static void Open()
    {
        var win = GetWindow<StageEditorWindow>("Stage Editor");
        win.minSize = new Vector2(700f, 500f);
    }

    private void OnEnable() => ScanBossPrefabs();
    private void OnDisable() => _thumbCache.Clear();

    // 최상위 GUI
    private void OnGUI()
    {
        if (_isDragging)
        {
            _dragPos = Event.current.mousePosition;
            Repaint();
        }

        // 매 프레임 슬롯 Rect 갱신 (Layout 이벤트 타이밍에만 클리어)
        if (Event.current.type == EventType.Layout)
            _slotRects.Clear();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawLeftPanel();
            DrawVerticalSeparator();
            DrawRightPanel();
        }

        if (_isDragging)
            DrawDragGhost();

        HandleDragEnd();
    }

    // 좌측 패널 — 보스 프리팹 목록

    private void DrawLeftPanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(LEFT_PANEL_WIDTH)))
        {
            DrawLeftHeader();
            DrawBossFilter();
            DrawBossList();
            DrawHpInput();
        }
    }

    private void DrawLeftHeader()
    {
        DrawColorHeader("보스 프리팹", new Color(0.2f, 0.35f, 0.6f));

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"{_bossPrefabs.Count}개 검색됨", EditorStyles.miniLabel);
            if (GUILayout.Button("↺", GUILayout.Width(26f)))
            {
                ScanBossPrefabs();
                Repaint();
            }
        }

        EditorGUILayout.Space(2f);
    }

    private void DrawBossFilter()
    {
        EditorGUI.BeginChangeCheck();
        _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField);
        if (EditorGUI.EndChangeCheck()) Repaint();
    }

    private void DrawBossList()
    {
        _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll, GUILayout.ExpandHeight(true));

        for (int i = 0; i < _bossPrefabs.Count; i++)
        {
            var prefab = _bossPrefabs[i];
            if (prefab == null) continue;

            if (!string.IsNullOrEmpty(_searchFilter) &&
                !prefab.name.ToLower().Contains(_searchFilter.ToLower()))
                continue;

            DrawBossListItem(i, prefab);
        }

        if (_bossPrefabs.Count == 0)
            EditorGUILayout.HelpBox("BossBase 프리팹을 찾지 못했습니다.", MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    private void DrawBossListItem(int index, GameObject prefab)
    {
        bool isSelected = _selectedBossIndex == index;

        Color originalBg = GUI.backgroundColor;
        GUI.backgroundColor = isSelected
            ? new Color(0.4f, 0.7f, 1f, 1f)
            : new Color(0.25f, 0.25f, 0.25f, 1f);

        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.Height(44f)))
        {
            var thumb = GetThumbnail(prefab);
            if (thumb != null)
                GUILayout.Label(thumb, GUILayout.Width(36f), GUILayout.Height(36f));
            else
                GUILayout.Label("?", GUILayout.Width(36f), GUILayout.Height(36f));

            using (new EditorGUILayout.VerticalScope())
            {
                var nameStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
                EditorGUILayout.LabelField(prefab.name, nameStyle);

                var bossBase = prefab.GetComponent<BossBase>();
                int patternCount = bossBase?.Patterns?.Length ?? 0;
                var subStyle = new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
                EditorGUILayout.LabelField($"패턴 {patternCount}개", subStyle);
            }
        }

        var itemRect = GUILayoutUtility.GetLastRect();
        var ev = Event.current;

        if (!itemRect.Contains(ev.mousePosition))
        {
            GUI.backgroundColor = originalBg;
            return;
        }

        // 클릭 — 선택 토글
        if (ev.type == EventType.MouseDown && ev.button == 0)
        {
            _selectedBossIndex = isSelected ? -1 : index;
            GUI.FocusControl(null);

            if (_selectedBossIndex == index)
            {
                // 드래그 준비
                _dragThumb = GetThumbnail(prefab);
                _dragPos = ev.mousePosition;
            }

            ev.Use();
            Repaint();
        }

        // 드래그 시작
        if (ev.type == EventType.MouseDrag && ev.button == 0 && _selectedBossIndex == index)
        {
            if (!_isDragging)
            {
                _isDragging = true;
                _selectedBossIndex = index;
            }

            ev.Use();
        }

        GUI.backgroundColor = originalBg;
    }

    private void DrawHpInput()
    {
        EditorGUILayout.Space(4f);
        DrawSeparator();

        if (_selectedBossIndex < 0 || _selectedBossIndex >= _bossPrefabs.Count)
        {
            EditorGUILayout.LabelField("보스를 선택하거나 드래그하세요",
                EditorStyles.centeredGreyMiniLabel);
            return;
        }

        EditorGUILayout.LabelField(
            $"선택: {_bossPrefabs[_selectedBossIndex].name}", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("HP", GUILayout.Width(24f));
            _pendingHp = EditorGUILayout.IntField(_pendingHp);
        }

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField(
            _isDragging ? "슬롯에 드롭하세요" : "슬롯 클릭으로 배치",
            EditorStyles.centeredGreyMiniLabel);
    }

    // ════════════════════════════════════════════════════════════════
    // 우측 패널 — StageData + 웨이브 슬롯
    // ════════════════════════════════════════════════════════════════
    private void DrawRightPanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
        {
            DrawRightHeader();

            if (_stage == null)
            {
                EditorGUILayout.HelpBox("StageData SO를 선택하거나 새로 만드세요.", MessageType.Info);
                return;
            }

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
            DrawWaveList();
            EditorGUILayout.EndScrollView();

            DrawFooter();

            // 우측 빈 곳 클릭 → 선택 해제 (드래그 중이 아닐 때, 슬롯 위가 아닐 때)
            var ev = Event.current;
            if (!_isDragging &&
                ev.type == EventType.MouseDown && ev.button == 0 &&
                _selectedBossIndex >= 0)
            {
                bool onSlot = false;
                foreach (var r in _slotRects.Values)
                    if (r.Contains(ev.mousePosition))
                    {
                        onSlot = true;
                        break;
                    }

                if (!onSlot)
                {
                    _selectedBossIndex = -1;
                    GUI.FocusControl(null);
                    Repaint();
                }
            }
        }
    }

    private void DrawRightHeader()
    {
        DrawColorHeader("스테이지 편집", new Color(0.25f, 0.5f, 0.3f));

        using (new EditorGUILayout.HorizontalScope())
        {
            _stage = (StageData)EditorGUILayout.ObjectField(
                "StageData", _stage, typeof(StageData), false, GUILayout.ExpandWidth(true));

            if (GUILayout.Button("New", GUILayout.Width(46f)))
                CreateNewStageData();
        }

        if (_stage == null) return;

        EditorGUI.BeginChangeCheck();
        string newName = EditorGUILayout.TextField("Stage Name", _stage.stageName);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_stage, "Stage Name");
            _stage.stageName = newName;
            EditorUtility.SetDirty(_stage);
        }

        EditorGUILayout.Space(4f);
        DrawSeparator();
    }

    // ── 웨이브 목록 ───────────────────────────────────────────────
    private void DrawWaveList()
    {
        if (_stage.waves == null) _stage.waves = new WaveData[0];

        EditorGUILayout.Space(6f);

        for (int w = 0; w < _stage.waves.Length; w++)
            DrawWaveEntry(w);

        EditorGUILayout.Space(8f);

        if (GUILayout.Button("+ 웨이브 추가", GUILayout.Height(28f)))
            AddWave();
    }

    private void DrawWaveEntry(int waveIndex)
    {
        var wave = _stage.waves[waveIndex];

        using (new EditorGUILayout.HorizontalScope())
        {
            var headerRect = EditorGUILayout.GetControlRect(false, 24f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(headerRect, new Color(0.18f, 0.18f, 0.18f));

            string waveLabel = wave != null
                ? $"  Wave {waveIndex + 1}  [{wave.waveName}]  —  보스 {wave.bosses?.Length ?? 0}마리"
                : $"  Wave {waveIndex + 1}  [미연결]";

            bool isExpanded = _selectedWave == waveIndex;
            var labelStyle = new GUIStyle(EditorStyles.boldLabel)
                { normal = { textColor = isExpanded ? new Color(0.4f, 0.8f, 1f) : Color.white } };

            if (GUI.Button(headerRect, waveLabel, labelStyle))
                _selectedWave = isExpanded ? -1 : waveIndex;

            GUI.enabled = waveIndex > 0;
            if (GUILayout.Button("▲", GUILayout.Width(22f))) MoveWave(waveIndex, -1);
            GUI.enabled = waveIndex < _stage.waves.Length - 1;
            if (GUILayout.Button("▼", GUILayout.Width(22f))) MoveWave(waveIndex, 1);
            GUI.enabled = true;

            GUI.color = new Color(1f, 0.45f, 0.45f);
            if (GUILayout.Button("✕", GUILayout.Width(24f)))
            {
                RemoveWave(waveIndex);
                return;
            }

            GUI.color = Color.white;
        }

        if (_selectedWave != waveIndex) return;

        EditorGUI.BeginChangeCheck();
        var newWave = (WaveData)EditorGUILayout.ObjectField(
            "  WaveData SO", wave, typeof(WaveData), false);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_stage, "Assign WaveData");
            _stage.waves[waveIndex] = newWave;
            EditorUtility.SetDirty(_stage);
            wave = newWave;
        }

        if (wave == null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox("WaveData SO를 연결하거나 새로 만드세요.", MessageType.Warning);
                if (GUILayout.Button("생성", GUILayout.Width(50f)))
                {
                    var created = CreateWaveDataAsset(waveIndex + 1);
                    Undo.RecordObject(_stage, "Create WaveData");
                    _stage.waves[waveIndex] = created;
                    EditorUtility.SetDirty(_stage);
                }
            }

            return;
        }

        EditorGUI.BeginChangeCheck();
        string wn = EditorGUILayout.TextField("  Wave Name", wave.waveName);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(wave, "Wave Name");
            wave.waveName = wn;
            EditorUtility.SetDirty(wave);
        }

        EditorGUILayout.Space(6f);
        DrawBossSlots(wave);
        EditorGUILayout.Space(8f);
    }

    // ── 보스 슬롯 그리드 ──────────────────────────────────────────
    private void DrawBossSlots(WaveData wave)
    {
        if (wave.bosses == null) wave.bosses = new BossEntry[0];

        string slotHint;
        if (_isDragging)
            slotHint = "  슬롯에 드롭하여 배치";
        else if (_selectedBossIndex >= 0)
            slotHint = "  [배치 모드]  좌클릭: 배치 / 우클릭: 제거  —  빈 곳 클릭 시 선택 해제";
        else
            slotHint = "  [편집 모드]  좌클릭: HP 수정 / 우클릭: 제거  —  보스 선택/드래그 시 배치";

        EditorGUILayout.LabelField(slotHint, EditorStyles.miniLabel);
        EditorGUILayout.Space(2f);

        int slotCount = Mathf.Min(wave.bosses.Length + 1, MAX_BOSSES_PER_WAVE);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(8f);

            for (int i = 0; i < slotCount; i++)
            {
                bool hasEntry = i < wave.bosses.Length && wave.bosses[i] != null;
                DrawBossSlot(wave, i, hasEntry);
                GUILayout.Space(SLOT_SPACING);
            }

            GUILayout.FlexibleSpace();
        }
    }

    private void DrawBossSlot(WaveData wave, int slotIndex, bool hasEntry)
    {
        var slotRect = GUILayoutUtility.GetRect(SLOT_SIZE, SLOT_SIZE + 18f,
            GUILayout.Width(SLOT_SIZE));

        // 슬롯 Rect를 스크린 좌표로 변환하여 캐시
        // (스크롤뷰 내부 로컬 좌표 → 윈도우 전체 기준으로 통일)
        var screenMin = GUIUtility.GUIToScreenPoint(new Vector2(slotRect.x, slotRect.y));
        var screenMax = GUIUtility.GUIToScreenPoint(new Vector2(slotRect.xMax, slotRect.yMax));
        _slotRects[(wave, slotIndex)] = Rect.MinMaxRect(screenMin.x, screenMin.y, screenMax.x, screenMax.y);

        bool isDragHover = _isDragging && slotRect.Contains(_dragPos);

        Color bgColor = isDragHover
            ? new Color(0.3f, 0.6f, 0.3f)
            : hasEntry
                ? new Color(0.28f, 0.22f, 0.32f)
                : new Color(0.18f, 0.18f, 0.18f);

        EditorGUI.DrawRect(slotRect, bgColor);

        Color borderColor = isDragHover
            ? new Color(0.4f, 1f, 0.4f)
            : hasEntry
                ? new Color(0.6f, 0.4f, 0.8f)
                : new Color(0.35f, 0.35f, 0.35f);

        DrawBorder(slotRect, borderColor, isDragHover ? 2f : 1f);

        if (!hasEntry)
        {
            var plusStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 20 };
            EditorGUI.LabelField(
                new Rect(slotRect.x, slotRect.y + slotRect.height * 0.2f,
                    slotRect.width, slotRect.height * 0.6f),
                "+", plusStyle);
        }
        else
        {
            var entry = wave.bosses[slotIndex];

            if (entry.prefab != null)
            {
                var thumb = GetThumbnail(entry.prefab);
                if (thumb != null)
                    GUI.DrawTexture(
                        new Rect(slotRect.x + 4f, slotRect.y + 4f,
                            slotRect.width - 8f, slotRect.height - 22f),
                        thumb, ScaleMode.ScaleToFit);

                GUI.Label(
                    new Rect(slotRect.x, slotRect.y + slotRect.height - 28f, slotRect.width, 14f),
                    entry.prefab.name,
                    new GUIStyle(EditorStyles.miniLabel)
                        { alignment = TextAnchor.MiddleCenter, fontSize = 9 });
            }

            GUI.Label(
                new Rect(slotRect.x, slotRect.y + slotRect.height - 14f, slotRect.width, 14f),
                $"HP {entry.hp}",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.9f, 0.7f, 0.3f) },
                    fontSize = 9
                });
        }

        // ── 마우스 이벤트 ────────────────────────────────────────
        var ev = Event.current;
        if (!slotRect.Contains(ev.mousePosition)) return;

        if (ev.type == EventType.Repaint && !isDragHover)
            EditorGUI.DrawRect(slotRect, new Color(1f, 1f, 1f, 0.05f));

        if (ev.type == EventType.MouseDown)
        {
            if (ev.button == 0)
            {
                if (_selectedBossIndex >= 0)
                    PlaceBossToSlot(wave, slotIndex);
                else if (hasEntry)
                {
                    var entry = wave.bosses[slotIndex];
                    entry.hp = EditorInputDialog.Show(
                        "HP 수정", $"{entry.prefab?.name} HP", entry.hp);
                    EditorUtility.SetDirty(wave);
                }
            }
            else if (ev.button == 1 && hasEntry)
                RemoveBossFromSlot(wave, slotIndex);

            ev.Use();
            Repaint();
        }
    }

    // ── 드래그 고스트 렌더링 ──────────────────────────────────────
    private void DrawDragGhost()
    {
        if (_selectedBossIndex < 0 || _selectedBossIndex >= _bossPrefabs.Count) return;

        const float ghostSize = 56f;
        var ghostRect = new Rect(
            _dragPos.x - ghostSize * 0.5f,
            _dragPos.y - ghostSize * 0.5f,
            ghostSize, ghostSize);

        EditorGUI.DrawRect(ghostRect, new Color(0.2f, 0.2f, 0.6f, 0.75f));
        DrawBorder(ghostRect, new Color(0.5f, 0.7f, 1f), 2f);

        if (_dragThumb != null)
            GUI.DrawTexture(
                new Rect(ghostRect.x + 4f, ghostRect.y + 4f,
                    ghostRect.width - 8f, ghostRect.height - 8f),
                _dragThumb, ScaleMode.ScaleToFit,
                true, 0f, new Color(1f, 1f, 1f, 0.85f), 0f, 0f);

        GUI.Label(
            new Rect(ghostRect.x, ghostRect.yMax + 2f, ghostRect.width, 14f),
            _bossPrefabs[_selectedBossIndex].name,
            new GUIStyle(EditorStyles.miniLabel)
                { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white }, fontSize = 9 });
    }

    // ── 드래그 종료 처리 ──────────────────────────────────────────
    private void HandleDragEnd()
    {
        if (!_isDragging) return;

        var ev = Event.current;
        if (ev.type != EventType.MouseUp) return;

        var screenMousePos = GUIUtility.GUIToScreenPoint(ev.mousePosition);
        foreach (var kvp in _slotRects)
        {
            if (!kvp.Value.Contains(screenMousePos)) continue;
            var (wave, slotIndex) = kvp.Key;
            PlaceBossToSlot(wave, slotIndex);
            break;
        }

        _isDragging = false;
        _dragThumb = null;

        ev.Use();
        Repaint();
    }

    // ── 슬롯 조작 ─────────────────────────────────────────────────
    private void PlaceBossToSlot(WaveData wave, int slotIndex)
    {
        int bossIdx = _selectedBossIndex;
        if (bossIdx < 0 || bossIdx >= _bossPrefabs.Count) return;

        Undo.RecordObject(wave, "Place Boss");

        if (slotIndex >= wave.bosses.Length)
            System.Array.Resize(ref wave.bosses, slotIndex + 1);

        wave.bosses[slotIndex] = new BossEntry
        {
            prefab = _bossPrefabs[bossIdx],
            hp = _pendingHp
        };

        EditorUtility.SetDirty(wave);
        _selectedBossIndex = -1;
    }

    private void RemoveBossFromSlot(WaveData wave, int slotIndex)
    {
        if (slotIndex >= wave.bosses.Length) return;

        Undo.RecordObject(wave, "Remove Boss");
        var list = new List<BossEntry>(wave.bosses);
        list.RemoveAt(slotIndex);
        wave.bosses = list.ToArray();
        EditorUtility.SetDirty(wave);
    }

    // ── Footer ────────────────────────────────────────────────────
    private void DrawFooter()
    {
        DrawSeparator();
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(
                _stage != null
                    ? $"{_stage.stageName}  |  총 {_stage.waves?.Length ?? 0}웨이브"
                    : "",
                EditorStyles.miniLabel);

            if (GUILayout.Button("Save All", GUILayout.Width(76f)))
                AssetDatabase.SaveAssets();
        }
    }

    // ════════════════════════════════════════════════════════════════
    // 보스 프리팹 스캔
    // ════════════════════════════════════════════════════════════════
    private void ScanBossPrefabs()
    {
        _bossPrefabs.Clear();
        _thumbCache.Clear();

        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            if (go.GetComponent<BossBase>() != null)
                _bossPrefabs.Add(go);
        }

        _bossPrefabs.Sort((a, b) =>
            string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
    }

    // ════════════════════════════════════════════════════════════════
    // 유틸
    // ════════════════════════════════════════════════════════════════
    private Texture2D GetThumbnail(GameObject prefab)
    {
        if (prefab == null) return null;
        if (_thumbCache.TryGetValue(prefab, out var cached)) return cached;
        var thumb = AssetPreview.GetAssetPreview(prefab) ?? AssetPreview.GetMiniThumbnail(prefab);
        _thumbCache[prefab] = thumb;
        return thumb;
    }

    private static void DrawColorHeader(string title, Color color)
    {
        var rect = EditorGUILayout.GetControlRect(false, 22f);
        EditorGUI.DrawRect(rect, color);
        EditorGUI.LabelField(rect, title, new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = Color.white },
            padding = new RectOffset(8, 0, 0, 0),
            fontSize = 12
        });
        EditorGUILayout.Space(2f);
    }

    private static void DrawSeparator()
    {
        var rect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(rect, new Color(0.35f, 0.35f, 0.35f));
    }

    private static void DrawVerticalSeparator()
    {
        var rect = GUILayoutUtility.GetRect(1f, float.MaxValue,
            GUILayout.Width(1f), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(rect, new Color(0.35f, 0.35f, 0.35f));
    }

    private static void DrawBorder(Rect rect, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    // ── SO 생성 ──────────────────────────────────────────────────
    private void CreateNewStageData()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "StageData 생성", "StageData", "asset", "저장 위치를 선택하세요");
        if (string.IsNullOrEmpty(path)) return;

        var so = CreateInstance<StageData>();
        so.stageName = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(so, path);
        AssetDatabase.SaveAssets();
        _stage = so;
    }

    private WaveData CreateWaveDataAsset(int waveNumber)
    {
        string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(_stage)) ?? "Assets";
        string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/WaveData_Wave{waveNumber}.asset");

        var so = CreateInstance<WaveData>();
        so.waveName = $"Wave {waveNumber}";
        AssetDatabase.CreateAsset(so, path);
        AssetDatabase.SaveAssets();
        return so;
    }

    // ── 웨이브 조작 ──────────────────────────────────────────────
    private void AddWave()
    {
        Undo.RecordObject(_stage, "Add Wave");
        var list = new List<WaveData>(_stage.waves ?? new WaveData[0]);
        list.Add(null);
        _stage.waves = list.ToArray();
        _selectedWave = list.Count - 1;
        EditorUtility.SetDirty(_stage);
    }

    private void RemoveWave(int index)
    {
        if (!EditorUtility.DisplayDialog("웨이브 삭제",
                $"Wave {index + 1}을 삭제하시겠습니까?", "삭제", "취소")) return;

        Undo.RecordObject(_stage, "Remove Wave");
        var list = new List<WaveData>(_stage.waves);
        list.RemoveAt(index);
        _stage.waves = list.ToArray();
        _selectedWave = Mathf.Clamp(_selectedWave, -1, list.Count - 1);
        EditorUtility.SetDirty(_stage);
    }

    private void MoveWave(int index, int dir)
    {
        int target = index + dir;
        if (target < 0 || target >= _stage.waves.Length) return;

        Undo.RecordObject(_stage, "Move Wave");
        (_stage.waves[index], _stage.waves[target]) = (_stage.waves[target], _stage.waves[index]);
        _selectedWave = target;
        EditorUtility.SetDirty(_stage);
    }
}


/// <summary>HP 수정용 미니 다이얼로그</summary>
public class EditorInputDialog : EditorWindow
{
    private string             _label;
    private int                _value;
    private System.Action<int> _onConfirm;

    public static int Show(string title, string label, int defaultValue)
    {
        int result = defaultValue;
        var win = CreateInstance<EditorInputDialog>();
        win.titleContent = new GUIContent(title);
        win._label       = label;
        win._value       = defaultValue;
        win.minSize      = new Vector2(240f, 80f);
        win.maxSize      = new Vector2(240f, 80f);
        win._onConfirm   = v => result = v;
        win.ShowModal();
        return result;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(_label, EditorStyles.boldLabel);
        _value = EditorGUILayout.IntField("HP", _value);
        EditorGUILayout.Space(6f);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("확인")) { _onConfirm?.Invoke(_value); Close(); }
            if (GUILayout.Button("취소")) Close();
        }
    }
}
