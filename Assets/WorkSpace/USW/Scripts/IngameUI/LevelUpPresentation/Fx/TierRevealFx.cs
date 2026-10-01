using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레벨업 요구사항 ② "화면 전환 후 등급별 이펙트" — 레어/에픽/레전더리 3종, 색·강도 차등.
/// 레퍼런스: design/연출예시1.mp4 (빛 번짐 언어, 사용자 선택 2026-09-29).
/// 흐름: 카드 뒤를 등급색으로 어둡게 깔고 → 빛줄기가 중심(카드 뒤)으로 빨려 모임 → 섬광 + (빛 구체) + 광선·보케
///       → 광선·후광은 잠깐(_glowLinger) 남았다가 꺼지고, 대신 등급색 빛 알갱이(_motes)가 패널 아래에서 반짝이며 떠올라
///         패널 중간쯤에서 녹아 사라진다 (사용자 요청 2026-09-30, 레퍼런스 design/연출예시1.mp4 0~1초·design/수정제안1.png).
/// 레전더리는 빨강으로 시작해 빛기둥이 고르게 차오른 뒤 한 번 더 터진다 (레퍼런스의 등급 상승 장면 차용).
/// 보라(에픽)에서 빨강으로 바뀌는 반전은 레전더리 10% 확률로만 나온다 (UpgradeChance, 사용자 결정 2026-09-29) —
/// 평소에는 처음부터 등급색으로 통일감, 가끔 "에픽인 줄 알았는데 레전더리" 반전.
/// 등급색: 레어 파랑 / 에픽 보라 / 레전더리 빨강 (사용자 지정).
/// 레벨업 패널 안, 카드 컨테이너 뒤에 둔다. LevelUpRevealSequence가 Play(등급)을 부르고, 반환값만큼 카드 등장을 늦춘다.
/// 모든 트윈은 기본 unscaled (레벨업 일시정지 중 재생). 패널이 닫혀 비활성화되면 트윈을 멈춘다.
/// </summary>
public class TierRevealFx : MonoBehaviour, IFxLabPlayable
{
    /// <summary>등급 하나의 색과 강도.</summary>
    [Serializable]
    public class TierStyle
    {
        [Tooltip("광선·후광·빛 구체·보케 색")] public Color Main = Color.white;
        [Tooltip("모이는 빛줄기·섬광 색 (밝은 쪽)")] public Color Accent = Color.white;
        [Tooltip("카드 뒤 배경 색")] public Color Backdrop = Color.black;
        [Range(0f, 1f)] public float BackdropAlpha = 0.9f;
        [Range(0f, 1f)] public float GlowPeak = 0.8f;
        [Range(0f, 1f)] public float GlowRest = 0.4f;
        [Range(0f, 1f)] public float RayPeak = 0.9f;
        [Range(0f, 1f)] public float RayRest = 0.45f;
        [Tooltip("섬광 크기 배율")] public float FlashScale = 1f;
        [Tooltip("빛 구체 충격파 사용")] public bool Bubble = true;
        [Tooltip("보케 원 개수 (요소 수 이내)")] public int Bokeh = 3;
        [Tooltip("모이는 빛줄기 초당 방출 수")] public float GatherRate = 90f;
        [Tooltip("빛기둥이 차오른 뒤 한 번 더 터지는 2단 연출 (레전더리)")] public bool Columns;
        [Tooltip("에픽 모습으로 시작해 이 등급 색으로 바뀌는 반전을 항상 재생 (Columns와 함께). 기본 끔 — 확률은 UpgradeChance")] public bool UpgradeFromEpic;
        [Tooltip("반전(에픽 → 이 등급)이 나올 확률 0~1. 레전더리 10% (사용자 결정 2026-09-29) — 나머지는 처음부터 등급색")]
        [Range(0f, 1f)] public float UpgradeChance;
        [Tooltip("마지막 섬광 후 카드가 날아오기 시작할 때까지(초)")] public float CardsDelayAfterBurst = 0.1f;
    }

    [Header("요소 (빌더가 연결)")]
    [SerializeField] private Image _backdrop;
    [SerializeField] private Image _columns;
    [SerializeField] private Image _backGlow;
    [SerializeField] private Image _rays;
    [SerializeField] private Image _bubble;
    [SerializeField] private Image[] _bokeh = Array.Empty<Image>();
    [SerializeField] private UiFxParticleEmitter _gather;
    [SerializeField] private Image _core;
    [Tooltip("글로우가 꺼진 뒤 선택 내내 은은하게 반짝이며 떠오르는 등급색 빛 알갱이 (비우면 생략)")]
    [SerializeField] private UiFxParticleEmitter _motes;
    [Tooltip("재생 중 원래 배경의 색만 투명하게 한다 (Backdrop이 대신한다). 필드 보기 터치 영역은 유지하고 비활성화 시 색을 복원한다.")]
    [SerializeField] private Graphic _replacedBackground;

    [Header("등급")]
    [SerializeField] private TierStyle _rare = new TierStyle();
    [SerializeField] private TierStyle _epic = new TierStyle();
    [SerializeField] private TierStyle _legend = new TierStyle();

    [Header("타이밍 (초)")]
    [Tooltip("빛줄기가 모이는 시간 — 끝나는 순간 첫 섬광")]
    [SerializeField] private float _gatherDuration = 0.3f;
    [Tooltip("레전더리: 첫 섬광 후 빛기둥이 차오르기 시작할 때까지")]
    [SerializeField] private float _upgradeDelay = 0.2f;
    [Tooltip("레전더리: 빛기둥이 차오르는 시간 (이 동안 색이 빨강으로 바뀜)")]
    [SerializeField] private float _upgradeDuration = 0.35f;
    [Tooltip("레전더리: 두 번째 섬광 크기 배율")]
    [SerializeField] private float _upgradeBurstScale = 1.3f;
    [Tooltip("마지막 섬광 후 광선·후광을 보여 주는 시간 — 이후 꺼진다")]
    [SerializeField] private float _glowLinger = 1f;
    [Tooltip("광선·후광이 꺼지는 시간")]
    [SerializeField] private float _glowFadeOut = 0.7f;
    [Tooltip("꺼진 뒤 남길 밝기 (등급 Rest 대비 비율, 0 = 완전히 끔)")]
    [Range(0f, 1f)] [SerializeField] private float _glowEndRatio;

    [Header("빛 알갱이")]
    [Tooltip("마지막 섬광 후 빛 알갱이가 나타나기 시작할 때까지")]
    [SerializeField] private float _motesDelay = 0.6f;
    [Tooltip("빛 알갱이가 서서히 나타나는 시간")]
    [SerializeField] private float _motesFadeIn = 1f;
    [Tooltip("알갱이 색을 흰색 쪽으로 섞는 비율 (0 = 등급색 그대로)")]
    [Range(0f, 1f)] [SerializeField] private float _motesWhiten = 0.2f;

    [Header("기타")]
    [SerializeField] private bool _useUnscaledTime = true;
    [Tooltip("실험실 캡처(Play())용 등급")]
    [SerializeField] private Tier _labTier = Tier.Legend;

    private Sequence _sequence;
    private Vector2[] _bokehBase;
    private Graphic _hiddenBackground;
    private Color _backgroundColor;

    /// <summary>timeScale 무시 여부. 다음 Play부터 반영.</summary>
    public bool UseUnscaledTime { get => _useUnscaledTime; set => _useUnscaledTime = value; }

    /// <summary>실험실/디버그: null이 아니면 반전 여부를 확률 대신 이 값으로 정한다.</summary>
    public bool? DebugForceUpgrade { get; set; }

    /// <summary>마지막 Play에서 반전(에픽 → 등급색)이 나왔는지.</summary>
    public bool LastPlayUpgraded { get; private set; }

    private void Awake()
    {
        CacheBokeh();
        HideAll();
    }

    private void CacheBokeh()
    {
        if (_bokehBase != null) return;
        _bokehBase = new Vector2[_bokeh.Length];
        for (int i = 0; i < _bokeh.Length; i++) if (_bokeh[i] != null) _bokehBase[i] = _bokeh[i].rectTransform.anchoredPosition;
    }

    private void OnDisable()
    {
        _sequence?.Kill();
        _sequence = null;
        if (_gather != null) _gather.Stop(clear: true);
        if (_motes != null) { _motes.DOKill(); _motes.Stop(clear: true); }
        RestoreBackground();
    }

    // The background also receives hold-to-peek input. Disabling its Graphic removes
    // the raycast surface, so replace only its appearance, not its input component.
    private void HideBackground()
    {
        if (_hiddenBackground == _replacedBackground) return;
        RestoreBackground();
        if (_replacedBackground == null) return;
        _hiddenBackground = _replacedBackground;
        _backgroundColor = _hiddenBackground.color;
        _hiddenBackground.color = WithAlpha(_backgroundColor, 0f);
    }

    private void RestoreBackground()
    {
        if (_hiddenBackground != null) _hiddenBackground.color = _backgroundColor;
        _hiddenBackground = null;
    }

    /// <summary>실험실용: _labTier로 재생.</summary>
    [ContextMenu("Play")]
    public void Play() => Play(_labTier);

    /// <summary>
    /// 등급 연출을 처음부터 재생하고, 카드가 날아오기 시작해야 할 시각(이 호출 기준 초)을 반환한다.
    /// 레어 미만은 레어로, 레전더리 이상은 레전더리로 취급한다.
    /// </summary>
    public float Play(Tier tier)
    {
        CacheBokeh();
        var style = StyleOf(tier);
        // 반전은 빛기둥 2단 연출이 있는 등급에서만, 항상(UpgradeFromEpic) 또는 확률(UpgradeChance)로
        bool upgrade = style.Columns && (DebugForceUpgrade ?? (style.UpgradeFromEpic || UnityEngine.Random.value < style.UpgradeChance));
        LastPlayUpgraded = upgrade;
        var first = upgrade ? _epic : style;

        _sequence?.Kill();
        KillTweens();
        HideAll();
        _sequence = DOTween.Sequence().SetUpdate(_useUnscaledTime).SetLink(gameObject);

        // 배경: 카드 뒤를 등급색으로 어둡게 (패널 페이드와 함께 나타남)
        if (_backdrop != null)
        {
            _backdrop.gameObject.SetActive(true);
            _backdrop.color = WithAlpha(first.Backdrop, first.BackdropAlpha);
            HideBackground();
        }

        // 모임: 후광이 서서히 차오르고 빛줄기가 중심으로 빨려 든다
        if (_backGlow != null)
        {
            Show(_backGlow, first.Main, 0.5f);
            _sequence.Insert(0f, _backGlow.DOFade(first.GlowPeak * 0.5f, _gatherDuration).SetEase(Ease.InQuad));
            _sequence.Insert(0f, _backGlow.rectTransform.DOScale(1f, _gatherDuration).SetEase(Ease.OutQuad));
        }
        if (_gather != null)
        {
            _gather.UseUnscaledTime = _useUnscaledTime;
            _gather.color = first.Accent;
            _gather.Rate = style.GatherRate;
            _gather.Stop(clear: true);
            _gather.Play();
            _sequence.InsertCallback(_gatherDuration, () => _gather.Stop());
        }

        float t = _gatherDuration;
        Burst(t, first, 1f);
        if (!style.Columns)
        {
            Settle(t, style);
            return t + style.CardsDelayAfterBurst;
        }

        // 레전더리 2단: 빛기둥이 아래에서 차오르며 (UpgradeFromEpic이면 색도 이 등급색으로 바뀌며) → 두 번째 섬광
        float u = t + _upgradeDelay;
        if (_columns != null)
        {
            var crt = _columns.rectTransform;
            _columns.color = WithAlpha(style.Main, 0f);
            crt.localScale = new Vector3(1f, 0f, 1f);
            _sequence.InsertCallback(u, () => _columns.gameObject.SetActive(true));
            _sequence.Insert(u, _columns.DOFade(0.9f, 0.12f));
            _sequence.Insert(u, crt.DOScaleY(1f, _upgradeDuration).SetEase(Ease.OutCubic));
        }
        if (_backdrop != null) _sequence.Insert(u, TweenRgb(_backdrop, style.Backdrop, _upgradeDuration));
        if (_backGlow != null) _sequence.Insert(u, TweenRgb(_backGlow, style.Main, _upgradeDuration));
        if (_rays != null) _sequence.Insert(u, TweenRgb(_rays, style.Main, _upgradeDuration));

        float b = u + _upgradeDuration;
        Burst(b, style, _upgradeBurstScale);
        if (_columns != null)
        {
            _sequence.Insert(b + 0.05f, _columns.DOFade(0f, 0.4f).SetEase(Ease.InQuad));
            _sequence.InsertCallback(b + 0.45f, () => _columns.gameObject.SetActive(false));
        }
        Settle(b, style);
        return b + style.CardsDelayAfterBurst;
    }

    /// <summary>
    /// 마지막 섬광(t) 후: 광선·후광을 _glowLinger 동안 보여 준 뒤 끄고, 빛 알갱이를 서서히 켠다.
    /// 알갱이는 패널이 닫힐 때(OnDisable)까지 계속 나온다.
    /// </summary>
    private void Settle(float t, TierStyle style)
    {
        float off = t + _glowLinger;
        if (_rays != null)
        {
            _sequence.Insert(off, _rays.DOFade(style.RayRest * _glowEndRatio, _glowFadeOut).SetEase(Ease.InOutSine));
            if (_glowEndRatio <= 0f) _sequence.InsertCallback(off + _glowFadeOut, () => _rays.gameObject.SetActive(false));
        }
        if (_backGlow != null)
        {
            _sequence.Insert(off, _backGlow.DOFade(style.GlowRest * _glowEndRatio, _glowFadeOut).SetEase(Ease.InOutSine));
            if (_glowEndRatio <= 0f) _sequence.InsertCallback(off + _glowFadeOut, () => _backGlow.gameObject.SetActive(false));
        }
        if (_motes != null)
        {
            var tint = Color.Lerp(style.Main, Color.white, _motesWhiten);
            _sequence.InsertCallback(t + _motesDelay, () =>
            {
                _motes.UseUnscaledTime = _useUnscaledTime;
                _motes.color = WithAlpha(tint, 0f);
                _motes.Stop(clear: true);
                _motes.Play();
            });
            _sequence.Insert(t + _motesDelay, _motes.DOFade(1f, _motesFadeIn).SetEase(Ease.InOutSine));
        }
    }

    /// <summary>섬광 + (빛 구체) + 광선·후광 상승 후 잔류 + 보케.</summary>
    private void Burst(float t, TierStyle style, float scale)
    {
        if (_core != null)
        {
            var rt = _core.rectTransform;
            _sequence.InsertCallback(t, () =>
            {
                _core.gameObject.SetActive(true);
                _core.color = WithAlpha(style.Accent, 0f);
                rt.localScale = Vector3.one * 0.4f * scale;
            });
            _sequence.Insert(t, _core.DOFade(1f, 0.03f));
            _sequence.Insert(t, rt.DOScale(1.2f * scale * style.FlashScale, 0.12f).SetEase(Ease.OutQuad));
            _sequence.Insert(t + 0.05f, _core.DOFade(0f, 0.25f).SetEase(Ease.OutQuad));
        }
        if (_bubble != null && style.Bubble)
        {
            var rt = _bubble.rectTransform;
            _sequence.InsertCallback(t, () =>
            {
                _bubble.gameObject.SetActive(true);
                _bubble.color = WithAlpha(style.Main, 1f);
                rt.localScale = Vector3.one * 0.1f;
            });
            _sequence.Insert(t, rt.DOScale(scale, 0.4f).SetEase(Ease.OutQuad));
            _sequence.Insert(t + 0.02f, _bubble.DOFade(0f, 0.3f).SetEase(Ease.OutCubic));
        }
        if (_rays != null)
        {
            var rt = _rays.rectTransform;
            _sequence.InsertCallback(t, () =>
            {
                if (!_rays.gameObject.activeSelf) { _rays.gameObject.SetActive(true); _rays.color = WithAlpha(style.Main, 0f); rt.localScale = Vector3.one * 0.5f; }
            });
            _sequence.Insert(t, _rays.DOFade(style.RayPeak, 0.12f));
            _sequence.Insert(t, rt.DOScale(1f, 0.35f).SetEase(Ease.OutCubic));
            _sequence.Insert(t + 0.12f, _rays.DOFade(style.RayRest, 0.4f).SetEase(Ease.OutQuad));
        }
        if (_backGlow != null)
        {
            _sequence.Insert(t, _backGlow.DOFade(style.GlowPeak, 0.1f));
            _sequence.Insert(t + 0.1f, _backGlow.DOFade(style.GlowRest, 0.5f).SetEase(Ease.OutQuad));
        }
        int count = Mathf.Min(style.Bokeh, _bokeh.Length);
        for (int i = 0; i < count; i++)
        {
            var img = _bokeh[i];
            if (img == null) continue;
            var rt = img.rectTransform;
            Vector2 basePos = _bokehBase[i];
            float s = t + 0.05f + i * 0.03f;
            _sequence.InsertCallback(s, () =>
            {
                img.gameObject.SetActive(true);
                img.color = WithAlpha(style.Main, 0f);
                rt.anchoredPosition = basePos;
                rt.localScale = Vector3.one * 0.6f;
            });
            _sequence.Insert(s, img.DOFade(0.55f, 0.08f));
            _sequence.Insert(s, rt.DOScale(1f, 0.3f).SetEase(Ease.OutQuad));
            _sequence.Insert(s, rt.DOAnchorPos(basePos * 1.3f, 0.45f).SetEase(Ease.OutSine));
            _sequence.Insert(s + 0.12f, img.DOFade(0f, 0.3f).SetEase(Ease.InQuad));
        }
    }

    private TierStyle StyleOf(Tier tier)
    {
        if (tier >= Tier.Legend) return _legend;
        if (tier == Tier.Epic) return _epic;
        return _rare;
    }

    private void HideAll()
    {
        foreach (var g in new Graphic[] { _backdrop, _columns, _backGlow, _rays, _bubble, _core })
            if (g != null) g.gameObject.SetActive(false);
        foreach (var b in _bokeh) if (b != null) b.gameObject.SetActive(false);
        if (_gather != null) _gather.Stop(clear: true);
        if (_motes != null) _motes.Stop(clear: true);
    }

    private void KillTweens()
    {
        foreach (var g in new Graphic[] { _backdrop, _columns, _backGlow, _rays, _bubble, _core })
            if (g != null) { g.DOKill(); g.rectTransform.DOKill(); }
        foreach (var b in _bokeh) if (b != null) { b.DOKill(); b.rectTransform.DOKill(); }
        if (_motes != null) _motes.DOKill();
    }

    private static void Show(Graphic g, Color rgb, float scale)
    {
        g.gameObject.SetActive(true);
        g.color = WithAlpha(rgb, 0f);
        g.rectTransform.localScale = Vector3.one * scale;
    }

    // 알파는 그대로 두고 RGB만 바꾸는 트윈 (알파 페이드 트윈과 동시에 돌 수 있다)
    private static Tween TweenRgb(Graphic g, Color target, float duration)
    {
        return DOTween.To(() => g.color, c => g.color = new Color(c.r, c.g, c.b, g.color.a), WithAlpha(target, 1f), duration)
                      .SetEase(Ease.InOutSine);
    }

    private static Color WithAlpha(Color c, float a) { c.a = a; return c; }
}
