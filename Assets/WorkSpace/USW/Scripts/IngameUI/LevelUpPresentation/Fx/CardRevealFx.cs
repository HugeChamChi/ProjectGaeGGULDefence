using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레벨업 카드 이펙트 (공개·선택 공용).
///   ③ 카드 출력: 등급색 테두리 번쩍임 + 바깥 번짐 → 흰 빛이 카드를 사선으로 훑음 → (레전더리) 가로 플레어.
///   ④ 카드 선택: 위 구성 + 카드 전체 흰 섬광 + 카드 모양 테두리가 바깥으로 퍼지는 파동 (요소를 넣은 프리팹만).
/// 레퍼런스: design/연출예시4.mp4 (등급 테두리 번쩍임), design/연출예시3.mp4 (빛 쓸기·선택 강조), 빛 번짐 언어는 연출예시1.
/// 빛은 카드 테두리 스프라이트의 실루엣을 따른다 (USW/UI/FxSpriteGlow — 카드 그림의 투명 여백·비스듬한 끝까지 맞음).
/// 사방으로 튀는 불씨는 쓰지 않는다 (사용자 결정 2026-09-29).
/// LevelUpRevealSequence 슬롯 B / LevelUpSelectSequence 선택 슬롯 프리팹으로 생성되며, 시퀀스가 PlayOnCard(카드)를 호출한다.
/// 카드 크기를 인스턴스 머티리얼에 넣으므로 머티리얼은 Awake에서 복제하고 파괴 시 정리한다.
/// </summary>
public class CardRevealFx : MonoBehaviour
{
    private static readonly int ExpandId = Shader.PropertyToID("_Expand");
    private static readonly int RadiusUvId = Shader.PropertyToID("_RadiusUV");
    private static readonly int SweepPosId = Shader.PropertyToID("_SweepPos");
    private static readonly int BorderIntensityId = Shader.PropertyToID("_BorderIntensity");
    private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
    private static readonly int QuadSizeId = Shader.PropertyToID("_QuadSize");
    private static readonly int BoxHalfId = Shader.PropertyToID("_BoxHalf");

    /// <summary>등급 하나의 색과 강도.</summary>
    [Serializable]
    public class TierLook
    {
        public Color Color = Color.white;
        [Tooltip("테두리·번짐 밝기 배율")] public float Intensity = 1f;
        [Tooltip("가로 플레어 사용 (레전더리)")] public bool Flare;
    }

    [Header("요소 (빌더가 연결, 비우면 생략)")]
    [SerializeField] private Image _glow;
    [SerializeField] private Image _sweep;
    [SerializeField] private Image _flare;
    [Tooltip("선택용: 카드 전체 흰 섬광")]
    [SerializeField] private Image _flash;
    [Tooltip("선택용: 카드 모양 테두리가 바깥으로 퍼지는 파동")]
    [SerializeField] private Image _pulse;
    [Tooltip("선택용: 카드 중심에서 터져 카드 안을 채우며 바깥으로 번지는 등급색 타원 빛 (테두리만 빛나 속이 비어 보이는 것 보완)")]
    [SerializeField] private Image _bloom;

    [Header("등급 (레어 파랑 / 에픽 보라 / 레전더리 빨강)")]
    [SerializeField] private TierLook _rare = new TierLook();
    [SerializeField] private TierLook _epic = new TierLook();
    [SerializeField] private TierLook _legend = new TierLook();

    [Header("모양")]
    [Tooltip("켜면 카드 이미지 영역 사각형(USW/UI/FxRectGlow)으로 빛난다 — 카드 폭을 따라 일직선으로 길게 뻗는 느낌 (③ 공개용, 사용자 선택). " +
             "끄면 카드 테두리 스프라이트 실루엣(USW/UI/FxSpriteGlow)을 따른다 (④ 선택용). 머티리얼 셰이더도 이에 맞춰야 한다.")]
    [SerializeField] private bool _rectShape;
    [Tooltip("바깥 번짐 반지름(px) — 카드 실루엣에서 이만큼 번진다 (실루엣 방식)")]
    [SerializeField] private float _glowRadius = 40f;
    [Tooltip("번짐이 그려질 여백(px, 반지름보다 크게)")]
    [SerializeField] private float _padding = 60f;
    [SerializeField] private float _flareWidth = 1500f;
    [SerializeField] private float _flareHeight = 36f;
    [Tooltip("파동이 퍼지는 최대 배율")]
    [SerializeField] private float _pulseScale = 1.15f;
    [Tooltip("테두리 빛·섬광·파동이 시작하는 배율 — 1보다 작으면 카드 안쪽에서 시작해 바깥으로 퍼진다 (1 = 처음부터 카드 크기). " +
             "④ 선택용: 너무 바깥에서부터 보인다는 피드백으로 안→밖 확산 (사용자 요청 2026-09-30)")]
    [SerializeField] private float _growFrom = 1f;
    [Tooltip("테두리 빛·섬광이 _growFrom에서 카드 크기까지 커지는 시간")]
    [SerializeField] private float _growDuration = 0.15f;
    [Tooltip("중심 빛 최종 크기 (카드 크기 대비 배율, 가로·세로)")]
    [SerializeField] private Vector2 _bloomSize = new Vector2(1.3f, 1.6f);
    [Tooltip("중심 빛 시작 배율")]
    [SerializeField] private float _bloomFrom = 0.1f;
    [Range(0f, 1f)] [SerializeField] private float _bloomPeak = 0.8f;
    [Tooltip("중심 빛 색을 흰색 쪽으로 섞는 비율")]
    [Range(0f, 1f)] [SerializeField] private float _bloomWhiten = 0.3f;
    [SerializeField] private float _bloomGrow = 0.22f;
    [SerializeField] private float _bloomFall = 0.4f;
    [Tooltip("테두리 빛·섬광·파동을 늦게 시작하는 시간 — 중심 빛이 먼저 카드를 채운 뒤 가장자리가 빛나게 (0 = 동시)")]
    [SerializeField] private float _edgeDelay;
    [Tooltip("흰 섬광 최대 밝기")]
    [Range(0f, 1f)] [SerializeField] private float _flashPeak = 0.85f;
    [Tooltip("테두리 바깥 번짐 폭의 시작 비율 — 1보다 작으면 테두리에 붙어 시작해 _glowRadius까지 넓어진다 (실루엣 방식만, 1 = 처음부터 전체 폭)")]
    [Range(0f, 1f)] [SerializeField] private float _radiusFrom = 1f;

    [Header("타이밍 (초)")]
    [SerializeField] private float _glowRise = 0.05f;
    [SerializeField] private float _glowHold = 0.08f;
    [SerializeField] private float _glowFall = 0.45f;
    [SerializeField] private float _sweepDelay = 0.04f;
    [SerializeField] private float _sweepDuration = 0.32f;
    [SerializeField] private float _flareDuration = 0.5f;
    [SerializeField] private float _flashDuration = 0.18f;
    [SerializeField] private float _pulseDuration = 0.35f;
    [SerializeField] private bool _useUnscaledTime = true;

    private Material _glowMat, _sweepMat, _flashMat, _pulseMat;
    private Sequence _sequence;
    private float _baseBorder;
    private float _baseGlow;

    private void Awake()
    {
        _glowMat = Instance(_glow);
        _sweepMat = Instance(_sweep);
        _flashMat = Instance(_flash);
        _pulseMat = Instance(_pulse);
        if (_glowMat != null)
        {
            _baseBorder = _glowMat.GetFloat(BorderIntensityId);
            _baseGlow = _glowMat.GetFloat(GlowIntensityId);
        }
        foreach (var g in new Graphic[] { _glow, _sweep, _flare, _flash, _pulse, _bloom })
            if (g != null) g.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        _sequence?.Kill();
        foreach (var m in new[] { _glowMat, _sweepMat, _flashMat, _pulseMat })
            if (m != null) Destroy(m);
    }

    /// <summary>
    /// 카드 외곽(테두리 이미지)의 중심·크기·스프라이트 실루엣에 맞춰 카드 등급으로 재생한다.
    /// 카드가 오버슈트/눌림 배율이어도 배율 1 기준 크기로 환산한다.
    /// </summary>
    public void PlayOnCard(LevelUpCardUI card)
    {
        if (card == null) return;
        var cardRt = (RectTransform)card.transform;
        var shape = card.RevealShape != null ? card.RevealShape.rectTransform : cardRt;
        var fxRt = (RectTransform)transform;
        fxRt.position = shape.TransformPoint(shape.rect.center);
        float parent = fxRt.parent != null ? fxRt.parent.lossyScale.x : 1f;
        float settled = cardRt.lossyScale.x / Mathf.Max(cardRt.localScale.x, 1e-4f);
        float shapeToCard = shape == cardRt ? 1f : shape.lossyScale.x / Mathf.Max(cardRt.lossyScale.x, 1e-6f);
        Vector2 size = shape.rect.size * shapeToCard * settled / Mathf.Max(parent, 1e-6f);
        var tier = card.GetData() != null ? card.GetData().tier : Tier.Rare;
        Play(tier, size, card.RevealShape != null ? card.RevealShape.sprite : null);
    }

    /// <summary>
    /// 카드 등급·크기(이 오브젝트 부모 기준 캔버스 단위)·테두리 스프라이트로 재생한다.
    /// silhouette가 없으면 실루엣 요소는 건너뛰고 플레어만 재생한다.
    /// </summary>
    public void Play(Tier tier, Vector2 cardSize, Sprite silhouette)
    {
        var look = tier >= Tier.Legend ? _legend : tier == Tier.Epic ? _epic : _rare;
        _sequence?.Kill();
        _sequence = DOTween.Sequence().SetUpdate(_useUnscaledTime).SetLink(gameObject);

        Vector2 quad = cardSize + Vector2.one * (_padding * 2f);
        bool hasShape = _rectShape || silhouette != null;
        if (_bloom != null)
        {
            // 중심에서 터져 카드 안을 채우고 바깥으로 번진다 (카드 모양과 무관한 부드러운 타원)
            var rt = _bloom.rectTransform;
            rt.sizeDelta = Vector2.Scale(cardSize, _bloomSize);
            rt.localScale = Vector3.one * _bloomFrom;
            _bloom.color = WithAlpha(Color.Lerp(look.Color, Color.white, _bloomWhiten), 0f);
            _bloom.gameObject.SetActive(true);
            _sequence.Insert(0f, _bloom.DOFade(_bloomPeak, 0.05f));
            _sequence.Insert(0f, rt.DOScale(1f, _bloomGrow).SetEase(Ease.OutCubic));
            _sequence.Insert(_bloomGrow * 0.5f, _bloom.DOFade(0f, _bloomFall).SetEase(Ease.OutQuad));
            _sequence.InsertCallback(_bloomGrow * 0.5f + _bloomFall, () => _bloom.gameObject.SetActive(false));
        }
        if (hasShape && _glow != null && _glowMat != null)
        {
            Setup(_glow, _glowMat, silhouette, quad, cardSize, look.Color);
            _glowMat.SetFloat(BorderIntensityId, _baseBorder * look.Intensity);
            _glowMat.SetFloat(GlowIntensityId, _baseGlow * look.Intensity);
            _sequence.Insert(_edgeDelay, _glow.DOFade(1f, _glowRise));
            _sequence.Insert(_edgeDelay + _glowRise + _glowHold, _glow.DOFade(0f, _glowFall).SetEase(Ease.OutQuad));
            InsertGrow(_glow.rectTransform);
            if (!_rectShape && _radiusFrom < 1f)
            {
                // 번짐 폭이 테두리에 붙어 시작해 바깥으로 넓어진다
                Vector4 full = _glowMat.GetVector(RadiusUvId);
                _glowMat.SetVector(RadiusUvId, full * _radiusFrom);
                _sequence.Insert(_edgeDelay, DOTween.To(() => _glowMat.GetVector(RadiusUvId), v => _glowMat.SetVector(RadiusUvId, v),
                                                        full, _glowRise + _glowHold + _glowFall * 0.5f).SetEase(Ease.OutCubic));
            }
        }
        if (hasShape && _sweep != null && _sweepMat != null)
        {
            Setup(_sweep, _sweepMat, silhouette, quad, cardSize, look.Color);
            _sweep.color = WithAlpha(look.Color, 1f);
            _sweepMat.SetFloat(SweepPosId, -0.3f);
            _sequence.Insert(_sweepDelay, _sweepMat.DOFloat(1.3f, SweepPosId, _sweepDuration).SetEase(Ease.InOutSine));
            _sequence.InsertCallback(_sweepDelay + _sweepDuration, () => _sweep.gameObject.SetActive(false));
        }
        if (hasShape && _flash != null && _flashMat != null)
        {
            Setup(_flash, _flashMat, silhouette, quad, cardSize, Color.white);
            _sequence.Insert(_edgeDelay, _flash.DOFade(_flashPeak, 0.03f));
            _sequence.Insert(_edgeDelay + 0.03f, _flash.DOFade(0f, _flashDuration).SetEase(Ease.OutQuad));
            InsertGrow(_flash.rectTransform);
        }
        if (hasShape && _pulse != null && _pulseMat != null)
        {
            Setup(_pulse, _pulseMat, silhouette, quad, cardSize, look.Color);
            var rt = _pulse.rectTransform;
            rt.localScale = Vector3.one * _growFrom;
            _sequence.Insert(_edgeDelay, _pulse.DOFade(1f, 0.04f));
            _sequence.Insert(_edgeDelay, rt.DOScale(_pulseScale, _pulseDuration).SetEase(Ease.OutCubic));
            _sequence.Insert(_edgeDelay + 0.06f, _pulse.DOFade(0f, _pulseDuration - 0.06f).SetEase(Ease.InQuad));
        }
        if (_flare != null && look.Flare)
        {
            var rt = _flare.rectTransform;
            rt.sizeDelta = new Vector2(_flareWidth, _flareHeight);
            rt.localScale = new Vector3(0.2f, 1f, 1f);
            _flare.color = WithAlpha(look.Color, 0f);
            _flare.gameObject.SetActive(true);
            _sequence.Insert(0f, _flare.DOFade(1f, 0.06f));
            _sequence.Insert(0f, rt.DOScaleX(1f, _flareDuration * 0.5f).SetEase(Ease.OutCubic));
            _sequence.Insert(0.08f, _flare.DOFade(0f, _flareDuration).SetEase(Ease.OutQuad));
        }
    }

    // 안→밖 확산: _edgeDelay부터 _growFrom 배율에서 카드 크기(1)까지 커진다. _growFrom이 1이면 배율만 1로 되돌린다.
    private void InsertGrow(RectTransform rt)
    {
        rt.localScale = Vector3.one * _growFrom;
        if (!Mathf.Approximately(_growFrom, 1f))
            _sequence.Insert(_edgeDelay, rt.DOScale(1f, _growDuration).SetEase(Ease.OutCubic));
    }

    // 실루엣 방식: 카드와 같은 스프라이트를 여백만큼 큰 영역에 그리고, 셰이더가 _Expand로 원래 크기 실루엣을 샘플한다.
    // 사각형 방식: 카드 이미지 영역 크기(_BoxHalf)와 쿼드 크기(_QuadSize)만 넘긴다 (스프라이트 없음).
    private void Setup(Image img, Material mat, Sprite sprite, Vector2 quad, Vector2 card, Color color)
    {
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
        img.rectTransform.sizeDelta = quad;
        var safeCard = Vector2.Max(card, Vector2.one);
        if (_rectShape)
        {
            img.sprite = null;
            mat.SetVector(QuadSizeId, quad);
            mat.SetVector(BoxHalfId, safeCard * 0.5f);
        }
        else
        {
            img.sprite = sprite;
            mat.SetVector(ExpandId, new Vector4(quad.x / safeCard.x, quad.y / safeCard.y, 0f, 0f));
            mat.SetVector(RadiusUvId, new Vector4(_glowRadius / safeCard.x, _glowRadius / safeCard.y, 0f, 0f));
        }
        img.color = WithAlpha(color, 0f);
        img.gameObject.SetActive(true);
    }

    private static Material Instance(Image img)
    {
        if (img == null || img.material == null) return null;
        var m = new Material(img.material);
        img.material = m;
        return m;
    }

    private static Color WithAlpha(Color c, float a) { c.a = a; return c; }
}
