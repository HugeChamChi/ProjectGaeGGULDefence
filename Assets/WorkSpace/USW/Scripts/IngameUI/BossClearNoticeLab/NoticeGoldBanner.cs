using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static TimerBonusEase;

/// <summary>
/// 공지 B 골드 배너 — 참고 GIF 2(보스 클리어! + 빠른 처치 보너스)형, 사용자 피드백(2026-10-02 "디자인적으로 짜침")으로 다듬은 판.
/// 띠는 단색 대신 층을 쌓는다: 그림자 → 주황 바탕 → 밝은 윗면 → 광택 줄 → 얇은 어두운 테두리 (+ 열리는 순간 흰 번쩍임, 도장 뒤 빛 한 줄 스윕).
/// 글자는 참고 GIF 2처럼 크림색 + 두꺼운 진갈색 외곽선(제목은 아래 그림자로 띄움).
/// 둘째 줄도 GIF 2처럼 띠와 같은 폭의 밝은 노란 줄이 띠 바로 아래로 툭 펼쳐지고, 그 위에 작은 글자 '남은 시간 +15초'(값만 조금 크게).
/// 사라질 땐 노란 줄이 먼저 접히고 띠가 한 줄로 납작해진다.
/// </summary>
public sealed class NoticeGoldBanner : IBossClearNotice
{
    private const int KeyStamp = 5201;
    private const int KeyTab = 5202;
    private const float BandWidth = 1300f;
    private const float BandHeight = 150f;
    private const float TopFaceRatio = 0.48f;       // 밝은 윗면이 차지하는 높이 비율
    private const float GlossHeight = 5f;
    private const float GlossInset = 13f;
    private const float EdgeHeight = 4f;
    private const float ShadowOffset = 12f;
    private const float GlintWidth = 90f;
    private const float GlintTilt = 20f;
    private const float StripHeight = 60f;
    private const float StripLine = 4f;             // 띠와 노란 줄 사이 진한 선
    private const float ValueSizePercent = 120f;
    private const float BandOpenSeconds = 0.1f;
    private const float OpenFlashSeconds = 0.07f;
    private const float TitleStart = 0.06f;
    private const float TitleStampSeconds = 0.09f;
    private const float GlintStart = 0.18f;
    private const float GlintSeconds = 0.28f;
    private const float TabStart = 0.24f;
    private const float TabUnfoldSeconds = 0.13f;
    private const float ExitSeconds = 0.16f;
    private const float TitleOutline = 0.32f;
    private const float SubOutline = 0.32f;
    private const float TitleShadowOffset = 6f;

    private BossClearNoticeLab _lab;
    private Image _shadow;
    private RectTransform _bandRoot;
    private CanvasGroup _bandGroup;
    private Image _glint;
    private Image _openFlash;
    private Image _strip;
    private Image _stripLine;
    private float _subBonus = float.NaN;
    private string _subLine;
    private TextMeshProUGUI _titleShadow;
    private TextMeshProUGUI _title;
    private TextMeshProUGUI _sub;
    private Color[] _stampColors;

    /// <inheritdoc />
    public string Name => "B 골드 배너";

    /// <inheritdoc />
    public void Setup(BossClearNoticeLab lab)
    {
        _lab = lab;
        var s = lab.Settings;

        _shadow = lab.CreateImage("GoldBanner_Shadow", new Color(0f, 0f, 0f, 0.35f));
        _shadow.rectTransform.sizeDelta = new Vector2(BandWidth, BandHeight);

        // 둘째 줄: 띠와 같은 폭의 밝은 노란 줄 (윗변을 띠 아랫변에 붙여 아래로 펼친다)
        _strip = NewStripPart("GoldBanner_Strip", s.BannerStrip, StripHeight);
        _stripLine = NewStripPart("GoldBanner_StripLine", new Color(s.Ink.r, s.Ink.g, s.Ink.b, 0.55f), StripLine);

        // 띠 묶음: 층을 쌓고 RectMask2D로 빛 스윕이 띠 밖으로 새지 않게.
        _bandRoot = TimerBonusLab.NewRect("GoldBanner_Band", (RectTransform)_strip.rectTransform.parent, new Vector2(BandWidth, BandHeight));
        _bandRoot.gameObject.AddComponent<RectMask2D>();
        _bandGroup = _bandRoot.gameObject.AddComponent<CanvasGroup>();
        _bandGroup.blocksRaycasts = false;
        Fill(lab.CreateImage("Base", s.OrangeBand, null, _bandRoot).rectTransform);
        var top = lab.CreateImage("TopFace", new Color(s.LightBand.r, s.LightBand.g, s.LightBand.b, 0.6f), null, _bandRoot).rectTransform;
        AnchorTop(top, BandHeight * TopFaceRatio, 0f);
        var gloss = lab.CreateImage("Gloss", new Color(1f, 1f, 1f, 0.45f), null, _bandRoot).rectTransform;
        AnchorTop(gloss, GlossHeight, GlossInset);
        var edgeColor = new Color(s.Ink.r, s.Ink.g, s.Ink.b, 0.6f);
        AnchorTop(lab.CreateImage("EdgeTop", edgeColor, null, _bandRoot).rectTransform, EdgeHeight, 0f);
        var edgeBottom = lab.CreateImage("EdgeBottom", edgeColor, null, _bandRoot).rectTransform;
        edgeBottom.anchorMin = new Vector2(0f, 0f);
        edgeBottom.anchorMax = new Vector2(1f, 0f);
        edgeBottom.pivot = new Vector2(0.5f, 0f);
        edgeBottom.sizeDelta = new Vector2(0f, EdgeHeight);
        edgeBottom.anchoredPosition = Vector2.zero;
        _glint = lab.CreateImage("Glint", new Color(1f, 1f, 1f, 0.55f), null, _bandRoot);
        _glint.rectTransform.sizeDelta = new Vector2(GlintWidth, BandHeight * 2f);
        _glint.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -GlintTilt);
        _openFlash = lab.CreateImage("OpenFlash", Color.white, null, _bandRoot);
        Fill(_openFlash.rectTransform);

        _titleShadow = lab.CreateText("GoldBanner_TitleShadow", 80f, new Color(0f, 0f, 0f, 0.35f));
        _title = lab.CreateText("GoldBanner_Title", 80f, s.BannerText);
        _title.outlineWidth = TitleOutline;
        _title.outlineColor = s.BannerOutline;
        _sub = lab.CreateText("GoldBanner_Sub", 34f, s.BannerText);
        _sub.outlineWidth = SubOutline;
        _sub.outlineColor = s.BannerOutline;
        _sub.richText = true;
        _stampColors = new[] { s.Gold, Color.white, s.LightBand };
        SetVisible(false);
    }

    /// <inheritdoc />
    public void SetVisible(bool visible)
    {
        _shadow.gameObject.SetActive(visible);
        _strip.gameObject.SetActive(visible);
        _stripLine.gameObject.SetActive(visible);
        _bandRoot.gameObject.SetActive(visible);
        _titleShadow.gameObject.SetActive(visible);
        _title.gameObject.SetActive(visible);
        _sub.gameObject.SetActive(visible);
    }

    /// <inheritdoc />
    public void Render(float n, float exitAt, float exitScale)
    {
        float exitDur = ExitSeconds * exitScale;
        if (n < 0f || n >= exitAt + exitDur) { HideAll(); return; }
        float k = n >= exitAt ? Mathf.Clamp01((n - exitAt) / exitDur) : 0f;
        float tabFold = Mathf.Clamp01(k * 2f);
        TimerBonusLab.SetText(_title, _lab.Title);
        TimerBonusLab.SetText(_titleShadow, _lab.Title);

        // 띠: 좌우로 튀어나옴(OutBack) + 세로는 눌렸다 펴지는 스프링 → 사라질 땐 한 줄로 납작
        float sx = OutBack(Mathf.Clamp01(n / BandOpenSeconds), 2.4f) * (1f + 0.08f * k);
        float sy = (1f - 0.3f * Spring(n, 24f, 8f)) * (1f - 0.92f * InCubic(k));
        float bandAlpha = 1f - InQuad(k);
        _bandRoot.anchoredPosition = Vector2.zero;
        _bandRoot.localScale = new Vector3(sx, sy, 1f);
        _bandGroup.alpha = bandAlpha;
        TimerBonusLab.Place(_shadow, new Vector2(0f, -ShadowOffset * sy), new Vector2(sx, sy), bandAlpha);

        // 다 열리는 순간 흰 번쩍임, 도장 뒤 빛 한 줄이 왼쪽→오른쪽으로 스윽
        float flash = 1f - Mathf.Clamp01(Mathf.Abs(n - BandOpenSeconds) / OpenFlashSeconds);
        _openFlash.canvasRenderer.SetAlpha(0.7f * flash);
        float g = Mathf.Clamp01((n - GlintStart) / GlintSeconds);
        float glintX = Mathf.Lerp(-BandWidth * 0.5f - GlintWidth, BandWidth * 0.5f + GlintWidth, OutCubic(g));
        _glint.rectTransform.anchoredPosition = new Vector2(glintX, 0f);
        _glint.canvasRenderer.SetAlpha(g > 0f && g < 1f ? 1f : 0f);

        // 제목: 크게 → 쾅 (도장) → 가로·세로가 엇갈리며 출렁, 아래 그림자로 띄움
        if (n >= TitleStart)
        {
            float q = Mathf.Clamp01((n - TitleStart) / TitleStampSeconds);
            float sc = Mathf.Lerp(1.9f, 1f, OutCubic(q));
            float tx = sc, ty = sc;
            float landed = TitleStart + TitleStampSeconds;
            if (n >= landed)
            {
                float d = Spring(n - landed, 26f, 8f);
                tx = 1f + 0.15f * d;
                ty = 1f - 0.15f * d;
                if (_lab.Once(KeyStamp))
                {
                    _lab.Burst(new Vector2(-BandWidth * 0.3f, 0f), 8, 300f, 700f, _stampColors, life0: 0.18f, life1: 0.3f, angle: Mathf.PI, spread: 0.6f);
                    _lab.Burst(new Vector2(BandWidth * 0.3f, 0f), 8, 300f, 700f, _stampColors, life0: 0.18f, life1: 0.3f, angle: 0f, spread: 0.6f);
                    _lab.TimerLab.Zoom = Mathf.Max(_lab.TimerLab.Zoom, 0.025f);
                }
            }
            ty *= 1f - InCubic(k);
            float alpha = Mathf.Clamp01(q * 3f) * (1f - k);
            var size = new Vector2(tx, ty);
            TimerBonusLab.Place(_titleShadow, new Vector2(0f, 4f * sy - TitleShadowOffset), size, alpha);
            TimerBonusLab.Place(_title, new Vector2(0f, 4f * sy), size, alpha);
        }
        else
        {
            TimerBonusLab.Hide(_titleShadow);
            TimerBonusLab.Hide(_title);
        }

        // 둘째 줄: 띠 바로 아래로 노란 줄이 툭 펼쳐짐 (지나쳤다 돌아옴)
        float bandBottom = -BandHeight * 0.5f * sy;
        if (n >= TabStart) RenderStrip(n, bandBottom, sx, bandAlpha, tabFold);
        else HideStrip();
    }

    private void RenderStrip(float n, float bandBottom, float sx, float bandAlpha, float fold)
    {
        var s = _lab.Settings;
        float bonus = _lab.TimerLab.Bonus;
        if (bonus != _subBonus)
        {
            _subBonus = bonus;
            _subLine = $"{s.BannerSubLabel} <size={ValueSizePercent}%>{string.Format(s.BannerSubValue, Mathf.RoundToInt(bonus))}</size>";
        }
        TimerBonusLab.SetText(_sub, _subLine);

        float q = Mathf.Clamp01((n - TabStart) / TabUnfoldSeconds);
        float unfold = Mathf.Max(0f, OutBack(q, 3f) * (1f - fold));
        var scale = new Vector2(sx, unfold);
        TimerBonusLab.Place(_strip, new Vector2(0f, bandBottom), scale, bandAlpha);
        TimerBonusLab.Place(_stripLine, new Vector2(0f, bandBottom), new Vector2(sx, unfold > 0.01f ? 1f : 0f), bandAlpha);

        float subY = bandBottom - StripHeight * 0.5f * unfold;
        float landed = TabStart + TabUnfoldSeconds;
        float punch = n >= landed ? 0.12f * Spring(n - landed, 26f, 9f) : 0f;
        TimerBonusLab.Place(_sub, new Vector2(0f, subY), new Vector2(1f + punch, (1f + punch) * unfold), Mathf.Clamp01(q * 2f) * (1f - fold));
        if (q >= 1f && _lab.Once(KeyTab))
        {
            _lab.Burst(new Vector2(-BandWidth * 0.32f, subY), 5, 160f, 340f, _stampColors, life0: 0.14f, life1: 0.24f, angle: Mathf.PI, spread: 0.5f);
            _lab.Burst(new Vector2(BandWidth * 0.32f, subY), 5, 160f, 340f, _stampColors, life0: 0.14f, life1: 0.24f, angle: 0f, spread: 0.5f);
        }
    }

    // 윗변 기준(pivot 위)으로 아래로 펼쳐지는 가로 줄
    private Image NewStripPart(string name, Color color, float height)
    {
        var img = _lab.CreateImage(name, color);
        img.rectTransform.pivot = new Vector2(0.5f, 1f);
        img.rectTransform.sizeDelta = new Vector2(BandWidth, height);
        return img;
    }

    private void HideStrip()
    {
        TimerBonusLab.Hide(_strip);
        TimerBonusLab.Hide(_stripLine);
        TimerBonusLab.Hide(_sub);
    }

    private static void Fill(RectTransform rt) => TimerBonusLab.Stretch(rt);

    // 띠 윗변에 붙인 가로 줄 (inset = 윗변에서 내려온 거리)
    private static void AnchorTop(RectTransform rt, float height, float inset)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, height);
        rt.anchoredPosition = new Vector2(0f, -inset);
    }

    private void HideAll()
    {
        TimerBonusLab.Hide(_shadow);
        _bandGroup.alpha = 0f;
        TimerBonusLab.Hide(_titleShadow);
        TimerBonusLab.Hide(_title);
        HideStrip();
    }
}
