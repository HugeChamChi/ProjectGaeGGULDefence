using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sephiria-style totem bag: a miniature of the real 6x4 board with unit levels, plus a drawer info card for the
/// tapped unit (what it is, level effects) or totem (range). Opened by <see cref="TotemBagController"/> with the bottom drawer. Reads live GridManager state; never mutates it.
/// Entry: <see cref="Bind"/> once, then <see cref="UI_Base.Open"/>, <see cref="ShowStored"/>, <see cref="Select"/>.
/// </summary>
public sealed class TotemBagPopup : UI_Base
{
    private const float RefWidth = 1080f, RefHeight = 2340f;
    private const float PanelWidth = 1000f, PanelTop = 250f, BannerHeight = 180f, BodyTopInset = 90f;
    private const float Cell = 146f, Gap = 8f, GridPad = 18f, InfoGap = 18f, InfoPad = 30f;
    private const float RowHeight = 66f, RowGap = 8f, MiniCell = 58f, MiniGap = 5f;
    private const int MiniSize = 5;
    private const int SortingOrder = 400;
    /// <summary>Bottom strip (reference px) left uncovered for the real totem drawer when its rect is not strip-sized.</summary>
    private const float DrawerReserve = 300f;
    /// <summary>Highest the bag may move up on short screens, and the gap kept above the drawer strip (reference px).</summary>
    private const float MinPanelTop = 70f, BottomGap = 16f;
    // Flat game palette (navy panel, cream card, thick dark rims like the HUD buttons).
    private static readonly Color BodyColor = new(0.23f, 0.21f, 0.47f);
    private static readonly Color GridColor = new(0.13f, 0.12f, 0.29f);
    private static readonly Color EmptyColor = new(0.27f, 0.25f, 0.52f);
    private static readonly Color EmptyRangeColor = new(0.20f, 0.36f, 0.24f);
    private static readonly Color CardColor = new(1f, 0.965f, 0.88f);
    private static readonly Color MiniBg = new(0.88f, 0.83f, 0.72f);
    private static readonly Color MiniCellColor = new(0.97f, 0.93f, 0.84f);
    private static readonly Color MiniHit = new(0.33f, 0.72f, 0.29f);
    private static readonly Color Gold = new(1f, 0.80f, 0.25f);
    private static readonly Color StepOn = new(0.33f, 0.68f, 0.30f);
    private static readonly Color StepNext = new(1f, 0.78f, 0.24f);
    private static readonly Color StepLocked = new(0.89f, 0.85f, 0.76f);
    private static readonly Color StepLockedText = new(0.55f, 0.49f, 0.41f);
    private static readonly Color StoredPill = new(0.45f, 0.40f, 0.33f);
    private static readonly Color RimColor = new(0.11f, 0.09f, 0.19f);
    private const int RoundSize = 64, RoundRadius = 24, RoundRim = 6;
    private static Sprite _roundRim, _roundFill;
    private static readonly Color Ink = new(0.16f, 0.10f, 0.07f);
    private static readonly Color InkSoft = new(0.36f, 0.25f, 0.18f);
    private static readonly Color Lime = new(0.55f, 0.98f, 0.42f);
    private static readonly Color RangeTint = new(0.55f, 1f, 0.5f);
    private static readonly Color SelectTint = new(1f, 0.85f, 0.25f);
    private static readonly Color Dim = new(0f, 0f, 0f, 0.62f);

    private sealed class CellView
    {
        public GridCell Cell;
        public Image Frame, Icon, Badge, Select;
        public TMP_Text Level, Plus;
    }

    private GridManager _grid;
    private TotemBagSkin _skin;
    private RectTransform _keepVisible;
    private RectTransform _root, _panel, _backdrop, _drawerHost;
    private readonly List<CellView> _cells = new();
    private GridCell _selected;
    private TotemData _storedSelection;
    private RectTransform _storedSlot;
    private Image _slotHighlight;
    private float _gridBottom, _reserve = DrawerReserve, _fitScale = 1f;
    private bool _topIsHighZ;

    /// <summary>Raised when the player taps outside the bag; the caller closes the bottom drawer too.</summary>
    public event Action OnOutsideTapped;

    /// <summary>Builds the bag once. <paramref name="keepVisible"/> (the bottom totem drawer) stays uncovered and tappable.</summary>
    public void Bind(GridManager grid, TotemBagSkin skin, RectTransform keepVisible)
    {
        _grid = grid; _skin = skin; _keepVisible = keepVisible;
        _topIsHighZ = grid.GetCell(0, grid.Rows - 1).transform.position.y > grid.GetCell(0, 0).transform.position.y
                   || grid.GetCell(0, grid.Rows - 1).transform.position.z > grid.GetCell(0, 0).transform.position.z;
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = SortingOrder; canvas.enabled = false;
        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(RefWidth, RefHeight); scaler.matchWidthOrHeight = 0f;
        _root = (RectTransform)transform;
        Build();
    }

    /// <summary>Selects a board cell (unit or totem). Null or an empty cell clears the info.</summary>
    public void Select(GridCell cell)
    {
        _storedSelection = null; _storedSlot = null;
        _selected = cell != null && cell.IsOccupied && _selected != cell ? cell : null;
        RefreshInfo();
    }

    /// <summary>
    /// Shows a stored (not placed) totem from the bottom drawer. <paramref name="slot"/> is the drawer slot that was tapped;
    /// it gets a highlight and tapping the same slot again clears the info.
    /// </summary>
    public void ShowStored(TotemData data, RectTransform slot = null)
    {
        bool same = slot != null && slot == _storedSlot && data == _storedSelection;
        _selected = null;
        _storedSelection = same ? null : data;
        _storedSlot = same ? null : slot;
        RefreshInfo();
    }

    /// <inheritdoc/>
    public override async UniTask CloseAsync()
    {
        _selected = null; _storedSelection = null; _storedSlot = null; RefreshInfo();
        await base.CloseAsync();
    }

    protected override async UniTask OpenAnimationAsync()
    {
        var token = this.GetCancellationTokenOnDestroy();
        FitPanel();
        _panel.localScale = new Vector3(_fitScale * 0.92f, _fitScale * 0.92f, 1f);
        await _panel.DOScale(_fitScale, 0.18f).SetEase(Ease.OutBack).SetUpdate(true).ToUniTask(cancellationToken: token);
    }

    private void LateUpdate()
    {
        if (_root == null || !GetComponent<Canvas>().enabled) return;
        FitBackdrop();
        FitPanel();
        RefreshCells();
        FitSlotHighlight();
    }

    /// <summary>Gold tint over the tapped drawer slot (drawn on this canvas, no raycast).</summary>
    private void FitSlotHighlight()
    {
        bool show = _storedSlot != null && _storedSelection != null && _storedSlot.gameObject.activeInHierarchy;
        _slotHighlight.enabled = show;
        if (!show) return;
        var corners = new Vector3[4]; _storedSlot.GetWorldCorners(corners);
        Vector2 a = RectTransformUtility.WorldToScreenPoint(null, corners[0]), b = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, a, null, out var la);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, b, null, out var lb);
        var r = _slotHighlight.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = (la + lb) / 2; r.sizeDelta = lb - la;
    }

    // ---------- build ----------
    private void Build()
    {
        _backdrop = NewImage("Backdrop", _root, null, Dim).rectTransform;
        _backdrop.anchorMin = new Vector2(0, 1); _backdrop.anchorMax = new Vector2(1, 1); _backdrop.pivot = new Vector2(0.5f, 1);
        _backdrop.anchoredPosition = Vector2.zero; _backdrop.sizeDelta = new Vector2(0, RefHeight);
        _backdrop.gameObject.AddComponent<Button>().onClick.AddListener(() => OnOutsideTapped?.Invoke());

        _panel = NewRect("Bag", _root);
        _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 1); _panel.pivot = new Vector2(0.5f, 1);
        _panel.anchoredPosition = new Vector2(0, -PanelTop);
        _panel.gameObject.AddComponent<Image>().color = Color.clear; // swallow taps inside the bag

        var body = NewRound("Body", _panel, true, BodyColor, 0.8f);
        Stretch(body.rectTransform, 0, BodyTopInset, 0, 0);

        var banner = NewImage("TitleBanner", _panel, _skin.TitleBanner, Color.white);
        banner.type = Image.Type.Sliced; Place(banner.rectTransform, 0, 0, PanelWidth, BannerHeight);
        var title = NewText("Title", banner.rectTransform, "토템 가방", _skin.TitleFont, 68, Color.white, TextAlignmentOptions.Center);
        Place(title.rectTransform, 200, 30, PanelWidth - 400, BannerHeight - 50); Outline(title, 0.28f);

        var close = NewImage("Close", _panel, _skin.CloseButton, Color.white);
        Place(close.rectTransform, PanelWidth - 120, 20, 110, 110);
        close.gameObject.AddComponent<Button>().onClick.AddListener(() => OnOutsideTapped?.Invoke());

        int cols = _grid.Columns, rows = _grid.Rows;
        float gridW = cols * Cell + (cols - 1) * Gap + GridPad * 2, gridH = rows * Cell + (rows - 1) * Gap + GridPad * 2;
        float gridX = (PanelWidth - gridW) / 2, gridY = BannerHeight + 14;
        var gridBg = NewRound("Grid", _panel, false, GridColor, 1f);
        Place(gridBg.rectTransform, gridX, gridY, gridW, gridH);
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            int z = _topIsHighZ ? rows - 1 - r : r;
            var view = new CellView { Cell = _grid.GetCell(c, z) };
            float cx = GridPad + c * (Cell + Gap), cy = GridPad + r * (Cell + Gap);
            // sibling behind the frame so the glow rims it instead of covering it
            view.Select = NewRound("Select_" + c + "_" + z, gridBg.rectTransform, false, SelectTint, 1.3f);
            Place(view.Select.rectTransform, cx - 8, cy - 8, Cell + 16, Cell + 16); view.Select.raycastTarget = false;
            var frame = NewRound("Cell_" + c + "_" + z, gridBg.rectTransform, false, EmptyColor, 1.6f);
            Place(frame.rectTransform, cx, cy, Cell, Cell);
            var captured = view.Cell;
            frame.gameObject.AddComponent<Button>().onClick.AddListener(() => Select(captured));
            view.Frame = frame;
            view.Icon = NewImage("Icon", frame.rectTransform, null, Color.white);
            Place(view.Icon.rectTransform, 14, 18, Cell - 28, Cell - 28); view.Icon.preserveAspect = true; view.Icon.raycastTarget = false;
            view.Badge = NewImage("LevelBadge", frame.rectTransform, _skin.LevelBadge, Color.white);
            Place(view.Badge.rectTransform, -8, -10, 108, 52); view.Badge.raycastTarget = false;
            view.Level = NewText("Level", view.Badge.rectTransform, "", _skin.NumberFont, 40, Color.white, TextAlignmentOptions.Center);
            Stretch(view.Level.rectTransform, 0, 0, 0, 2); Outline(view.Level, 0.3f);
            view.Plus = NewText("Plus", frame.rectTransform, "", _skin.NumberFont, 40, Lime, TextAlignmentOptions.BottomRight);
            Place(view.Plus.rectTransform, 0, 0, Cell, Cell); view.Plus.margin = new Vector4(0, 0, 10, 4); Outline(view.Plus, 0.32f);
            _cells.Add(view);
        }
        _gridBottom = gridY + gridH;

        _drawerHost = NewRect("DrawerInfo", _panel);
        _slotHighlight = NewRound("StoredSlotHighlight", _root, false, new Color(SelectTint.r, SelectTint.g, SelectTint.b, 0.4f), 1.3f);
        _slotHighlight.raycastTarget = false; _slotHighlight.enabled = false;
        RefreshInfo();
    }

    // ---------- runtime ----------
    private void FitBackdrop()
    {
        float height = _root.rect.height, reserve = DrawerReserve;
        if (_keepVisible != null)
        {
            var corners = new Vector3[4]; _keepVisible.GetWorldCorners(corners);
            Vector2 screenTop = RectTransformUtility.WorldToScreenPoint(null, corners[1]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screenTop, null, out var local);
            float fromBottom = local.y + height * _root.pivot.y;
            // the drawer root may be full-screen (its own close catcher); only trust a strip-sized rect
            if (fromBottom > 0 && fromBottom < height * 0.4f) reserve = fromBottom;
        }
        _reserve = reserve;
        _backdrop.sizeDelta = new Vector2(0, Mathf.Max(0, height - reserve));
    }

    /// <summary>
    /// Short screens (e.g. 16:9) have less height above the drawer strip: move the bag up, then shrink it if it still
    /// would reach the strip, so the info card never covers the drawer.
    /// </summary>
    private void FitPanel()
    {
        float bottomLimit = _root.rect.height - _reserve - BottomGap, height = _panel.sizeDelta.y;
        float top = Mathf.Clamp(bottomLimit - height, MinPanelTop, PanelTop);
        _fitScale = Mathf.Clamp01((bottomLimit - top) / Mathf.Max(1f, height));
        _panel.anchoredPosition = new Vector2(0, -top);
        if (!DOTween.IsTweening(_panel)) _panel.localScale = new Vector3(_fitScale, _fitScale, 1f);
    }

    private void RefreshCells()
    {
        var range = new HashSet<GridCell>();
        var rangeTotem = _selected != null ? _selected.OccupyingTotem : null;
        if (rangeTotem != null) foreach (var c in rangeTotem.GetAffectedCells()) range.Add(c);
        var bonus = BonusByCell();
        foreach (var v in _cells)
        {
            var unit = v.Cell.OccupyingUnit; var totem = v.Cell.OccupyingTotem;
            bool inRange = range.Contains(v.Cell);
            v.Select.enabled = _selected == v.Cell;
            if (unit != null)
            {
                int tier = Mathf.Clamp((int)unit.GrowthTier, 0, _skin.UnitFrames.Length - 1);
                SetFrame(v.Frame, _skin.UnitFrames[tier]);
                v.Icon.sprite = unit.unitData.icon; v.Icon.enabled = v.Icon.sprite != null;
                v.Badge.enabled = true; v.Level.enabled = true;
                bool boosted = unit.CurrentLevel > unit.BaseLevel;
                v.Level.text = unit.CurrentLevel + "<size=55%>/" + _skin.DisplayMaxLevel + "</size>";
                v.Level.color = boosted ? Lime : Color.white;
                v.Plus.text = inRange && rangeTotem != null ? "+" + rangeTotem.Data.LevelIncrease : "";
                v.Plus.alignment = TextAlignmentOptions.BottomRight; v.Plus.fontSize = 40;
            }
            else if (totem != null)
            {
                SetFrame(v.Frame, _skin.TotemFrame);
                v.Icon.sprite = totem.Data.DisplaySprite; v.Icon.enabled = v.Icon.sprite != null;
                v.Badge.enabled = false; v.Level.enabled = false;
                v.Plus.text = totem.Data.IsLevelTotem ? "+" + totem.Data.LevelIncrease : "";
                v.Plus.alignment = TextAlignmentOptions.BottomRight; v.Plus.fontSize = 40;
            }
            else
            {
                SetRound(v.Frame, false, 1.6f); v.Icon.enabled = false; v.Badge.enabled = false; v.Level.enabled = false;
                bonus.TryGetValue(v.Cell, out int b);
                v.Plus.text = b > 0 ? "+" + b : "";
                v.Plus.alignment = TextAlignmentOptions.Center; v.Plus.fontSize = 52;
            }
            bool empty = unit == null && totem == null;
            v.Frame.color = empty ? (inRange || v.Plus.text.Length > 0 ? EmptyRangeColor : EmptyColor) : inRange ? RangeTint : Color.white;
        }
    }

    private Dictionary<GridCell, int> BonusByCell()
    {
        var map = new Dictionary<GridCell, int>();
        foreach (var v in _cells)
        {
            var t = v.Cell.OccupyingTotem;
            if (t == null || !t.Data.IsLevelTotem) continue;
            foreach (var c in t.GetAffectedCells()) { map.TryGetValue(c, out int n); map[c] = n + t.Data.LevelIncrease; }
        }
        return map;
    }

    private void RefreshInfo()
    {
        if (_drawerHost == null) return;
        Clear(_drawerHost);
        bool has = (_selected != null && _selected.IsOccupied) || _storedSelection != null;
        float panelHeight = _gridBottom + GridPad + 20;
        if (has)
        {
            // Drawer: the info card hangs under the grid inside the bag (user decision 2026-11-17).
            float h = BuildInfo(_drawerHost, PanelWidth - 72);
            Place(_drawerHost, 36, _gridBottom + InfoGap, PanelWidth - 72, h);
            panelHeight = _gridBottom + InfoGap + h + 40;
        }
        _panel.sizeDelta = new Vector2(PanelWidth, panelHeight);
    }

    /// <returns>Height used.</returns>
    private float BuildInfo(RectTransform host, float width)
    {
        var bg = NewRound("Card", host, true, CardColor, 0.9f);
        float inner = width - InfoPad * 2, y = InfoPad;
        var unit = _selected != null ? _selected.OccupyingUnit : null;
        if (unit != null) y = BuildUnit(bg.rectTransform, unit, inner, y);
        else y = BuildTotem(bg.rectTransform, _selected != null ? _selected.OccupyingTotem : null, _storedSelection, inner, y);
        float height = y + InfoPad;
        Place(bg.rectTransform, 0, 0, width, height);
        return height;
    }

    private float BuildUnit(RectTransform card, UnitBase unit, float inner, float y)
    {
        const float icon = 136f, levelW = 150f;
        int tier = Mathf.Clamp((int)unit.GrowthTier, 0, _skin.UnitFrames.Length - 1);
        var frame = NewImage("UnitFrame", card, _skin.UnitFrames[tier], Color.white); Place(frame.rectTransform, InfoPad, y, icon, icon);
        var img = NewImage("UnitIcon", frame.rectTransform, unit.unitData.icon, Color.white); Place(img.rectTransform, 12, 14, icon - 24, icon - 24); img.preserveAspect = true;

        float textX = InfoPad + icon + 22, textW = inner - icon - 22 - levelW;
        var name = NewText("Name", card, unit.unitData.unitName, Heading, 50, Ink, TextAlignmentOptions.TopLeft);
        Place(name.rectTransform, textX, y - 2, textW, 64);
        var desc = NewText("Desc", card, unit.unitData.GetFormattedDescription(unit.currentTier), _skin.BodyFont, 28, InkSoft, TextAlignmentOptions.TopLeft);
        desc.enableWordWrapping = true;
        float descH = Mathf.Max(40, desc.GetPreferredValues(desc.text, textW, 0).y);
        Place(desc.rectTransform, textX, y + 64, textW, descH);

        var level = NewText("Level", card, unit.CurrentLevel + "<size=45%>/" + _skin.DisplayMaxLevel + "</size>", _skin.NumberFont, 92,
            unit.CurrentLevel > unit.BaseLevel ? Lime : Color.white, TextAlignmentOptions.TopRight);
        Place(level.rectTransform, InfoPad + inner - levelW, y - 8, levelW, 110); Outline(level, 0.3f);

        y += Mathf.Max(icon, 64 + descH) + 22;
        var steps = unit.unitData.LevelBonuses;
        if (steps == null || steps.Count == 0)
        {
            var none = NewText("NoSteps", card, "레벨 효과 없음", _skin.BodyFont, 28, InkSoft, TextAlignmentOptions.Left);
            Place(none.rectTransform, InfoPad, y, inner, 44);
            return y + 44;
        }
        int next = int.MaxValue;
        foreach (var s in steps) if (s.Level > unit.CurrentLevel && s.Level < next) next = s.Level;
        foreach (var s in steps)
        {
            bool on = s.Level <= unit.CurrentLevel, isNext = s.Level == next;
            var row = NewRound("Step_" + s.Level, card, false, on ? StepOn : isNext ? StepNext : StepLocked, 1.5f);
            Place(row.rectTransform, InfoPad, y, inner, RowHeight);
            var badge = NewImage("Lv", row.rectTransform, _skin.LevelBadge, on || isNext ? Color.white : new Color(0.72f, 0.68f, 0.62f));
            Place(badge.rectTransform, 10, 8, 110, RowHeight - 16);
            var lv = NewText("LvText", badge.rectTransform, "Lv " + s.Level, _skin.NumberFont, 30, Color.white, TextAlignmentOptions.Center);
            Stretch(lv.rectTransform, 0, 0, 0, 2); Outline(lv, 0.3f);
            var txt = NewText("Desc", row.rectTransform, (s.Description ?? "").Replace("\n", " "), _skin.BodyFont, 28,
                on ? Color.white : isNext ? Ink : StepLockedText, TextAlignmentOptions.Left);
            txt.enableWordWrapping = false; txt.overflowMode = TextOverflowModes.Ellipsis;
            Place(txt.rectTransform, 136, 0, inner - 150, RowHeight);
            y += RowHeight + RowGap;
        }
        return y - RowGap;
    }

    private float BuildTotem(RectTransform card, TotemBase placed, TotemData stored, float inner, float y)
    {
        var data = placed != null ? placed.Data : stored;
        if (data == null) return y;
        var offsets = new List<Vector2Int>();
        if (placed != null && placed.CurrentCell != null)
            foreach (var c in placed.GetAffectedCells()) offsets.Add(c.GridPosition - placed.CurrentCell.GridPosition);
        else offsets.AddRange(data.GetEffectPreviewOffsets());

        float gridW = MiniSize * MiniCell + (MiniSize - 1) * MiniGap + 24;
        var mini = NewRound("RangeGrid", card, false, MiniBg, 1.4f);
        Place(mini.rectTransform, InfoPad, y, gridW, gridW);
        int half = MiniSize / 2;
        for (int r = 0; r < MiniSize; r++)
        for (int c = 0; c < MiniSize; c++)
        {
            int dx = c - half, dz = _topIsHighZ ? half - r : r - half;
            bool origin = dx == 0 && dz == 0, hit = offsets.Contains(new Vector2Int(dx, dz));
            var cell = NewRound("m", mini.rectTransform, false, origin ? Gold : hit ? MiniHit : MiniCellColor, 2.6f);
            Place(cell.rectTransform, 12 + c * (MiniCell + MiniGap), 12 + r * (MiniCell + MiniGap), MiniCell, MiniCell);
            if (origin)
            {
                var ic = NewImage("TotemIcon", cell.rectTransform, data.DisplaySprite, Color.white);
                Stretch(ic.rectTransform, 4, 4, 4, 4); ic.preserveAspect = true;
            }
            else if (hit && data.IsLevelTotem)
            {
                var plus = NewText("Plus", cell.rectTransform, "+" + data.LevelIncrease, _skin.NumberFont, 32, Color.white, TextAlignmentOptions.Center);
                Stretch(plus.rectTransform, 0, 0, 0, 2); Outline(plus, 0.3f);
            }
        }
        float textX = InfoPad + gridW + 30, textW = inner - gridW - 30;
        var name = NewText("Name", card, data.totemName, Heading, 56, Ink, TextAlignmentOptions.TopLeft);
        Place(name.rectTransform, textX, y + 14, textW, 72);
        string effect = data.IsLevelTotem
            ? "범위 안 유닛 레벨 <color=#2f9a25>+" + data.LevelIncrease + "</color>"
            : data.GetDisplayDescription(false);
        var line = NewText("Effect", card, effect, _skin.BodyFont, 34, InkSoft, TextAlignmentOptions.TopLeft);
        line.enableWordWrapping = true; Place(line.rectTransform, textX, y + 98, textW, 100);
        var pill = NewRound("State", card, false, placed != null ? StepOn : StoredPill, 2f);
        Place(pill.rectTransform, textX, y + gridW - 58, placed != null ? 200 : 140, 52);
        var tag = NewText("Text", pill.rectTransform, placed != null ? "전장에 배치됨" : "보관 중", _skin.BodyFont, 26, Color.white, TextAlignmentOptions.Center);
        Stretch(tag.rectTransform, 0, 0, 0, 2);
        return y + gridW;
    }

    // ---------- helpers ----------
    private TMP_FontAsset Heading => _skin.HeadingFont != null ? _skin.HeadingFont : _skin.BodyFont;

    /// <summary>Rounded 9-slice box generated once (white fill, optional dark rim), tinted per use so corners never stretch.</summary>
    private static Sprite Round(bool rim)
    {
        var cached = rim ? _roundRim : _roundFill;
        if (cached != null) return cached;
        var tex = new Texture2D(RoundSize, RoundSize, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = rim ? "TotemBagRoundRim" : "TotemBagRound" };
        var px = new Color32[RoundSize * RoundSize];
        Color32 rimColor = RimColor, white = new Color32(255, 255, 255, 255);
        for (int y = 0; y < RoundSize; y++)
        for (int x = 0; x < RoundSize; x++)
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            var corner = new Vector2(Mathf.Clamp(p.x, RoundRadius, RoundSize - RoundRadius), Mathf.Clamp(p.y, RoundRadius, RoundSize - RoundRadius));
            float inside = RoundRadius - Vector2.Distance(p, corner); // distance in from the outer edge
            var c = Color32.Lerp(rimColor, white, rim ? Mathf.Clamp01(inside - RoundRim + 0.5f) : 1f);
            c.a = (byte)(Mathf.Clamp01(inside + 0.5f) * 255);
            px[y * RoundSize + x] = c;
        }
        tex.SetPixels32(px); tex.Apply(false, true);
        var sprite = Sprite.Create(tex, new Rect(0, 0, RoundSize, RoundSize), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(RoundRadius, RoundRadius, RoundRadius, RoundRadius));
        if (rim) _roundRim = sprite; else _roundFill = sprite;
        return sprite;
    }

    /// <summary><paramref name="corner"/> above 1 makes the corners smaller (Image.pixelsPerUnitMultiplier).</summary>
    private static Image NewRound(string name, Transform parent, bool rim, Color color, float corner)
    {
        var img = NewImage(name, parent, Round(rim), color);
        img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = corner;
        return img;
    }

    private static void SetRound(Image img, bool rim, float corner) { img.sprite = Round(rim); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = corner; }
    private static void SetFrame(Image img, Sprite frame) { img.sprite = frame; img.type = Image.Type.Simple; img.pixelsPerUnitMultiplier = 1f; }

    private static void Clear(RectTransform t) { for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject); }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); var r = (RectTransform)go.transform; r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); return r;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var r = NewRect(name, parent); var img = r.gameObject.AddComponent<Image>();
        img.sprite = sprite; img.color = color; if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
        return img;
    }

    private static TMP_Text NewText(string name, Transform parent, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
    {
        var r = NewRect(name, parent); var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false; t.richText = true;
        return t;
    }

    private static void Outline(TMP_Text t, float width) { t.outlineWidth = width; t.outlineColor = new Color32(40, 24, 18, 255); }

    /// <summary>Top-left placement inside a top-left anchored parent (y grows downward).</summary>
    private static void Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
    }

    private static void Stretch(RectTransform r, float left, float top, float right, float bottom)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(left, bottom); r.offsetMax = new Vector2(-right, -top);
    }
}
