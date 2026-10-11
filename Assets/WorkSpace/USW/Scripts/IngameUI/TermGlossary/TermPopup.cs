using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 용어 팝업 (사용자 확정 B안 2026-10-10): 아래에서 올라오는 바텀 시트 + 영상 용어는 화면 위쪽에 큰 필름 영상(design/예시.png 2026-10-11).
/// 열려 있는 동안 화면 전체 방패가 누름을 받아 바깥 탭은 닫기만 하고 카드 선택으로 이어지지 않는다.
/// 방패는 반투명 검정이라 시트·필름을 뺀 나머지를 어둡게 한다. 그 아래에 열 때 한 번 찍은 화면을 흐린 스냅숏을 깐다
/// (사용자 선택 2026-10-11: 실시간 블러 대신 스냅숏 — 비용은 열 때 1회, 메모리는 1/4 해상도 RT 2장).
/// 시트에 가린 카드를 밀어 올리는 일은 소유자(레벨업 화면)가 <see cref="SheetHeight"/>로 계산한다.
/// 시트 높이는 내용에 맞추고(최대 화면의 _sheetMaxRatio), 등장은 넘쳤다 돌아오는 연출에 제목·배지 팝, 본문 줄별 페이드.
/// </summary>
public sealed class TermPopup : MonoBehaviour
{
    private static readonly Color Cream = new Color(1f, 0.965f, 0.9f, 1f);
    private static readonly Color SheetColor = new Color(0.118f, 0.129f, 0.188f, 0.98f);
    private static readonly Color SheetText = new Color(0.87f, 0.85f, 0.79f, 1f);
    private static readonly Color Muted = new Color(0.56f, 0.58f, 0.66f, 1f);
    private static readonly Color FactColor = new Color(0.5f, 0.83f, 0.93f, 1f);
    // 넘쳤다 돌아오는 동안 아래 빈틈이 보이지 않게 시트를 화면 아래로 이만큼 더 길게 둔다.
    private const float SheetOverscan = 90f;

    [Header("시트 (캔버스 기준 단위)")]
    [SerializeField, Range(0.2f, 0.6f)] private float _sheetMaxRatio = 0.36f;
    [Tooltip("내용이 짧아도 이만큼은 올라온다")]
    [SerializeField, Min(0f)] private float _sheetMinHeight = 420f;
    [SerializeField, Min(0f)] private float _edgeHeight = 8f;

    [Header("영상 필름 (팝업이 열린 동안엔 카드를 고를 수 없으므로 카드를 가려도 된다)")]
    [Tooltip("필름 위 끝 위치 (화면 위에서부터 비율)")]
    [SerializeField, Range(0f, 0.4f)] private float _filmTopRatio = 0.12f;
    [Tooltip("필름 최대 폭 (화면 폭 비율). 시트에 닿을 만큼 길면 그만큼 줄인다")]
    [SerializeField, Range(0.4f, 1f)] private float _filmWidthRatio = 0.86f;
    [Tooltip("필름과 시트 사이 최소 간격")]
    [SerializeField, Min(0f)] private float _filmGap = 60f;

    [Header("어둡게")]
    [Tooltip("시트·필름 뒤 화면을 덮는 검정 투명도")]
    [SerializeField, Range(0f, 1f)] private float _dimAlpha = 0.6f;
    [SerializeField, Min(0.01f)] private float _dimFade = 0.2f;

    [Header("스냅숏 블러")]
    [Tooltip("끄면 어둡게만 한다. 블러 셰이더가 없어도 어둡게만 한다")]
    [SerializeField] private bool _blurBackdrop = true;
    [Tooltip("찍은 화면을 몇 분의 1 크기로 줄여 흐릴지 (클수록 가볍고 더 뭉개짐)")]
    [SerializeField, Range(1, 8)] private int _blurDownsample = 4;
    [Tooltip("흐림 반복 횟수 (Kawase, 반복마다 반경이 커진다)")]
    [SerializeField, Range(1, 8)] private int _blurPasses = 4;

    [Header("연출")]
    [SerializeField, Min(0.01f)] private float _openDuration = 0.34f;
    [Tooltip("시트가 살짝 넘쳤다 돌아오는 정도")]
    [SerializeField, Min(0f)] private float _overshoot = 1.15f;
    [SerializeField, Min(0.01f)] private float _closeDuration = 0.2f;
    [Tooltip("제목 줄이 튀어나오기 시작하는 시점 / 이후 줄마다 간격")]
    [SerializeField, Min(0f)] private float _contentDelay = 0.08f;
    [SerializeField, Min(0f)] private float _stagger = 0.05f;

    private RectTransform _root;
    private TMP_FontAsset _font;
    private Material _fontMaterial;
    private Sprite _rounded;
    private GameObject _shield;
    private Image _dim;
    private CanvasGroup _group;
    private RawImage _backdrop;
    private Material _blurMaterial;
    private RenderTexture _blurA, _blurB;
    private int _captureVersion;
    private static readonly int OffsetId = Shader.PropertyToID("_Offset");
    private RectTransform _sheet, _header, _badge, _chipRow;
    private Image _edge, _kindBg;
    private TMP_Text _kind, _title, _body, _facts, _chipLabel;
    private readonly List<CanvasGroup> _parts = new List<CanvasGroup>();
    private TermFilm _film;
    private bool _sheetOpen;
    private bool _closing;
    private float _sheetHeight;
    private DescriptionModeState _descriptionMode;
    private DescriptionModeToggle _descriptionToggle;
    private TermEntry _entry;

    /// <summary>Connects an optional externally owned preference; existing callers keep their original display.</summary>
    public void ConfigureDescriptionMode(DescriptionModeState state)
    {
        if (_descriptionMode != null) _descriptionMode.OnChanged -= RefreshDescription;
        _descriptionMode = state;
        if (state != null) state.OnChanged += RefreshDescription;
        if (_descriptionToggle == null && state != null)
            _descriptionToggle = DescriptionModeToggle.Create(_header, _body, _rounded, state);
        else if (_descriptionToggle != null) _descriptionToggle.Bind(state);
        if (_descriptionToggle != null) _descriptionToggle.gameObject.SetActive(state != null);
        RefreshDescription(state?.Current ?? DescriptionMode.Brief);
    }

    /// <summary>Current visible explanation, independent of the card that opened it.</summary>
    public string Body => _body != null ? _body.text : string.Empty;

    /// <summary>Raised when a mode change alters the sheet layout.</summary>
    public event Action OnLayoutChanged;

    private void RefreshDescription(DescriptionMode mode)
    {
        if (_entry == null || !IsOpen) return;
        _body.text = _descriptionMode == null ? _entry.Body : _entry.GetBody(mode);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_sheet);
        float max = _root.rect.height * _sheetMaxRatio;
        _sheetHeight = Mathf.Clamp(LayoutUtility.GetPreferredHeight(_sheet) - SheetOverscan, Mathf.Min(_sheetMinHeight, max), max);
        _sheet.sizeDelta = new Vector2(0f, _sheetHeight + SheetOverscan);
        if (_entry.Clip != null && _film.Rect.gameObject.activeSelf)
        {
            var area = _root.rect;
            _film.Relayout(area.yMax - area.height * _filmTopRatio, area.yMin + _sheetHeight + _filmGap,
                area.width * _filmWidthRatio, area.center);
        }
        OnLayoutChanged?.Invoke();
    }

    /// <summary>방패가 켜져 있는 동안 true.</summary>
    public bool IsOpen => _shield != null && _shield.activeSelf && !_closing;
    /// <summary>화면에 보이는 시트 높이 (캔버스 기준 단위). Show 때 정해진다.</summary>
    public float SheetHeight => _sheetHeight > 0f ? _sheetHeight : _root.rect.height * _sheetMaxRatio;
    /// <summary>영상 필름 (검증용).</summary>
    public TermFilm Film => _film;
    /// <summary>흐린 스냅숏이 깔려 있는지 (검증용).</summary>
    public bool BackdropShown => _backdrop != null && _backdrop.gameObject.activeSelf && _backdrop.texture != null;
    /// <summary>시트 루트 (검증용).</summary>
    public RectTransform Sheet => _sheet;
    /// <summary>현재 시트 제목 (검증용).</summary>
    public string Title => _title != null ? _title.text : string.Empty;
    /// <summary>현재 수치 줄 (검증용).</summary>
    public string Facts => _facts != null && _facts.gameObject.activeSelf ? _facts.text : string.Empty;
    /// <summary>바깥 탭으로 닫혔을 때.</summary>
    public event Action Closed;
    /// <summary>"이 카드의 다른 용어" 칩을 눌렀을 때 (용어 ID).</summary>
    public event Action<string> SiblingTapped;

    /// <summary>캔버스 맨 위에 팝업 층을 만든다. 글꼴은 카드 설명 글자와 같은 것을 쓴다. blurShader가 없으면 어둡게만 한다.</summary>
    public static TermPopup Create(RectTransform canvas, TMP_Text fontSource, Sprite rounded, Shader blurShader = null)
    {
        var root = NewRect("TermPopup", canvas);
        Stretch(root);
        root.SetAsLastSibling();
        var popup = root.gameObject.AddComponent<TermPopup>();
        popup._root = root;
        popup._font = fontSource.font;
        popup._fontMaterial = fontSource.fontSharedMaterial;
        popup._rounded = rounded;
        popup._group = root.gameObject.AddComponent<CanvasGroup>();
        if (blurShader != null && blurShader.isSupported) popup._blurMaterial = new Material(blurShader) { hideFlags = HideFlags.HideAndDontSave };
        popup.Build();
        popup._film = TermFilm.Create(root, popup._font, popup._fontMaterial);
        popup.HideImmediately();
        return popup;
    }

    /// <summary>용어를 보여 준다. 영상이 있으면 시트 바로 위에 필름도 띄운다. siblings = 같은 카드의 다른 용어.</summary>
    public void Show(TermEntry entry, IReadOnlyList<TermEntry> siblings)
    {
        if (entry == null) return;
        bool opening = !_shield.activeSelf;
        KillTweens();
        _closing = false;
        if (opening)
        {
            _dim.color = new Color(0f, 0f, 0f, 0f);
            _dim.DOFade(_dimAlpha, _dimFade).SetUpdate(true).SetLink(gameObject);
            if (_blurBackdrop && _blurMaterial != null)
            {
                // 이번 프레임은 팝업을 그리지 않고(방패는 누름을 계속 받음) 프레임 끝에 화면을 찍는다.
                _group.alpha = 0f;
                CaptureBackdropAsync(++_captureVersion).Forget();
            }
        }
        else
        {
            _dim.color = new Color(0f, 0f, 0f, _dimAlpha);
            _backdrop.color = Color.white;
        }
        _shield.SetActive(true);
        _kindBg.color = entry.KindColor;
        _kind.text = entry.Kind;
        _title.text = entry.DisplayName;
        _entry = entry;
        _body.text = _descriptionMode == null ? entry.Body : entry.GetBody(_descriptionMode.Current);
        string facts = entry.BuildFacts();
        _facts.gameObject.SetActive(!string.IsNullOrEmpty(facts));
        _facts.text = facts;
        BuildChips(siblings, entry.Id);
        _edge.color = entry.KindColor;

        _sheet.gameObject.SetActive(true);
        float max = _root.rect.height * _sheetMaxRatio;
        // 내용에 맞춘 높이 — 고정 높이일 때 아래 절반이 비어 늘어져 보이던 것을 줄인다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(_sheet);
        _sheetHeight = Mathf.Clamp(LayoutUtility.GetPreferredHeight(_sheet) - SheetOverscan, Mathf.Min(_sheetMinHeight, max), max);
        _sheet.sizeDelta = new Vector2(0f, _sheetHeight + SheetOverscan);
        if (!_sheetOpen) _sheet.anchoredPosition = new Vector2(0f, -(_sheetHeight + SheetOverscan));
        _sheetOpen = true;

        _sheet.DOAnchorPosY(-SheetOverscan, _openDuration).SetEase(Ease.OutBack, _overshoot).SetUpdate(true).SetLink(gameObject);
        _header.localScale = Vector3.one * 0.7f;
        _header.DOScale(1f, 0.3f).SetEase(Ease.OutBack, 2f).SetDelay(_contentDelay).SetUpdate(true).SetLink(gameObject);
        _badge.localScale = Vector3.zero;
        _badge.DOScale(1f, 0.25f).SetEase(Ease.OutBack, 3f).SetDelay(_contentDelay + 0.06f).SetUpdate(true).SetLink(gameObject);
        for (int i = 0; i < _parts.Count; i++)
        {
            _parts[i].alpha = 0f;
            _parts[i].DOFade(1f, 0.2f).SetDelay(_contentDelay + _stagger * (i + 1)).SetUpdate(true).SetLink(gameObject);
        }

        if (entry.Clip != null) ShowFilm(entry);
        else _film.Hide();
    }

    /// <summary>방패 탭과 같은 닫기 — 시트는 내려가며 사라진다.</summary>
    public void Close()
    {
        if (!IsOpen) return;
        KillTweens();
        _film.Hide();
        // 어둡게 한 막은 시트와 함께 걷힌다. 걷히는 동안에도 방패가 누름을 받아 닫는 손가락이 카드를 누르지 않는다.
        _closing = true;
        _captureVersion++; // 아직 안 끝난 찍기는 버린다
        _group.alpha = 1f;
        _backdrop.DOFade(0f, _closeDuration).SetUpdate(true).SetLink(gameObject);
        _dim.DOFade(0f, _closeDuration).SetUpdate(true).SetLink(gameObject).OnComplete(() =>
        {
            _shield.SetActive(false);
            _backdrop.gameObject.SetActive(false);
            _closing = false;
        });
        if (_sheetOpen)
        {
            _sheetOpen = false;
            _sheet.DOAnchorPosY(-(SheetHeight + SheetOverscan), _closeDuration).SetEase(Ease.InCubic).SetUpdate(true).SetLink(gameObject)
                  .OnComplete(() => _sheet.gameObject.SetActive(false));
        }
        Closed?.Invoke();
    }

    /// <summary>연출 없이 모두 숨긴다 (카드 다시 뽑기·화면 닫힘 등).</summary>
    public void HideImmediately()
    {
        KillTweens();
        _film.Hide();
        _shield.SetActive(false);
        _closing = false;
        _captureVersion++;
        if (_group != null) _group.alpha = 1f;
        _backdrop.gameObject.SetActive(false);
        _sheetOpen = false;
        _sheet.gameObject.SetActive(false);
    }

    // 화면 위쪽에 크게: 위 끝은 화면의 _filmTopRatio, 폭은 _filmWidthRatio, 아래는 시트 위 _filmGap까지.
    private void ShowFilm(TermEntry entry)
    {
        var area = _root.rect;
        float top = area.yMax - area.height * _filmTopRatio;
        float bottomLimit = area.yMin + _sheetHeight + _filmGap;
        if (!_film.Show(entry.Clip, top, bottomLimit, area.width * _filmWidthRatio, area.center))
            Debug.Log($"[TermPopup] 영상 자리 부족: {top - bottomLimit:0}");
    }

    // 팝업이 안 보이는 프레임 끝에 화면을 한 번 찍어 1/N로 줄이고 Kawase 블러를 몇 번 건 뒤 배경으로 깐다.
    private async UniTaskVoid CaptureBackdropAsync(int version)
    {
        try { await UniTask.WaitForEndOfFrame(this, this.GetCancellationTokenOnDestroy()); }
        catch (OperationCanceledException) { return; }
        if (version != _captureVersion || !_shield.activeSelf) return;

        int width = Mathf.Max(1, Screen.width / _blurDownsample), height = Mathf.Max(1, Screen.height / _blurDownsample);
        EnsureBlurTargets(width, height);
        var full = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
        ScreenCapture.CaptureScreenshotIntoRenderTexture(full);
        Graphics.Blit(full, _blurA);
        RenderTexture.ReleaseTemporary(full);
        var src = _blurA;
        var dst = _blurB;
        for (int i = 0; i < _blurPasses; i++)
        {
            _blurMaterial.SetFloat(OffsetId, i);
            Graphics.Blit(src, dst, _blurMaterial);
            (src, dst) = (dst, src);
        }
        _backdrop.texture = src;
        // 화면 찍기 결과는 그래픽 API에 따라 위아래가 뒤집힌다 (D3D·Metal·Vulkan = UV 위에서 시작).
        _backdrop.uvRect = SystemInfo.graphicsUVStartsAtTop ? new Rect(0f, 1f, 1f, -1f) : new Rect(0f, 0f, 1f, 1f);
        _backdrop.color = Color.white;
        _backdrop.gameObject.SetActive(true);
        _group.alpha = 1f;
    }

    private void EnsureBlurTargets(int width, int height)
    {
        if (_blurA != null && _blurA.width == width && _blurA.height == height) return;
        ReleaseBlurTargets();
        _blurA = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        _blurB = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
    }

    private void ReleaseBlurTargets()
    {
        if (_blurA != null) { _blurA.Release(); Destroy(_blurA); _blurA = null; }
        if (_blurB != null) { _blurB.Release(); Destroy(_blurB); _blurB = null; }
    }

    private void KillTweens()
    {
        _dim.DOKill();
        _backdrop.DOKill();
        _sheet.DOKill();
        // 중간에 끊긴 등장 연출이 반쯤 투명·축소된 채 남지 않게 최종 상태로 돌린다.
        _header.DOKill(); _header.localScale = Vector3.one;
        _badge.DOKill(); _badge.localScale = Vector3.one;
        foreach (var part in _parts) { part.DOKill(); part.alpha = 1f; }
    }

    private void OnDestroy()
    {
        if (_descriptionMode != null) _descriptionMode.OnChanged -= RefreshDescription;
        ReleaseBlurTargets();
        if (_blurMaterial != null) Destroy(_blurMaterial);
        if (_backdrop != null) _backdrop.DOKill();
        if (_sheet != null) _sheet.DOKill();
        if (_header != null) _header.DOKill();
        if (_badge != null) _badge.DOKill();
        foreach (var part in _parts) if (part != null) part.DOKill();
    }

    private void BuildChips(IReadOnlyList<TermEntry> siblings, string currentId)
    {
        for (int i = _chipRow.childCount - 1; i >= 0; i--)
        {
            var old = _chipRow.GetChild(i).gameObject;
            old.SetActive(false); // Destroy는 프레임 끝이라, 높이 계산에 끼지 않게 먼저 끈다
            Destroy(old);
        }
        int count = 0;
        if (siblings != null)
        {
            foreach (var other in siblings)
            {
                if (other == null || other.Id == currentId) continue;
                count++;
                var chip = NewRect("Chip_" + other.Id, _chipRow);
                var bg = chip.gameObject.AddComponent<Image>();
                bg.color = new Color(FactColor.r, FactColor.g, FactColor.b, 0.16f);
                ApplyRounded(bg);
                var layout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(26, 26, 10, 12);
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                var label = Text(chip, "Label", 30f, FactColor);
                label.text = other.DisplayName;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                string id = other.Id;
                chip.gameObject.AddComponent<Button>().onClick.AddListener(() => SiblingTapped?.Invoke(id));
            }
        }
        _chipLabel.gameObject.SetActive(count > 0);
        _chipRow.gameObject.SetActive(count > 0);
    }

    private void Build()
    {
        // 흐린 스냅숏 (맨 아래). 누름은 받지 않는다.
        var backdrop = NewRect("BlurBackdrop", _root);
        Stretch(backdrop);
        _backdrop = backdrop.gameObject.AddComponent<RawImage>();
        _backdrop.raycastTarget = false;
        backdrop.gameObject.SetActive(false);

        // 방패: 반투명 검정, 누름을 모두 받는다 — 바깥 탭은 닫기만 한다.
        var shield = NewRect("Shield", _root);
        Stretch(shield);
        _dim = shield.gameObject.AddComponent<Image>();
        _dim.color = Color.clear;
        var shieldButton = shield.gameObject.AddComponent<Button>();
        shieldButton.transition = Selectable.Transition.None;
        shieldButton.onClick.AddListener(Close);
        _shield = shield.gameObject;

        _sheet = NewRect("Sheet", _root);
        _sheet.anchorMin = new Vector2(0f, 0f);
        _sheet.anchorMax = new Vector2(1f, 0f);
        _sheet.pivot = new Vector2(0.5f, 0f);
        _sheet.gameObject.AddComponent<Image>().color = SheetColor;
        var layout = _sheet.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(60, 60, 26, 48 + (int)SheetOverscan);
        layout.spacing = 18f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var edge = NewRect("TopEdge", _sheet);
        _edge = edge.gameObject.AddComponent<Image>();
        edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        edge.anchorMin = new Vector2(0f, 1f);
        edge.anchorMax = Vector2.one;
        edge.pivot = new Vector2(0.5f, 1f);
        edge.sizeDelta = new Vector2(0f, _edgeHeight);
        edge.anchoredPosition = Vector2.zero;

        var grabRow = NewRect("GrabRow", _sheet);
        grabRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 12f;
        var grab = NewRect("Grab", grabRow);
        grab.anchorMin = grab.anchorMax = new Vector2(0.5f, 0.5f);
        grab.sizeDelta = new Vector2(100f, 10f);
        var grabImage = grab.gameObject.AddComponent<Image>();
        grabImage.color = new Color(0.29f, 0.31f, 0.39f, 1f);
        ApplyRounded(grabImage);

        _header = NewRect("Header", _sheet);
        _header.pivot = new Vector2(0f, 0.5f); // 제목 줄은 왼쪽 기준으로 튀어나온다
        var headerLayout = _header.gameObject.AddComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 16f;
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childControlWidth = headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = headerLayout.childForceExpandHeight = false;
        _badge = NewRect("Kind", _header);
        _kindBg = _badge.gameObject.AddComponent<Image>();
        ApplyRounded(_kindBg);
        var badgeLayout = _badge.gameObject.AddComponent<HorizontalLayoutGroup>();
        badgeLayout.padding = new RectOffset(16, 16, 6, 8);
        badgeLayout.childControlWidth = badgeLayout.childControlHeight = true;
        badgeLayout.childForceExpandWidth = badgeLayout.childForceExpandHeight = false;
        _kind = Text(_badge, "Label", 26f, Color.white);
        _kind.textWrappingMode = TextWrappingModes.NoWrap;
        _title = Text(_header, "Title", 50f, Cream);
        _title.fontStyle = FontStyles.Bold;
        _title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        _body = Text(_sheet, "Body", 37f, SheetText);
        _body.lineSpacing = 12f;
        _facts = Text(_sheet, "Facts", 32f, FactColor);
        _chipLabel = Text(_sheet, "ChipLabel", 26f, Muted);
        _chipLabel.text = "이 카드의 다른 용어";
        _chipRow = NewRect("Chips", _sheet);
        var chipLayout = _chipRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        chipLayout.spacing = 16f;
        chipLayout.childControlWidth = chipLayout.childControlHeight = true;
        chipLayout.childForceExpandWidth = chipLayout.childForceExpandHeight = false;
        chipLayout.childAlignment = TextAnchor.MiddleLeft;
        var hint = Text(_sheet, "Hint", 26f, Muted);
        hint.text = "바깥을 누르면 닫힘";
        foreach (var part in new Component[] { _body, _facts, _chipLabel, _chipRow, hint })
            _parts.Add(part.gameObject.AddComponent<CanvasGroup>());
    }

    private TMP_Text Text(Transform parent, string name, float size, Color color)
    {
        var text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) text.font = _font;
        if (_fontMaterial != null) text.fontSharedMaterial = _fontMaterial;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private void ApplyRounded(Image image)
    {
        if (_rounded == null) return;
        image.sprite = _rounded;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 0.35f;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
