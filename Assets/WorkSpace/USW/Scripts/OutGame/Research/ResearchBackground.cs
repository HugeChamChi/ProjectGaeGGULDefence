using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum ResearchBackgroundDesign { Original, ReferenceBlue, CyanBand, VioletBand, DeepBlueBand }

public enum ResearchBackgroundPattern { Still, AlternatingUp, StaggeredDiagonal, OpposingDiagonals, RowCrossUp, SmoothFrogs, SilhouetteDiagonal }

/// <summary>뷰포트 배경을 MAX 높이에서 자르고, 그 안에서 아이콘 패턴을 흘린다. 저장값은 변경하지 않는다.</summary>
public sealed class ResearchBackground : MonoBehaviour
{
    private readonly List<Image> _icons = new();
    private RectTransform _viewport, _content, _reveal, _art;
    private Image _backgroundArt;
    private ResearchViewSettings _settings;
    private ResearchProgress _progress;
    private float _elapsed;
    private RectTransform _bottomFade, _frontier;
    private float _displayRow, _rowFrom, _rowElapsed = 0.35f;
    private int _targetRow;
    private const float RiseSeconds = 0.35f;
    public ResearchBackgroundPattern Pattern { get; private set; }
    public bool Preview { get; set; }
    public float RevealHeight => _reveal.sizeDelta.y;

    public void Initialize(RectTransform viewport, RectTransform content, ResearchViewSettings settings, ResearchProgress progress)
    {
        _viewport = viewport;
        _content = content;
        _settings = settings;
        _progress = progress;
        _displayRow = _targetRow = progress.HighestMaxedRow;
        _progress.OnChanged += ProgressChanged;
        Pattern = settings.BackgroundPattern;
        var dark = ResearchUi.NewImage(transform, "Unfinished", null, Vector2.zero, Vector2.zero);
        ResearchUi.Stretch(dark.rectTransform);
        dark.color = settings.UnfinishedBackground;
        _reveal = ResearchUi.NewRect(transform, "MaxReveal");
        _reveal.anchorMin = Vector2.zero;
        _reveal.anchorMax = new Vector2(1f, 0f);
        _reveal.pivot = new Vector2(0.5f, 0f);
        _reveal.gameObject.AddComponent<RectMask2D>();
        bool silhouette = Pattern == ResearchBackgroundPattern.SilhouetteDiagonal;
        bool useArt = !silhouette && (settings.BackgroundDesign == ResearchBackgroundDesign.Original || Pattern == ResearchBackgroundPattern.SmoothFrogs);
        var background = ResearchUi.NewImage(_reveal, "Art", useArt ? settings.PanelBackground : null, Vector2.zero, Vector2.zero);
        background.color = background.sprite != null ? Color.white : settings.Background;
        if (Pattern == ResearchBackgroundPattern.SmoothFrogs) background.color = new Color(0.55f, 1f, 0.88f, 1f);
        _art = background.rectTransform;
        _backgroundArt = background;
        if (background.sprite != null) background.type = Image.Type.Tiled;
        _art.anchorMin = _art.anchorMax = new Vector2(0.5f, 0f);
        _art.pivot = new Vector2(0.5f, 0f);
        if (settings.BackgroundDesign != ResearchBackgroundDesign.Original)
        {
            var vignetteRect = ResearchUi.Stretch(ResearchUi.NewRect(transform, "Vignette"));
            var vignette = vignetteRect.gameObject.AddComponent<ResearchVignetteGraphic>();
            vignette.raycastTarget = false;
            vignette.SetStrength(settings.BackgroundVignetteStrength);
            var shade = ResearchUi.Stretch(ResearchUi.NewRect(_art, "SideShade"));
            shade.gameObject.AddComponent<ResearchGradientGraphic>().Configure(ResearchGradientGraphic.Shape.RightShade, new Color(0f, 0f, 0f, 0.18f));
            _bottomFade = ResearchUi.NewRect(viewport, "BlackBottomFade");
            _bottomFade.anchorMin = Vector2.zero;
            _bottomFade.anchorMax = new Vector2(1f, 0f);
            _bottomFade.pivot = new Vector2(0.5f, 0f);
            _bottomFade.gameObject.AddComponent<ResearchGradientGraphic>().Configure(ResearchGradientGraphic.Shape.BottomFade,
                new Color(0f, 0f, 0f, 0.45f));
            var line = ResearchUi.NewImage(_reveal, "MaxFrontierLine", null, Vector2.zero, Vector2.zero);
            line.color = settings.BackgroundBandColor;
            _frontier = line.rectTransform;
            _frontier.anchorMin = new Vector2(0f, 1f);
            _frontier.anchorMax = Vector2.one;
            _frontier.pivot = new Vector2(0.5f, 1f);
            _frontier.sizeDelta = new Vector2(0f, 4f);
        }
    }

    public void SetPattern(ResearchBackgroundPattern pattern)
    {
        Pattern = pattern;
        _elapsed = 0f;
    }

    public static string Label(ResearchBackgroundPattern pattern) => pattern switch
    {
        ResearchBackgroundPattern.Still => "정지",
        ResearchBackgroundPattern.AlternatingUp => "교차 상승",
        ResearchBackgroundPattern.StaggeredDiagonal => "엇갈린 대각선",
        ResearchBackgroundPattern.OpposingDiagonals => "양방향 대각선",
        ResearchBackgroundPattern.RowCrossUp => "줄마다 좌우 교차 상승",
        ResearchBackgroundPattern.SilhouetteDiagonal => "개구리 실루엣 대각선",
        _ => string.Empty
    };

    private void LateUpdate()
    {
        if (_settings == null) return;
        _elapsed += Time.unscaledDeltaTime;
        if (_rowElapsed < RiseSeconds)
        {
            _rowElapsed = Mathf.Min(RiseSeconds, _rowElapsed + Time.unscaledDeltaTime);
            _displayRow = Mathf.Lerp(_rowFrom, _targetRow, Mathf.SmoothStep(0f, 1f, _rowElapsed / RiseSeconds));
        }
        RefreshVisuals();
    }

    /// <summary>스크롤·화면 크기·강화·초기화 변경을 같은 좌표계로 반영한다.</summary>
    public void RefreshVisuals()
    {
        float width = _viewport.rect.width, height = _viewport.rect.height;
        int maxRow = _progress.HighestMaxedRow;
        float fill = 0f;
        if (maxRow >= 0)
        {
            float top = _content.rect.yMin + _settings.ContentPadding + _displayRow * _settings.RowSpacing;
            fill = _viewport.InverseTransformPoint(_content.TransformPoint(new Vector3(0f, top))).y - _viewport.rect.yMin;
        }
        _reveal.sizeDelta = new Vector2(0f, Preview ? height : maxRow < 0 ? 0f : Mathf.Clamp(fill, 0f, height));
        bool silhouette = Pattern == ResearchBackgroundPattern.SilhouetteDiagonal;
        float spacing = Mathf.Max(80f, _settings.BackgroundIconRowSpacing);
        // 실루엣은 보이는 영역만 채운다. 긴 트리 전체에 장식 오브젝트를 만들지 않는다.
        float contentHeight = silhouette ? height + spacing * 2f : Mathf.Max(height, _content.rect.height);
        float contentBottom = _viewport.InverseTransformPoint(_content.TransformPoint(new Vector3(0f, _content.rect.yMin))).y - _viewport.rect.yMin;
        _art.sizeDelta = new Vector2(width, contentHeight);
        _art.anchoredPosition = new Vector2(0f, silhouette ? -spacing : contentBottom);
        if (_backgroundArt.sprite != null && width > 0f)
            _backgroundArt.pixelsPerUnitMultiplier = _backgroundArt.sprite.rect.width / (width * _backgroundArt.pixelsPerUnit);
        if (_bottomFade != null)
        {
            _bottomFade.SetAsLastSibling();
            _bottomFade.sizeDelta = new Vector2(0f, Mathf.Min(_settings.BackgroundBottomFadeHeight, RevealHeight));
            _frontier.gameObject.SetActive(RevealHeight > 0f);
        }

        int rows = Mathf.CeilToInt((contentHeight + _settings.BackgroundIconSize * 2f) / spacing) + 1;
        if ((rows & 1) != 0) rows++; // 반복 경계에서도 A/B 행 교차를 유지한다.
        bool rowCross = Pattern == ResearchBackgroundPattern.RowCrossUp;
        bool frogs = Pattern == ResearchBackgroundPattern.SmoothFrogs;
        int columns = rowCross || frogs || silhouette ? 3 : 2;
        int count = rows * columns;
        while (_icons.Count < count)
        {
            var icon = ResearchUi.NewImage(_art, frogs ? "FloatingFrog" : "PatternIcon", null, Vector2.zero, Vector2.zero);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _icons.Add(icon);
        }
        float travel = _elapsed * _settings.BackgroundIconSpeed;
        float periodY = rows * spacing;
        float periodX = width + _settings.BackgroundIconSize * 2f;
        for (int i = 0; i < _icons.Count; i++)
        {
            var icon = _icons[i];
            bool visible = i < count && Pattern != ResearchBackgroundPattern.Still;
            if (icon.gameObject.activeSelf != visible) icon.gameObject.SetActive(visible);
            if (!visible) continue;
            int row = i / columns, column = i % columns;
            int kind = rowCross ? row % 2 : (row + column) % 2;
            Sprite sprite = kind == 0 ? _settings.BackgroundIconA : _settings.BackgroundIconB;
            if (sprite == null) sprite = silhouette ? _settings.BackgroundIconA : kind == 0 ? _settings.UpgradeArrow : _settings.GoldIcon;
            if (icon.sprite != sprite) icon.sprite = sprite;
            icon.color = _settings.BackgroundIconTint;
            float x = (column == 0 ? -0.27f : 0.27f) * width;
            float y = Mathf.Repeat(row * spacing + travel, periodY) - spacing;
            if (silhouette)
            {
                float cellWidth = periodX / columns;
                x = ((column + 0.5f) / columns - 0.5f) * periodX + (row % 2) * cellWidth * 0.5f + travel * 0.55f;
                y = Mathf.Repeat(row * spacing + contentBottom + travel, periodY) - spacing;
                icon.rectTransform.localRotation = Quaternion.identity;
            }
            else if (frogs)
            {
                float phase = i * 2.39996f;
                float speed = 0.7f + Mathf.Repeat(i * 0.37f, 0.6f);
                x = ((column + 0.5f) / columns - 0.5f) * width + Mathf.Sin(_elapsed * 0.45f + phase) * width * 0.065f;
                y = Mathf.Repeat(row * spacing + Mathf.Repeat(i * 73f, spacing * 0.6f) + travel * speed, periodY) - spacing;
                icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase) * 55f + Mathf.Sin(_elapsed * 0.22f + phase) * 8f);
            }
            else if (rowCross)
                x = ((column + 0.5f) / columns - 0.5f) * periodX + (row % 2 == 0 ? 1f : -1f) * travel * 0.55f;
            else if (Pattern == ResearchBackgroundPattern.StaggeredDiagonal)
                x += (row % 2) * width * 0.15f + travel * 0.45f;
            else if (Pattern == ResearchBackgroundPattern.OpposingDiagonals)
                x += (kind == 0 ? 1f : -1f) * travel * 0.55f;
            x = Mathf.Repeat(x + periodX * 0.5f, periodX) - periodX * 0.5f;
            icon.rectTransform.anchoredPosition = new Vector2(x, y);
            icon.rectTransform.sizeDelta = Vector2.one * _settings.BackgroundIconSize * (frogs ? Mathf.Lerp(_settings.BackgroundSmallIconScale, 1f, Mathf.Repeat(i * 0.618f, 1f)) : kind == 0 ? 1f : _settings.BackgroundSmallIconScale);
        }
    }

    private void ProgressChanged(ResearchNodeData node)
    {
        int next = _progress.HighestMaxedRow;
        if (next == _targetRow) return;
        bool rise = _settings.BackgroundDesign != ResearchBackgroundDesign.Original && next > _targetRow;
        _rowFrom = _targetRow < 0 ? -_settings.ContentPadding / _settings.RowSpacing : _displayRow;
        _targetRow = next;
        _rowElapsed = rise ? 0f : RiseSeconds;
        _displayRow = rise ? _rowFrom : next;
    }

    private void OnEnable()
    {
        if (_bottomFade != null) _bottomFade.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (_bottomFade != null) _bottomFade.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_bottomFade != null)
        {
            if (Application.isPlaying) Destroy(_bottomFade.gameObject);
            else DestroyImmediate(_bottomFade.gameObject);
        }
        if (_progress != null) _progress.OnChanged -= ProgressChanged;
    }

}
