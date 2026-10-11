using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>One owner-bound modal. Its shield consumes the whole dismissal gesture.</summary>
public sealed class DebuffInfoPopup : UI_Base, IDescriptionPopup
{
    private Behaviour _owner;
    private GameObject _ownerRoot;
    private Canvas[] _ownerCanvases;
    private TMP_Text _title;
    private TMP_Text _body;
    private ScrollRect _scroll;
    private GameObject _panel;
    private CanvasGroup _fadeGroup;
    private Sequence _transition;
    private bool _closeAnimationComplete;
    private const float TransitionSeconds = 0.3f;
    private const float SlideDistance = 60f;
    private RectTransform _safeArea;
    private bool _closing;
    private int _closedFrame;
    private Rect _lastSafeArea;
    private Vector2Int _lastSize;
    public bool BlocksOwnerInput => this != null && gameObject != null && gameObject.activeSelf;

    /// <summary>Creates a screen-sized modal using the information window's font and canvas scale.</summary>
    public static DebuffInfoPopup Create(UI_Base owner, TMP_Text source)
        => Create(owner, owner.gameObject, source);

    public static DebuffInfoPopup Create(Behaviour owner, GameObject ownerRoot, TMP_Text source)
    {
        var root = new GameObject("EffectInfoPopup", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, owner.gameObject.scene);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var ownerCanvas = owner.GetComponentInParent<Canvas>();
        canvas.sortingOrder = ownerCanvas != null ? Mathf.Max(1000, ownerCanvas.sortingOrder + 50) : 1000;
        var scaler = root.GetComponent<CanvasScaler>();
        var original = ownerCanvas != null ? ownerCanvas.rootCanvas.GetComponent<CanvasScaler>() : null;
        scaler.uiScaleMode = original != null ? original.uiScaleMode : CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = original != null ? original.referenceResolution : new Vector2(1080, 2340);
        scaler.screenMatchMode = original != null ? original.screenMatchMode : CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = original != null ? original.matchWidthOrHeight : 0.5f;
        scaler.scaleFactor = original != null ? original.scaleFactor : 1f;
        var view = root.AddComponent<DebuffInfoPopup>();
        view._owner = owner;
        view._ownerRoot = ownerRoot;
        view._ownerCanvases = owner.GetComponentsInParent<Canvas>(true);
        view._fadeGroup = root.AddComponent<CanvasGroup>();

        var shield = Rect(root.transform, "Backdrop", Vector2.zero, Vector2.one);
        var dim = shield.gameObject.AddComponent<Image>();
        dim.color = new Color(0, 0, 0, 0.45f);
        shield.gameObject.AddComponent<Button>().onClick.AddListener(view.Close);
        view._safeArea = Rect(root.transform, "SafeArea", Vector2.zero, Vector2.one);
        var panel = Rect(view._safeArea, "Panel", new Vector2(0.07f, 0.27f), new Vector2(0.93f, 0.73f));
        view._panel = panel.gameObject;
        var background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(0, 0, 0, 0.85f);
        view._title = Text(panel, "Title", source, 42f, new Vector2(0.06f, 0.83f), new Vector2(0.83f, 0.97f));
        view._title.fontStyle = FontStyles.Bold;
        view._title.alignment = TextAlignmentOptions.TopLeft;
        var close = Rect(panel, "Close", new Vector2(0.85f, 0.84f), new Vector2(0.98f, 0.98f));
        close.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.08f);
        close.gameObject.AddComponent<Button>().onClick.AddListener(view.Close);
        var closeText = Text(close, "Label", source, 42f, Vector2.zero, Vector2.one);
        closeText.text = "×"; closeText.alignment = TextAlignmentOptions.Center;

        var viewport = Rect(panel, "Viewport", new Vector2(0.06f, 0.07f), new Vector2(0.94f, 0.80f));
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        view._body = Text(viewport, "Body", source, 32f, new Vector2(0, 1), Vector2.one);
        view._body.rectTransform.pivot = new Vector2(0.5f, 1f);
        view._body.alignment = TextAlignmentOptions.TopLeft;
        view._body.lineSpacing = 8f;
        view._body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        view._scroll = viewport.gameObject.AddComponent<ScrollRect>();
        view._scroll.viewport = viewport;
        view._scroll.content = view._body.rectTransform;
        view._scroll.horizontal = false;
        view._scroll.movementType = ScrollRect.MovementType.Clamped;
        view.ApplySafeArea();
        root.SetActive(false);
        return view;
    }

    /// <summary>Replaces existing content; never stacks another popup or requests another pause.</summary>
    public void Show(DebuffInfoModel model)
        => Show(model.Title, model.Body);

    public void Show(string title, string body)
    {
        _closing = false;
        _panel.SetActive(true);
        _title.text = title;
        _body.text = body;
        gameObject.SetActive(true);
        _canvas.enabled = true;
        ApplySafeArea();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_scroll.content);
        _scroll.StopMovement();
        _scroll.verticalNormalizedPosition = 1f;
        _transition?.Kill();
        _fadeGroup.interactable = true;
        _fadeGroup.alpha = 0f;
        var panelRect = (RectTransform)_panel.transform;
        panelRect.anchoredPosition = Vector2.down * SlideDistance;
        _transition = DOTween.Sequence().SetUpdate(true)
            .Join(_fadeGroup.DOFade(1f, TransitionSeconds).SetEase(Ease.OutQuad))
            .Join(panelRect.DOAnchorPos(Vector2.zero, TransitionSeconds).SetEase(Ease.OutQuad));
    }

    /// <summary>Slides and fades out, keeping the shield through the animation and pointer releases.</summary>
    public override void Close()
    {
        if (this == null || gameObject == null || !gameObject.activeSelf || _closing) return;
        _transition?.Kill();
        _closing = true;
        _closeAnimationComplete = false;
        _closedFrame = Time.frameCount;
        _fadeGroup.interactable = false;
        _transition = DOTween.Sequence().SetUpdate(true)
            .Join(_fadeGroup.DOFade(0f, TransitionSeconds).SetEase(Ease.InQuad))
            .Join(((RectTransform)_panel.transform).DOAnchorPos(Vector2.down * SlideDistance, TransitionSeconds).SetEase(Ease.InQuad))
            .OnComplete(() => _closeAnimationComplete = true);
        if (_scroll != null) _scroll.StopMovement();
    }
    /// <summary>UI_Base close buttons use the same guarded close path.</summary>
    public override UniTask CloseAsync() { Close(); return UniTask.CompletedTask; }

    /// <summary>Owner closure/target replacement cannot leave stale content alive.</summary>
    public void HideImmediately()
    {
        _closing = false;
        if (this == null || gameObject == null) return;
        _transition?.Kill();
        if (_scroll != null) _scroll.StopMovement();
        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (_owner == null || !_owner.isActiveAndEnabled || _ownerRoot == null || !_ownerRoot.activeInHierarchy || OwnerCanvasHidden())
        { HideImmediately(); return; }
        if (_closing && _closeAnimationComplete && Time.frameCount > _closedFrame && Input.touchCount == 0 && !Input.GetMouseButton(0)) HideImmediately();
        ApplySafeArea();
    }

    private bool OwnerCanvasHidden()
    {
        foreach (var canvas in _ownerCanvases) if (canvas != null && !canvas.enabled) return true;
        return false;
    }
    private void OnApplicationFocus(bool focused) { if (!focused) HideImmediately(); }
    private void OnApplicationPause(bool paused) { if (paused) HideImmediately(); }
    private void OnDestroy() => _transition?.Kill();

    private void ApplySafeArea()
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (size.x <= 0 || size.y <= 0 || (_lastSafeArea == Screen.safeArea && _lastSize == size)) return;
        _lastSafeArea = Screen.safeArea; _lastSize = size;
        _safeArea.anchorMin = new Vector2(_lastSafeArea.xMin / size.x, _lastSafeArea.yMin / size.y);
        _safeArea.anchorMax = new Vector2(_lastSafeArea.xMax / size.x, _lastSafeArea.yMax / size.y);
        _safeArea.offsetMin = _safeArea.offsetMax = Vector2.zero;
    }
    private static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    private static TMP_Text Text(Transform parent, string name, TMP_Text source, float size, Vector2 min, Vector2 max)
    {
        var text = Rect(parent, name, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = source.font;
        text.fontSharedMaterial = source.fontSharedMaterial;
        text.fontSize = size;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }
}
