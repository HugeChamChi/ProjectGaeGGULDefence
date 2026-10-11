using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VContainer;

/// <summary>실제 인게임 그리드에서 봉인 사선과 여러 패턴 시안을 비교하는 독립 실험실.</summary>
public sealed class SealedGridShaderLab : MonoBehaviour
{
    private const int PreviewCellCount = 4;
    private const float TileInset = .92f;
    private const float CanvasWorldScale = .01f;
    private static readonly Color PanelColor = new Color(.045f, .075f, .10f, .97f);
    private static readonly Color ButtonColor = new Color(.12f, .19f, .24f, 1f);
    private static readonly Color SelectedColor = new Color(.20f, .42f, .44f, 1f);
    [SerializeField] private Material _sourceMaterial;
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private SealedGridShaderVariant[] _variants = Array.Empty<SealedGridShaderVariant>();
    [Inject] private GridManager _grid;
    [Inject] private UnitFactory _factory;
    [Inject] private UnitSpawner _spawner;
    [Inject] private UIManager _ui;
    [Inject] private GameInitializer _initializer;
    [Inject] private TimeScaleService _timeScale;
    private readonly GridCell[] _cells = new GridCell[PreviewCellCount];
    private readonly SpriteRenderer[] _renderers = new SpriteRenderer[PreviewCellCount];
    private readonly Canvas[] _symbols = new Canvas[PreviewCellCount];
    private readonly SealedGridPatternGraphic[] _symbolGraphics = new SealedGridPatternGraphic[PreviewCellCount];
    private Button[] _variantButtons;
    private Button _motionButton, _symbolButton, _areaButton;
    private TMP_Text _description;
    private Material _material;
    private Sprite _sprite;
    private Texture2D _texture;
    private int _selected = 4;
    private bool _moving = true, _showSymbol = true, _sealed = true;

    /// <summary>실험실 초기화 완료 여부.</summary>
    public bool IsReady { get; private set; }
    /// <summary>시안 수.</summary>
    public int VariantCount => _variants.Length;
    /// <summary>현재 시안 인덱스.</summary>
    public int SelectedIndex => _selected;
    /// <summary>사선 이동 여부.</summary>
    public bool IsMoving => _moving && CurrentPattern == SealedGridPatternKind.MovingStripes;
    /// <summary>현재 선택한 봉인 패턴.</summary>
    public SealedGridPatternKind CurrentPattern => _variants.Length > _selected && _variants[_selected] != null
        ? _variants[_selected].PatternKind : SealedGridPatternKind.MovingStripes;
    /// <summary>자물쇠 표시 여부.</summary>
    public bool IsSymbolVisible => _showSymbol;
    /// <summary>미리보기 칸의 봉인 여부.</summary>
    public bool IsSealed => _sealed;
    /// <summary>사선 렌더러들이 공유하는 실험실 소유 재질.</summary>
    public Material ActiveMaterial => _material;
    /// <summary>실제 전장에 연결한 미리보기 칸.</summary>
    public System.Collections.Generic.IReadOnlyList<GridCell> PreviewCells => _cells;

    private void Start() => InitializeAsync().Forget();

    private async UniTaskVoid InitializeAsync()
    {
        var token = this.GetCancellationTokenOnDestroy();
        try
        {
            if (_variants == null || _variants.Length == 0 || Array.Exists(_variants, value => value == null))
                throw new InvalidOperationException("SealedGridShaderLab: 시안 데이터 참조가 없습니다.");
            _selected = Mathf.Clamp(_selected, 0, _variants.Length - 1);
            BuildUi();
            if (_initializer == null || _grid == null || _sourceMaterial == null)
                throw new InvalidOperationException("SealedGridShaderLab: 씬 DI 또는 재질 참조가 없습니다.");
            await UniTask.WaitUntil(() => _initializer.IsReady, cancellationToken: token);
            await UniTask.WaitUntil(AllPreviewUnitsLoaded, cancellationToken: token);
            _ui?.HideStartButton();
            BuildTiles();
            SpawnPreviewUnits();
            Refresh();
            await UniTask.Delay(700, cancellationToken: token);
            _timeScale?.Pause(this);
            IsReady = true;
            SelectVariant(Mathf.Clamp(_selected, 0, _variants.Length - 1));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            if (_description != null) _description.text = "실험실 초기화 실패 · Console을 확인하세요";
        }
    }

    private bool AllPreviewUnitsLoaded()
    {
        var data = _factory?.UnitDataList;
        if (data == null || data.Length == 0) return false;
        int found = 0;
        foreach (var unit in data)
        {
            if (unit == null) continue;
            if (unit.prefab == null) return false;
            if (++found == 3) return true;
        }
        return found > 0;
    }

    /// <summary>선택한 시안의 회색 채움과 봉인 패턴을 적용한다.</summary>
    public void SelectVariant(int index)
    {
        if (index < 0 || index >= _variants.Length || _variants[index] == null) return;
        _selected = index;
        Refresh();
    }

    /// <summary>사선 이동을 켜거나 끈다. 봉인 판정은 유지한다.</summary>
    public void ToggleMotion() { _moving = !_moving; Refresh(); }
    /// <summary>작은 자물쇠만 켜거나 끈다.</summary>
    public void ToggleSymbol() { _showSymbol = !_showSymbol; Refresh(); }
    /// <summary>네 칸의 봉인 상태를 해제하거나 복원한다.</summary>
    public void ToggleSeal() { _sealed = !_sealed; Refresh(); }

    private void Refresh()
    {
        if (_description == null || _variants.Length == 0) return;
        var variant = _variants[_selected];
        bool stripes = variant.PatternKind == SealedGridPatternKind.MovingStripes;
        if (IsReady) _description.text = variant.Description;
        if (_material != null)
        {
            _material.SetColor("_FillColor", variant.FillColor);
            _material.SetColor("_StripeColor", stripes ? variant.StripeColor : variant.FillColor);
            _material.SetFloat("_StripeSpacing", variant.StripeSpacing);
            _material.SetFloat("_StripeWidth", variant.StripeWidth);
            _material.SetFloat("_ScrollSpeed", stripes && _moving ? variant.ScrollSpeed : 0f);
        }
        for (int i = 0; i < PreviewCellCount; i++)
        {
            if (_cells[i] != null) _cells[i].Model.SetSealed(_sealed);
            if (_renderers[i] != null) _renderers[i].gameObject.SetActive(_sealed);
            if (_symbols[i] != null) _symbols[i].gameObject.SetActive(_sealed);
            if (_symbolGraphics[i] != null) _symbolGraphics[i].Configure(variant, _showSymbol);
        }
        for (int i = 0; i < _variantButtons.Length; i++)
            _variantButtons[i].targetGraphic.color = i == _selected ? SelectedColor : ButtonColor;
        _motionButton.interactable = stripes;
        SetLabel(_motionButton, stripes ? (_moving ? "사선 이동 ON" : "사선 이동 OFF") : "고정 패턴", stripes && _moving);
        SetLabel(_symbolButton, _showSymbol ? "자물쇠 ON" : "자물쇠 OFF", _showSymbol);
        SetLabel(_areaButton, _sealed ? "전체 해제" : "봉인 복원", false);
    }

    private void BuildTiles()
    {
        _material = new Material(_sourceMaterial) { name = "SealedGridLab_SharedMovingStripes" };
        _material.SetFloat("_UseSpriteAlpha", 0f);
        _material.SetFloat("_StripeAngle", -45f);
        _material.SetColor("_Color", Color.white);
        _texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "SealedGridLab_White" };
        _texture.SetPixel(0, 0, Color.white); _texture.Apply();
        _sprite = Sprite.Create(_texture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1f);
        int baseX = Mathf.Clamp(_grid.Columns / 2 - 1, 0, _grid.Columns - 2);
        int baseY = Mathf.Clamp(_grid.Rows / 2 - 1, 0, _grid.Rows - 2);
        for (int i = 0; i < PreviewCellCount; i++)
        {
            var cell = _grid.GetCell(baseX + i % 2, baseY + i / 2);
            if (cell == null || cell.OccupyingUnit != null || cell.OccupyingTotem != null)
                throw new InvalidOperationException("봉인 비교 칸은 비어 있어야 합니다.");
            _cells[i] = cell;
            var original = cell.GetComponentInChildren<SpriteRenderer>();
            var bounds = original.bounds;
            var tile = new GameObject("MovingSeal_" + i, typeof(SpriteRenderer));
            tile.transform.SetParent(transform, false); tile.transform.position = bounds.center;
            tile.transform.localScale = new Vector3(bounds.size.x * TileInset, bounds.size.y * TileInset, 1f);
            var renderer = tile.GetComponent<SpriteRenderer>();
            renderer.sprite = _sprite; renderer.sharedMaterial = _material;
            renderer.sortingLayerID = original.sortingLayerID; renderer.sortingOrder = original.sortingOrder + 1;
            _renderers[i] = renderer;
            var canvasObject = new GameObject("SealSymbolCanvas_" + i, typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false); canvasObject.transform.position = bounds.center;
            canvasObject.transform.localScale = Vector3.one * CanvasWorldScale;
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera.main;
            canvas.sortingLayerID = original.sortingLayerID; canvas.sortingOrder = original.sortingOrder + 2;
            ((RectTransform)canvasObject.transform).sizeDelta = new Vector2(bounds.size.x, bounds.size.y) / CanvasWorldScale;
            _symbols[i] = canvas;
            var symbol = Rect("SmallLock_NoText_" + i, canvasObject.transform, Vector2.zero, Vector2.one);
            var graphic = symbol.gameObject.AddComponent<SealedGridPatternGraphic>();
            graphic.raycastTarget = false; graphic.Configure(_variants[_selected], _showSymbol);
            _symbolGraphics[i] = graphic;
        }
    }

    private void SpawnPreviewUnits()
    {
        int placed = 0;
        foreach (var data in _factory.UnitDataList)
        {
            if (data == null) continue;
            Vector2Int position = placed == 0 ? new Vector2Int(1, 0)
                : placed == 1 ? new Vector2Int(_grid.Columns - 2, 1) : new Vector2Int(1, _grid.Rows - 1);
            var cell = _grid.GetCell(position);
            if (cell != null && cell.IsAvailable && cell.OccupyingUnit == null)
            {
                var unit = _factory.CreateUnitFromData(data);
                if (unit != null) _spawner.PlaceUnitWithEffect(unit, cell);
            }
            if (++placed == 3) break;
        }
    }

    private void BuildUi()
    {
        var canvasObject = new GameObject("SealedGridVariantCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 300;
        var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(540f, 1168f); scaler.matchWidthOrHeight = .5f;
        var panel = Rect("SealVariantPanel", canvasObject.transform, new Vector2(.04f, .53f), new Vector2(.96f, .88f));
        var image = panel.gameObject.AddComponent<Image>(); image.color = PanelColor; image.raycastTarget = false;
        Text(panel, "SealLabTitle", "봉인 · 패턴 비교", new Vector2(.03f, .88f), new Vector2(.97f, .98f), 25f, FontStyles.Bold);
        _description = Text(panel, "SealVariantDescription", "전장을 준비하고 있습니다", new Vector2(.03f, .79f), new Vector2(.97f, .88f), 15f);
        _variantButtons = new Button[_variants.Length];
        for (int i = 0; i < _variants.Length; i++)
        {
            int choice = i; float x = .025f + (i % 3) * .32f; float y = .62f - (i / 3) * .155f;
            string label = i < 4 ? new[] { "A · 밝은 사선", "B · 사선 잠금", "C · 촘촘 사선", "D · 진한 사선" }[i] : _variants[i].DisplayName;
            _variantButtons[i] = Button(panel, "SealStyle_" + i, label,
                new Vector2(x, y), new Vector2(x + .31f, y + .14f), () => SelectVariant(choice));
        }
        _motionButton = Button(panel, "SealMotion", "", new Vector2(.025f, .02f), new Vector2(.325f, .13f), ToggleMotion);
        _symbolButton = Button(panel, "SealSymbol", "", new Vector2(.35f, .02f), new Vector2(.65f, .13f), ToggleSymbol);
        _areaButton = Button(panel, "SealArea", "", new Vector2(.675f, .02f), new Vector2(.975f, .13f), ToggleSeal);
        Refresh();
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
    }

    private TMP_Text Text(Transform parent, string name, string value, Vector2 min, Vector2 max, float size, FontStyles style = FontStyles.Normal)
    {
        var rect = Rect(name, parent, min, max); var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _font; text.text = value; text.fontSize = size; text.fontStyle = style;
        text.color = new Color(.92f, .96f, .97f); text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.enableAutoSizing = true; text.fontSizeMin = size * .8f; text.fontSizeMax = size;
        text.overflowMode = TextOverflowModes.Ellipsis; return text;
    }

    private Button Button(Transform parent, string name, string label, Vector2 min, Vector2 max, UnityAction action)
    {
        var rect = Rect(name, parent, min, max); var image = rect.gameObject.AddComponent<Image>(); image.color = ButtonColor;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None }; button.onClick.AddListener(action);
        Text(rect, "Label", label, new Vector2(.025f, .02f), new Vector2(.975f, .98f), 17f);
        return button;
    }

    private static void SetLabel(Button button, string value, bool selected)
    {
        button.GetComponentInChildren<TMP_Text>().text = value;
        button.targetGraphic.color = selected ? SelectedColor : ButtonColor;
    }

    private void OnDestroy()
    {
        _timeScale?.Release(this);
        foreach (var cell in _cells) if (cell != null && cell.Model != null) cell.Model.SetSealed(false);
        if (_material != null) Destroy(_material);
        if (_sprite != null) Destroy(_sprite);
        if (_texture != null) Destroy(_texture);
    }
}
