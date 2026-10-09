using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>ZIP E 사선 스트립: 25눈금·홀수 반칸·READY. ZIP의 @1x 좌표를 HP바 아래에 배치한다.</summary>
public sealed class HackGaugeSlanted : HackStackGauge
{
    /// <summary>ZIP 스펙의 최대 스택.</summary>
    public const int Capacity = 50;
    /// <summary>2스택마다 하나인 눈금 개수.</summary>
    public const int TickCount = Capacity / 2;
    private const float ReferenceWidth = 407f;
    private const float StripWidth = 314f;
    private const float StripHeight = 18f;
    private const float LabelTop = 20f;
    private const float LabelHeight = 12f;
    private const float HpGap = 4f;
    private const float ArtScale = 3f;
    private const float TickStart = 70f;
    private const float TickPitch = 9.44f;
    private const float TickHalfWidth = 3.72f;
    private const float PopDuration = .12f;
    private const float PopScale = .3f;
    private const float PulsePeriod = .6f;
    private readonly HackSlantedGaugeStyle _style;
    private readonly Image _tag;
    private readonly Image[] _ticks = new Image[TickCount];
    private readonly float[] _popAt = new float[TickCount];
    private readonly TextMeshProUGUI _label;
    private readonly CanvasGroup _group;
    private int _previous = -1;

    /// <inheritdoc/>
    public override string Name => "ZIP E 사선";

    /// <summary>압축에 제공된 원본 PNG 부품을 사용해 스트립을 만든다.</summary>
    public HackGaugeSlanted(RectTransform parent, HackSlantedGaugeStyle style)
        : base(parent, style != null ? style.Font : null, style != null ? style.FontMaterial : null,
            new Vector2(StripWidth, LabelTop + LabelHeight), new Vector2(1f, 1f))
    {
        if (style == null || !style.IsComplete) throw new ArgumentException("Hack gauge E art/font references are incomplete.", nameof(style));
        _style = style;
        _group = Root.GetComponent<CanvasGroup>();
        Box("Background", Root, new Vector2(0f, 1f), Vector2.zero, new Vector2(StripWidth, StripHeight), Color.white, style.Background);
        _tag = Box("Tag", Root, new Vector2(0f, 1f), Vector2.zero, new Vector2(64f, StripHeight), Color.white, style.HackTag);
        for (int i = 0; i < TickCount; i++)
        {
            // PNG 발광·기울기 여백을 자르지 않고 중심 기준으로 배치한다.
            _ticks[i] = Box("Tick_" + i.ToString("00"), Root, new Vector2(0f, 1f),
                new Vector2(TickStart + i * TickPitch + TickHalfWidth, -9f), style.TickOff.rect.size / ArtScale,
                Color.white, style.TickOff);
            _ticks[i].rectTransform.pivot = new Vector2(.5f, .5f);
            _popAt[i] = float.NegativeInfinity;
        }
        _label = Text("Count", Root, new Vector2(1f, 1f), new Vector2(-3f, -LabelTop),
            new Vector2(100f, LabelHeight), 12f, TextAlignmentOptions.Right);
        _label.fontStyle = FontStyles.Normal;
        _label.characterSpacing = .5f / 12f * 100f;
        _label.margin = new Vector4(-2f, -2f, -2f, -2f);
        _label.overflowMode = TextOverflowModes.Overflow;
    }

    /// <summary>실제 스택으로 즉시 갱신한다. 0도 표시하며 공용 시안의 전체 확대·색 단계는 적용하지 않는다.</summary>
    public override void Tick(float now, Vector2 bossLocal)
    {
        float scale = UseHud ? Parent.rect.width / ReferenceWidth : 1f;
        Root.localScale = Vector3.one * scale;
        Root.anchoredPosition = UseHud ? new Vector2(HudBar.xMax, HudBar.yMin - HpGap * scale) : bossLocal;
        int stack = Mathf.Clamp(Target, 0, Capacity);
        bool ready = stack == Capacity;
        _group.alpha = ready ? .85f + .15f * Mathf.Cos(now * (2f * Mathf.PI / PulsePeriod)) : 1f;
        if (stack != _previous)
        {
            for (int i = 0; i < TickCount; i++)
            {
                int state = Mathf.Clamp(stack - i * 2, 0, 2);
                int previousState = Mathf.Clamp(_previous - i * 2, 0, 2);
                _ticks[i].sprite = state == 2 ? _style.TickOn : state == 1 ? _style.TickHalf : _style.TickOff;
                _ticks[i].rectTransform.sizeDelta = _ticks[i].sprite.rect.size / ArtScale;
                if (_previous >= 0 && state > previousState) _popAt[i] = now;
                else if (state < previousState) _popAt[i] = float.NegativeInfinity;
            }
            _tag.sprite = ready ? _style.ReadyTag : _style.HackTag;
            _label.text = stack + "<color=#C9CDF0>/50</color>";
            _previous = stack;
        }
        for (int i = 0; i < TickCount; i++)
        {
            float remaining = 1f - Mathf.Clamp01((now - _popAt[i]) / PopDuration);
            _ticks[i].rectTransform.localScale = Vector3.one * (1f + PopScale * remaining * remaining);
        }
    }

    /// <inheritdoc/>
    protected override void Render(float now, int shown, float ratio, float bump, float ignite) { }
}
