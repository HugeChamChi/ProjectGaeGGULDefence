using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Self-contained interaction and FX preview. Does not mutate live inventory or simulate combat buffs.</summary>
public sealed class TotemInventoryLab : MonoBehaviour
{
    [Serializable]
    private sealed class Tile
    {
        public RectTransform Rect;
        public Image Frame;
        public Image Background;
        public Image Icon;
        public TMP_Text Label;
    }

    [SerializeField] private TotemInventoryLabSettings _settings;
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private RectTransform _root;
    [SerializeField] private Tile[] _cells;
    [SerializeField] private Tile[] _slots;
    [SerializeField] private Image _dragGhost;
    [SerializeField] private Image _flightA;
    [SerializeField] private Image _flightB;
    [SerializeField] private TMP_Text _status;
    [SerializeField] private TMP_Text _detailTitle;
    [SerializeField] private TMP_Text _detailBody;
    [SerializeField] private TMP_Text _count;
    [SerializeField] private Button _recallButton;
    [SerializeField] private Button _rotateButton;
    [SerializeField] private Button _resetButton;
    [SerializeField] private Button _fullButton;
    [SerializeField] private Button _fxButton;
    [SerializeField] private TMP_Text _fxLabel;
    private TotemInventoryLabState _state;
    private bool _selectedStorage;
    private int _selected = -1;
    private bool _busy;
    private bool _fxEnabled = true;
    private bool _dragActive;
    private int _pointerId;
    private bool _dragStorage;
    private int _dragIndex;
    private int _hoverCell = -1;
    private CancellationTokenSource _presentationCts;
    private readonly List<RaycastResult> _hits = new List<RaycastResult>();
    private static readonly Color Ink = new Color(.075f, .12f, .13f);
    private static readonly Color Panel = new Color(.1f, .17f, .18f);
    private static readonly Color Accent = new Color(.9f, .76f, .43f);
    private static readonly Color Muted = new Color(.65f, .75f, .73f);

    /// <summary>Current isolated state, available to lab verification.</summary>
    public TotemInventoryLabState State => _state;
    /// <summary>True while a transfer is being presented.</summary>
    public bool IsBusy => _busy;

    private void Start()
    {
        _state = new TotemInventoryLabState(_settings);
        _recallButton.onClick.AddListener(RecallSelected);
        _rotateButton.onClick.AddListener(RotateSelected);
        _resetButton.onClick.AddListener(() => ResetSamples(false));
        _fullButton.onClick.AddListener(() => ResetSamples(true));
        _fxButton.onClick.AddListener(ToggleFx);
        ResetSamples(false);
    }

    /// <summary>Restores a repeatable demonstration, including a full-storage edge case.</summary>
    public void ResetSamples(bool full)
    {
        if (_busy) return;
        CancelDrag();
        _state.Reset(full);
        _selected = -1;
        Refresh();
        Say(full ? "보관함이 가득 찼어요. 보관 토템을 선택해 필드 토템과 맞바꿔 보세요." : "필드 토템을 눌러 회수하거나, 보관 토템을 필드로 끌어 보세요.");
    }

    /// <summary>Selects a source, or places/exchanges a selected stored item on a tapped field cell.</summary>
    public void Select(bool storage, int index)
    {
        if (_busy || _dragActive || index < 0) return;
        if (!storage && _selectedStorage && GetEntry(true, _selected) != null)
        {
            Transfer(true, _selected, false, index);
            return;
        }
        if (GetEntry(storage, index) == null) { _selected = -1; Refresh(); return; }
        if (_selected == index && _selectedStorage == storage) _selected = -1;
        else { _selectedStorage = storage; _selected = index; }
        Refresh();
        Say(storage ? "배치할 칸을 누르세요. 토템이 있는 칸은 서로 교체돼요." : "아래 회수 버튼을 누르거나 보관함으로 끌어 놓으세요.");
    }

    /// <summary>Recalls the selected field item without selling it.</summary>
    public void RecallSelected()
    {
        if (!_selectedStorage && _selected >= 0) Transfer(false, _selected, true, -1);
    }

    /// <summary>Rotates a selected sample while preserving orientation through future transfers.</summary>
    public void RotateSelected()
    {
        if (_busy || _dragActive) return;
        var entry = GetEntry(_selectedStorage, _selected);
        if (entry == null || !entry.Data.isRotatable) return;
        entry.Rotation = (entry.Rotation + 1) % 4;
        Refresh();
    }

    private void ToggleFx()
    {
        if (_busy) return;
        _fxEnabled = !_fxEnabled;
        _fxLabel.text = _fxEnabled ? "연출 켜짐" : "연출 꺼짐";
    }

    /// <summary>Begins a pointer-owned drag; contents remain intact until a valid drop.</summary>
    public void BeginDrag(bool storage, int index, PointerEventData pointer)
    {
        if (_busy || _dragActive || GetEntry(storage, index) == null) return;
        _dragActive = true;
        _pointerId = pointer.pointerId;
        _dragStorage = storage;
        _dragIndex = index;
        _selectedStorage = storage;
        _selected = index;
        Refresh();
        SetGhost(_dragGhost, GetEntry(storage, index));
        Drag(pointer);
    }

    /// <summary>Shows a non-raycast ghost and SO-backed footprint for the hovered field cell.</summary>
    public void Drag(PointerEventData pointer)
    {
        if (!_dragActive || pointer.pointerId != _pointerId) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, pointer.position, null, out var point);
        _dragGhost.rectTransform.anchoredPosition = point;
        _hits.Clear();
        EventSystem.current.RaycastAll(pointer, _hits);
        int hover = -1;
        foreach (var hit in _hits)
        {
            var target = hit.gameObject.GetComponentInParent<TotemInventoryLabPointer>();
            if (target != null && target.Owner == this) { if (!target.Storage) hover = target.Index; break; }
        }
        if (hover != _hoverCell) { _hoverCell = hover; RefreshRanges(); }
    }

    /// <summary>Commits only a drop from the pointer that began the active drag.</summary>
    public void Drop(bool storage, int index, PointerEventData pointer)
    {
        if (!_dragActive || pointer.pointerId != _pointerId) return;
        bool sourceStorage = _dragStorage;
        int source = _dragIndex;
        CancelDrag();
        Transfer(sourceStorage, source, storage, index);
    }

    /// <summary>Invalid drops restore the source without consuming or duplicating an item.</summary>
    public void EndDrag(PointerEventData pointer)
    {
        if (!_dragActive || pointer.pointerId != _pointerId) return;
        CancelDrag();
        Say("이동을 취소했어요.");
    }

    private void CancelDrag()
    {
        _dragActive = false;
        _hoverCell = -1;
        if (_dragGhost != null) _dragGhost.gameObject.SetActive(false);
        if (_state != null) Refresh();
    }

    private void OnApplicationFocus(bool focus) { if (!focus) CancelDrag(); }
    private void OnApplicationPause(bool pause) { if (pause) CancelDrag(); }
    private void OnDisable()
    {
        _presentationCts?.Cancel();
        CancelDrag();
        if (_root != null)
            foreach (var rect in _root.GetComponentsInChildren<RectTransform>(true)) rect.DOKill();
        if (_flightA != null) _flightA.gameObject.SetActive(false);
        if (_flightB != null) _flightB.gameObject.SetActive(false);
    }

    /// <summary>Moves a source to a field cell or recalls it. Full inventory exchanges remain valid.</summary>
    public void Transfer(bool sourceStorage, int source, bool targetStorage, int target)
    {
        if (_busy || sourceStorage && targetStorage) return;
        var incoming = GetEntry(sourceStorage, source);
        if (incoming == null) return;
        var outgoing = targetStorage ? null : GetEntry(false, target);
        Vector2 start = GetTile(sourceStorage, source).Rect.anchoredPosition + Vector2.up * 10f;
        // All tile roots are authored directly under the same content root.
        int destination = targetStorage ? _state.Stored.Count : target;
        if (targetStorage && destination >= _settings.Capacity)
        { Say("보관함이 가득 찼어요. 보관 토템을 선택해 필드 토템과 교체해 주세요."); return; }
        if (!targetStorage && (target < 0 || target >= _cells.Length)) return;
        Vector2 end = GetTile(targetStorage, destination).Rect.anchoredPosition + Vector2.up * 10f;
        bool success = sourceStorage ? _state.Place(source, target)
            : targetStorage ? _state.Recall(source) : _state.Move(source, target);
        if (!success) return;
        _selectedStorage = targetStorage;
        _selected = destination;
        _hoverCell = -1;
        string verb = targetStorage ? "회수했어요." : outgoing != null ? "교체했어요." : "배치했어요.";
        Say($"{DisplayName(incoming.Data)} 토템을 {verb}");
        PresentTransfer(incoming, outgoing, sourceStorage, source, targetStorage, destination, start, end);
    }

    private async UniTaskVoid PresentTransfer(TotemInventoryLabState.Entry incoming,
        TotemInventoryLabState.Entry outgoing, bool sourceStorage, int source,
        bool targetStorage, int target, Vector2 start, Vector2 end)
    {
        _busy = true;
        using var presentationCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        _presentationCts = presentationCts;
        CancellationToken token = presentationCts.Token;
        try
        {
            Refresh();
            if (!_fxEnabled) return;
            GetTile(targetStorage, target).Icon.enabled = false;
            if (outgoing != null) GetTile(sourceStorage, source).Icon.enabled = false;
            SetGhost(_flightA, incoming);
            _flightA.rectTransform.anchoredPosition = start;
            if (outgoing != null) { SetGhost(_flightB, outgoing); _flightB.rectTransform.anchoredPosition = end; }
            var sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable);
            sequence.Append(_flightA.rectTransform.DOJumpAnchorPos(end, _settings.TransferLift, 1, _settings.TransferSeconds).SetEase(Ease.InOutSine));
            sequence.Join(_flightA.rectTransform.DOScale(targetStorage ? .82f : 1f, _settings.TransferSeconds));
            if (outgoing != null)
                sequence.Join(_flightB.rectTransform.DOJumpAnchorPos(start, -_settings.TransferLift, 1, _settings.TransferSeconds).SetEase(Ease.InOutSine));
            await sequence.ToUniTask(cancellationToken: token);
            token.ThrowIfCancellationRequested();
            _flightA.gameObject.SetActive(false);
            _flightB.gameObject.SetActive(false);
            Refresh();
            var rect = GetTile(targetStorage, target).Icon.rectTransform;
            rect.localScale = Vector3.one * _settings.ArrivalScale;
            await rect.DOScale(1f, _settings.SettleSeconds).SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable).ToUniTask(cancellationToken: token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            _presentationCts = null;
            _busy = false;
            if (this != null)
            {
                _flightA.gameObject.SetActive(false);
                _flightB.gameObject.SetActive(false);
                Refresh();
            }
        }
    }

    private TotemInventoryLabState.Entry GetEntry(bool storage, int index)
    {
        if (_state == null || index < 0) return null;
        var entries = storage ? _state.Stored : _state.Field;
        return index < entries.Count ? entries[index] : null;
    }
    private Tile GetTile(bool storage, int index) => storage ? _slots[index] : _cells[index];
    private static string DisplayName(TotemData data) => $"TD{data.totemId}";
    private static Sprite SpriteFor(TotemInventoryLabState.Entry entry)
    {
        var sprites = entry.Data.rotationSprites;
        return sprites != null && entry.Rotation < sprites.Length && sprites[entry.Rotation] != null
            ? sprites[entry.Rotation] : entry.Data.DisplaySprite;
    }
    private void SetGhost(Image image, TotemInventoryLabState.Entry entry)
    {
        image.gameObject.SetActive(true);
        image.sprite = SpriteFor(entry);
        image.rectTransform.localScale = Vector3.one;
    }
    private void Say(string message) { _status.text = message; }
    private void Refresh()
    {
        for (int i = 0; i < _cells.Length; i++) Bind(_cells[i], false, i);
        for (int i = 0; i < _slots.Length; i++) Bind(_slots[i], true, i);
        _count.text = $"{_state.Stored.Count} / {_settings.Capacity}";
        var selected = GetEntry(_selectedStorage, _selected);
        _detailTitle.text = selected == null ? "언제든 꺼내고, 다시 놓기" : $"{DisplayName(selected.Data)}   /   {(_selectedStorage ? "보관 중" : "필드 배치 중")}";
        _detailBody.text = selected == null ? "필드 → 보관함으로 끌면 회수\n보관함 → 필드 토템 위에 놓으면 맞교체"
            : selected.Data.GetDisplayDescription(false) + $"\n방향 {selected.Rotation + 1} / 4";
        _recallButton.interactable = !_busy && !_dragActive && selected != null && !_selectedStorage;
        _rotateButton.interactable = !_busy && !_dragActive && selected != null && selected.Data.isRotatable;
        _resetButton.interactable = _fullButton.interactable = _fxButton.interactable = !_busy && !_dragActive;
        RefreshRanges();
    }
    private void Bind(Tile tile, bool storage, int index)
    {
        var entry = GetEntry(storage, index);
        tile.Icon.enabled = entry != null;
        tile.Icon.sprite = entry == null ? null : SpriteFor(entry);
        tile.Icon.color = _dragActive && storage == _dragStorage && index == _dragIndex
            ? new Color(1f, 1f, 1f, .3f) : Color.white;
        tile.Icon.rectTransform.localScale = Vector3.one;
        tile.Frame.color = index == _selected && storage == _selectedStorage ? Accent : new Color(.24f, .34f, .33f);
        tile.Label.text = storage ? entry == null ? "빈 칸" : DisplayName(entry.Data)
            : entry == null ? $"{index % _settings.Columns + 1} · {index / _settings.Columns + 1}" : DisplayName(entry.Data);
    }
    private void RefreshRanges()
    {
        foreach (var tile in _cells) tile.Background.color = new Color(.16f, .235f, .22f);
        if (_state == null) return;
        var entry = GetEntry(_selectedStorage, _selected);
        int center = _dragActive ? _hoverCell : _selectedStorage ? -1 : _selected;
        if (entry == null || center < 0) return;
        foreach (var offset in entry.Data.GetEffectPreviewOffsets())
        {
            var rotated = offset;
            for (int step = 0; step < entry.Rotation; step++) rotated = new Vector2Int(rotated.y, -rotated.x);
            int x = center % _settings.Columns + rotated.x;
            int y = center / _settings.Columns + rotated.y;
            if (x >= 0 && x < _settings.Columns && y >= 0 && y < _settings.Rows)
                _cells[y * _settings.Columns + x].Background.color = new Color(.34f, .43f, .28f);
        }
    }

    /// <summary>Authors the complete serialized preview UI using Unity APIs. Called once by the scene authoring tool.</summary>
    public void BuildPreview(TotemInventoryLabSettings settings, TMP_FontAsset font, Sprite background)
    {
        _settings = settings;
        _font = font;
        var canvasObject = new GameObject("Totem Inventory Lab UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(900, 1600);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var backdrop = Box("Backdrop", canvasObject.transform, Vector2.zero, new Vector2(6000, 6000), Ink);
        if (background != null)
        {
            var art = Box("Battlefield", canvasObject.transform, Vector2.zero, new Vector2(950, 1680), new Color(.35f, .45f, .4f, .13f));
            art.sprite = background;
        }
        _root = Rect("Portrait Content", canvasObject.transform, Vector2.zero, new Vector2(900, 1600));
        Text("Eyebrow", _root, new Vector2(0, 762), new Vector2(780, 30), "FX LAB   /   TOTEM INVENTORY", 19, Accent);
        Text("Title", _root, new Vector2(0, 707), new Vector2(780, 74), "토템을 자유롭게 교체", 48, Color.white);
        Text("Subtitle", _root, new Vector2(0, 651), new Vector2(780, 40), "회수하고 · 보관하고 · 다시 배치하기", 23, Muted);
        Box("Hint background", _root, new Vector2(0, 569), new Vector2(768, 78), Panel);
        _status = Text("Status", _root, new Vector2(0, 569), new Vector2(720, 70), "필드 토템을 눌러 회수해 보세요.", 22, Color.white);
        Box("Board rim", _root, new Vector2(0, 158), new Vector2(756, 756), new Color(.21f, .3f, .27f));
        Box("Board", _root, new Vector2(0, 158), new Vector2(748, 748), Ink);
        _cells = new Tile[settings.Columns * settings.Rows];
        float pitch = Mathf.Min(710f / settings.Columns, 710f / settings.Rows);
        for (int y = 0; y < settings.Rows; y++)
            for (int x = 0; x < settings.Columns; x++)
            {
                int i = y * settings.Columns + x;
                _cells[i] = MakeTile("Cell " + i, false, i,
                    new Vector2((x - (settings.Columns - 1) * .5f) * pitch, 158 + (y - (settings.Rows - 1) * .5f) * pitch), pitch - 10f);
            }
        Text("Board hint", _root, new Vector2(0, -244), new Vector2(780, 30), "범위는 선택한 토템 기준으로 표시됩니다", 19, Muted);
        Box("Detail", _root, new Vector2(0, -335), new Vector2(768, 130), Panel);
        _detailTitle = Text("Detail title", _root, new Vector2(0, -299), new Vector2(720, 35), "언제든 꺼내고, 다시 놓기", 26, Accent);
        _detailBody = Text("Detail body", _root, new Vector2(0, -351), new Vector2(714, 75), "", 21, Color.white);
        _recallButton = MakeButton("Recall", new Vector2(-193, -439), new Vector2(375, 66), "보관함으로 회수", Accent, out _);
        _rotateButton = MakeButton("Rotate", new Vector2(193, -439), new Vector2(375, 66), "방향 돌리기", new Color(.24f, .4f, .35f), out _);
        var storage = Box("Inventory drop area", _root, new Vector2(0, -597), new Vector2(810, 220), Panel);
        var pointer = storage.gameObject.AddComponent<TotemInventoryLabPointer>();
        pointer.Owner = this; pointer.Storage = true; pointer.Index = -1;
        storage.raycastTarget = true;
        Text("Inventory title", _root, new Vector2(-180, -514), new Vector2(360, 38), "토템 보관함", 27, Color.white);
        _count = Text("Count", _root, new Vector2(280, -514), new Vector2(160, 38), "3 / 5", 24, Accent);
        _slots = new Tile[settings.Capacity];
        for (int i = 0; i < _slots.Length; i++)
            _slots[i] = MakeTile("Stored " + i, true, i, new Vector2((i - (_slots.Length - 1) * .5f) * 148f, -606), 130f);
        Text("Inventory hint", _root, new Vector2(0, -685), new Vector2(760, 26), "여기로 끌면 회수  /  가득 차도 맞교체 가능", 18, Muted);
        _resetButton = MakeButton("Reset", new Vector2(-270, -752), new Vector2(247, 60), "처음 상태", Panel, out _);
        _fullButton = MakeButton("Full", new Vector2(0, -752), new Vector2(247, 60), "보관함 가득", Panel, out _);
        _fxButton = MakeButton("FX", new Vector2(270, -752), new Vector2(247, 60), "연출 켜짐", Panel, out _fxLabel);
        _dragGhost = Ghost("Drag ghost");
        _flightA = Ghost("Transfer incoming");
        _flightB = Ghost("Transfer outgoing");
        _state = new TotemInventoryLabState(settings);
        ResetSamples(false);
    }

    private Tile MakeTile(string name, bool storage, int index, Vector2 position, float size)
    {
        var frame = Box(name, _root, position, Vector2.one * size, new Color(.24f, .34f, .33f));
        frame.raycastTarget = true;
        var pointer = frame.gameObject.AddComponent<TotemInventoryLabPointer>();
        pointer.Owner = this; pointer.Storage = storage; pointer.Index = index;
        var bg = Box("Fill", frame.transform, Vector2.zero, Vector2.one * (size - 4f), new Color(.16f, .235f, .22f));
        var icon = Box("Icon", frame.transform, new Vector2(0, 10), new Vector2(size - 28f, size - 34f), Color.white);
        icon.preserveAspect = true;
        icon.enabled = false;
        var label = Text("Label", frame.transform, new Vector2(0, -size * .35f), new Vector2(size - 6, 28), storage ? "빈 칸" : "", 17, Muted);
        return new Tile { Rect = frame.rectTransform, Frame = frame, Background = bg, Icon = icon, Label = label };
    }
    private Image Ghost(string name)
    {
        var ghost = Box(name, _root, Vector2.zero, new Vector2(108, 108), Color.white);
        ghost.preserveAspect = true;
        ghost.gameObject.SetActive(false);
        return ghost;
    }
    private Button MakeButton(string name, Vector2 pos, Vector2 size, string label, Color color, out TMP_Text text)
    {
        var bg = Box(name, _root, pos, size, color);
        bg.raycastTarget = true;
        var button = bg.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        var colors = button.colors;
        colors.disabledColor = new Color(.4f, .45f, .43f, .5f);
        button.colors = colors;
        text = Text("Label", bg.transform, Vector2.zero, size - new Vector2(18, 4), label, 23, color == Accent ? Ink : Color.white);
        return button;
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
    private static Image Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
    private TMP_Text Text(string name, Transform parent, Vector2 position, Vector2 size, string value, float fontSize, Color color)
    {
        var text = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _font;
        text.fontSize = fontSize;
        text.color = color;
        text.text = value;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }
}
