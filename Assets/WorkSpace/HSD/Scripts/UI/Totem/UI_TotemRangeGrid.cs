using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

namespace GaeGGUL.UI.Totem
{
    /// <summary>
    /// 토템의 효과 범위를 보여주는 그리드 (명일방주식 공격 범위 표시).
    /// - 토템 자신의 칸은 꽉 채운 단색, 적용 범위 칸만 그룹 색 테두리 + 반투명 채움으로 그린다. 범위 밖 칸은 그리지 않는다.
    /// - 칸 크기는 TotemDisplaySettings.maxCellSize를 넘지 않는다. 범위가 박스를 넘칠 때만 줄어들고, 항상 박스 가운데 정렬.
    /// - 그룹 색은 게임플레이와 같은 TotemData.EffectGroups 색을 쓰며, 어두운 색은 테두리에서만 밝기를 보정한다.
    /// - 그리드 아래에 캡션("적용 범위")을 런타임에 만들어 표시한다. 범위가 없으면 박스 가운데에 "범위 없음"을 표시한다.
    /// </summary>
    public class UI_TotemRangeGrid : MonoBehaviour
    {
        private enum CellKind { Totem, Range }

        private struct Entry
        {
            public Vector2Int Offset;
            public CellKind Kind;
            public Color Color;
        }

        [Header("Settings")]
        [SerializeField] private TotemDisplaySettings settings;
        [SerializeField] private UI_TotemRangeCell cellPrefab;
        [SerializeField] private UIGridLayout gridLayout;

        [Header("Legend Settings")]
        [SerializeField] private UI_TotemLegendSlot legendSlotPrefab;
        [SerializeField] private Transform tr_LegendParent;

        private readonly List<UI_TotemRangeCell> _cells = new List<UI_TotemRangeCell>();
        private readonly List<Entry> _entries = new List<Entry>();
        private TextMeshProUGUI _caption;
        private TextMeshProUGUI _noRangeLabel;
        private bool _legendReady;

        private void OnEnable()
        {
            if (gridLayout != null)
            {
                gridLayout.OnLayoutChanged -= RefreshLayoutPositions;
                gridLayout.OnLayoutChanged += RefreshLayoutPositions;
            }
        }

        private void OnDisable()
        {
            if (gridLayout != null)
            {
                gridLayout.OnLayoutChanged -= RefreshLayoutPositions;
            }
        }

        /// <summary>데이터 없이 토템 칸만 보여주는 미리보기 상태로 준비합니다.</summary>
        public void Initialize()
        {
            if (settings == null)
            {
                Debug.LogError("[TotemUI] TotemDisplaySettings is missing.");
                return;
            }
            EnsureLegend();
            _entries.Clear();
            _entries.Add(new Entry { Offset = Vector2Int.zero, Kind = CellKind.Totem, Color = settings.totemCellColor });
            Render();
        }

        /// <summary>효과/디버프 오프셋만으로 범위를 표시합니다 (그룹 색 없음).</summary>
        public void SetRange(List<Vector2Int> effectRange, List<Vector2Int> debuffRange)
        {
            var map = new Dictionary<Vector2Int, Color>();
            if (settings != null)
            {
                AddOffsets(map, effectRange, settings.defaultRangeColor);
                AddOffsets(map, debuffRange, settings.debuffRangeColor);
            }
            ShowRange(map);
        }

        /// <summary>Shows effect-specific colors from the same SO used by gameplay.</summary>
        public void SetData(TotemData data)
        {
            if (data == null || settings == null) { ShowRange(null); return; }

            var map = new Dictionary<Vector2Int, Color>();
            if (data.HasEffectGroups)
            {
                foreach (var group in data.EffectGroups)
                    if (group != null) AddOffsets(map, group.GetPreviewOffsets(), group.Color);
            }
            else
            {
                AddOffsets(map, data.GetEffectPreviewOffsets(), settings.defaultRangeColor);
            }
            ShowRange(map);
            if (map.Count > 0) UpdateLegend(data);
        }

        /// <summary>그리드 컨테이너 크기가 바뀌었을 때 현재 범위를 다시 배치합니다.</summary>
        public void RefreshLayoutPositions()
        {
            if (_entries.Count > 0) Layout();
        }

        // ── 표시 ─────────────────────────────────────────────────────

        private static void AddOffsets(Dictionary<Vector2Int, Color> map, List<Vector2Int> offsets, Color color)
        {
            if (offsets == null) return;
            foreach (var o in offsets)
                if (o != Vector2Int.zero) map[o] = color; // 뒤 그룹이 겹치는 칸을 덮어쓴다 (월드 미리보기와 동일)
        }

        private void ShowRange(Dictionary<Vector2Int, Color> map)
        {
            bool hasRange = map != null && map.Count > 0;
            if (settings == null)
            {
                if (gridLayout != null) gridLayout.gameObject.SetActive(hasRange);
                Debug.LogError("[TotemUI] TotemDisplaySettings is missing.");
                return;
            }
            if (!hasRange)
            {
                ShowNoRange();
                return;
            }
            if (gridLayout != null) gridLayout.gameObject.SetActive(true);
            SetLabelActive(_noRangeLabel, false);

            DisableConflictingLayoutGroup();
            EnsureLegend();

            _entries.Clear();
            _entries.Add(new Entry { Offset = Vector2Int.zero, Kind = CellKind.Totem, Color = settings.totemCellColor });
            foreach (var pair in map)
                _entries.Add(new Entry { Offset = pair.Key, Kind = CellKind.Range, Color = pair.Value });
            Render();
        }

        private void Render()
        {
            if (cellPrefab == null || gridLayout == null) return;

            while (_cells.Count < _entries.Count)
                _cells.Add(Instantiate(cellPrefab, gridLayout.transform));

            for (int i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i];
                if (cell == null) continue;
                bool used = i < _entries.Count;
                cell.gameObject.SetActive(used);
                if (!used) continue;

                var e = _entries[i];
                if (e.Kind == CellKind.Totem)
                {
                    cell.SetTotem(e.Color);
                }
                else
                {
                    var fill = e.Color;
                    fill.a = settings.rangeFillAlpha;
                    cell.SetRange(fill, BrightenForOutline(e.Color), settings.rangeOutlineSprite,
                        settings.outlineThicknessMultiplier);
                }
            }

            UpdateCaption();
            Layout();
        }

        /// <summary>범위 바운딩 박스를 박스 가운데에 배치. 칸은 maxCellSize를 넘지 않고, 넘칠 때만 줄어든다.</summary>
        private void Layout()
        {
            if (gridLayout == null || settings == null || _entries.Count == 0) return;

            int minX = 0, maxX = 0, minY = 0, maxY = 0;
            foreach (var e in _entries)
            {
                minX = Mathf.Min(minX, e.Offset.x); maxX = Mathf.Max(maxX, e.Offset.x);
                minY = Mathf.Min(minY, e.Offset.y); maxY = Mathf.Max(maxY, e.Offset.y);
            }
            int cols = maxX - minX + 1;
            int rows = maxY - minY + 1;

            Rect rect = gridLayout.Rect.rect;
            float left = gridLayout.paddingLeft;
            float bottom = gridLayout.paddingBottom + CaptionReserve();
            float availW = Mathf.Max(1f, rect.width - gridLayout.paddingLeft - gridLayout.paddingRight);
            float availH = Mathf.Max(1f, rect.height - gridLayout.paddingTop - bottom);

            float ratio = settings.cellSpacingRatio;
            float cell = Mathf.Min(settings.maxCellSize,
                availW / (cols + (cols - 1) * ratio),
                availH / (rows + (rows - 1) * ratio));
            float step = cell * (1f + ratio);
            float totalW = cols * cell + (cols - 1) * cell * ratio;
            float totalH = rows * cell + (rows - 1) * cell * ratio;
            float originX = left + (availW - totalW) * 0.5f;
            float originY = bottom + (availH - totalH) * 0.5f;

            for (int i = 0; i < _entries.Count && i < _cells.Count; i++)
            {
                if (_cells[i] == null) continue;
                var o = _entries[i].Offset;
                var pos = new Vector2(originX + (o.x - minX) * step, originY + (o.y - minY) * step);
                gridLayout.ApplyLayoutToChild((RectTransform)_cells[i].transform, pos, new Vector2(cell, cell));
            }
        }

        /// <summary>색상 계열은 유지하고 밝기(V)만 최소값 이상으로 올린다. 데이터 색은 바꾸지 않는다.</summary>
        private Color BrightenForOutline(Color color)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            var result = Color.HSVToRGB(h, s, Mathf.Max(v, settings.outlineMinBrightness));
            result.a = 1f;
            return result;
        }

        /// <summary>범위가 없는 토템: 박스는 남기고 칸·캡션을 숨긴 뒤 가운데에 "범위 없음"을 표시한다.</summary>
        private void ShowNoRange()
        {
            _entries.Clear();
            foreach (var cell in _cells)
                if (cell != null) cell.gameObject.SetActive(false);
            SetLabelActive(_caption, false);

            // 문구가 비어 있으면 예전처럼 박스(Grid_List)째 숨긴다.
            // 이 컴포넌트의 GameObject는 설명 텍스트(Stat_Area)도 품고 있어 켜져 있어야 하므로 gridLayout만 끈다.
            bool show = !string.IsNullOrEmpty(settings.noRangeText);
            if (gridLayout != null) gridLayout.gameObject.SetActive(show);
            if (!show) return;

            _noRangeLabel = EnsureLabel(_noRangeLabel, "Range_None");
            _noRangeLabel.fontSize = settings.noRangeFontSize;
            _noRangeLabel.color = settings.noRangeColor;
            _noRangeLabel.text = settings.noRangeText;
            var rt = _noRangeLabel.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(gridLayout.paddingLeft, gridLayout.paddingBottom);
            rt.offsetMax = new Vector2(-gridLayout.paddingRight, -gridLayout.paddingTop);
            _noRangeLabel.gameObject.SetActive(true);
        }

        private TextMeshProUGUI EnsureLabel(TextMeshProUGUI label, string name)
        {
            if (label == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(gridLayout.transform, false);
                label = go.AddComponent<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
                label.textWrappingMode = TextWrappingModes.NoWrap;
            }
            if (settings.captionFont != null) label.font = settings.captionFont;
            return label;
        }

        private static void SetLabelActive(TextMeshProUGUI label, bool active)
        {
            if (label != null) label.gameObject.SetActive(active);
        }

        // ── 캡션 ─────────────────────────────────────────────────────

        private float CaptionReserve() =>
            settings != null && !string.IsNullOrEmpty(settings.captionText) ? settings.captionReserve : 0f;

        private void UpdateCaption()
        {
            if (gridLayout == null || settings == null) return;
            bool show = !string.IsNullOrEmpty(settings.captionText);
            if (!show)
            {
                if (_caption != null) _caption.gameObject.SetActive(false);
                return;
            }
            _caption = EnsureLabel(_caption, "Range_Caption");
            _caption.fontSize = settings.captionFontSize;
            _caption.color = settings.captionColor;
            _caption.text = settings.captionText;
            _caption.gameObject.SetActive(true);

            var crt = _caption.rectTransform;
            crt.anchorMin = new Vector2(0f, 0f);
            crt.anchorMax = new Vector2(1f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, gridLayout.paddingBottom);
            crt.sizeDelta = new Vector2(-(gridLayout.paddingLeft + gridLayout.paddingRight), settings.captionReserve);
        }

        // ── 범례 (기존 동작 유지) ───────────────────────────────────

        private void DisableConflictingLayoutGroup()
        {
            // GridLayoutGroup이 있으면 수동 레이아웃을 위해 비활성화
            if (gridLayout == null) return;
            var glg = gridLayout.GetComponent<GridLayoutGroup>();
            if (glg != null && glg.enabled) glg.enabled = false;
        }

        private void EnsureLegend()
        {
            if (_legendReady) return;
            SetupLegend();
            _legendReady = true;
        }

        private void SetupLegend()
        {
            if (legendSlotPrefab == null || tr_LegendParent == null || settings == null) return;
            ClearLegend();

            var legendData = new (string label, Sprite sprite)[]
            {
                (settings.fieldLabel, settings.fieldSprite),
                (settings.totemLabel, settings.totemSprite),
                (settings.effectLabel, settings.effectSprite)
            };

            foreach (var data in legendData)
            {
                var slot = Instantiate(legendSlotPrefab, tr_LegendParent);
                slot.SetData(data.label, data.sprite);
            }
        }

        private void UpdateLegend(TotemData data)
        {
            if (!data.HasEffectGroups) { SetupLegend(); return; }
            if (legendSlotPrefab == null || tr_LegendParent == null) return;
            ClearLegend();
            foreach (var group in data.EffectGroups)
            {
                if (group == null) continue;
                var slot = Instantiate(legendSlotPrefab, tr_LegendParent);
                slot.SetData(string.IsNullOrWhiteSpace(group.Label) ? "효과" : group.Label, settings.fieldSprite);
                slot.SetColor(group.Color);
            }
        }

        private void ClearLegend()
        {
            for (int i = tr_LegendParent.childCount - 1; i >= 0; i--)
            {
                var child = tr_LegendParent.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }
    }
}
