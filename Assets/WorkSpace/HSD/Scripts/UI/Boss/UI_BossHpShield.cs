using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보스 HP바 위에 남은 보호막을 네온 파랑 띠로 겹쳐 그린다 (FxLab_BossShieldBar, 사용자 확정 2026-10-22).
/// 확정안: 길이 B(바 오른쪽 끝부터 최대HP 비율) + 2번 유리 실드(BossShieldBand_Glass 재질, 광택이 지나감).
/// 띠는 HP 채움 그림과 같은 모양·높이이고, 경계 발광 이음선을 더한다. 오라·방패 아이콘은 실험실 비교용 선택 항목.
/// 피격 감소는 HP와 같은 보간, 0이 되면 짧게 반짝이며 조각으로 깨지고, 부여 순간 짧게 차오른다.
/// </summary>
public class UI_BossHpShield : MonoBehaviour
{
    /// <summary>보호막 띠 길이를 재는 기준.</summary>
    public enum ShieldSpan
    {
        CurrentLine, // A안: HP 채움 끝부터 줄 단위 길이, 넘치면 현재 줄 위로 덮고 최대 한 줄
        WholeBar,    // B안: 바 오른쪽 끝부터 왼쪽으로 최대HP 비율 길이
    }

    [Header("HP Bar (UI_BossHpBar와 같은 참조)")]
    [Tooltip("HP에 따라 가로 길이가 줄어드는 Mask 영역. 이 오른쪽 끝에서 보호막이 시작한다")]
    [SerializeField] private RectTransform _hpFillMask;
    [Tooltip("HP 채움 그림. 같은 그림으로 띠 모양을 맞춘다")]
    [SerializeField] private Image _hpColor;
    [SerializeField] private Image _hpBackground;
    [SerializeField] private Vector2 _fillSpriteHorizontalPadding;

    [Header("Span")]
    [SerializeField] private ShieldSpan _span = ShieldSpan.WholeBar;

    [Header("Look")]
    [Tooltip("경계 발광 이음선 그림 (세로 빛줄기)")]
    [SerializeField] private Sprite _seamSprite;
    [SerializeField] private Color _bandColor = new Color(0.2f, 0.62f, 1f, 0.85f);
    [SerializeField] private Color _seamColor = new Color(0.72f, 0.97f, 1f, 1f);
    [SerializeField] private Color _flashColor = new Color(0.92f, 1f, 1f, 0.95f);
    [SerializeField, Min(1f)] private float _seamWidth = 22f;
    [Tooltip("이음선 높이 (바 높이 비율). 테두리 안쪽 몸통만 빛나게 한다")]
    [SerializeField, Range(0.1f, 1.2f)] private float _seamHeight = 0.72f;
    [SerializeField, Range(0f, 1f)] private float _pulseMinAlpha = 0.55f;
    [SerializeField, Min(0.1f)] private float _pulsePeriod = 1.1f;
    [Tooltip("띠 무늬 재질 (GGD/UI/BossShieldBand). 비우면 단색 띠")]
    [SerializeField] private Material _bandMaterial;
    [Tooltip("바 뒤에서 번지는 오라 그림 (9-slice). 비우면 없음")]
    [SerializeField] private Sprite _haloSprite;
    [SerializeField] private Color _haloColor = new Color(0.2f, 0.75f, 1f, 0.85f);
    [Tooltip("오라가 띠보다 넓은 양 (x 픽셀, y 바 높이 배수)")]
    [SerializeField] private Vector2 _haloPadding = new Vector2(26f, 1.05f);
    [Tooltip("경계에 붙는 방패 아이콘. 비우면 없음")]
    [SerializeField] private Sprite _iconSprite;
    [SerializeField] private Color _iconColor = new Color(0.55f, 0.93f, 1f, 1f);
    [SerializeField, Min(1f)] private float _iconSize = 58f;

    [Header("Tween")]
    [SerializeField] private float _tweenDuration = 0.32f;
    [SerializeField] private Ease _damageEase = Ease.OutCubic;
    [SerializeField] private float _grantDuration = 0.25f;
    [SerializeField] private Ease _grantEase = Ease.OutQuad;
    [SerializeField] private float _hitFlashDuration = 0.14f;
    [SerializeField, Range(0f, 1f)] private float _hitFlashStrength = 0.45f;
    [SerializeField] private float _breakDuration = 0.28f;
    [SerializeField] private float _breakScaleY = 1.35f;

    private RectTransform _bar;
    private RectTransform _mask;
    private Image _band;
    private Image _seam;
    private Image _halo;
    private Image _icon;
    private Material _bandInstance;
    private static readonly int BandUId = Shader.PropertyToID("_BandU");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");
    private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
    private readonly Vector3[] _corners = new Vector3[4];

    private decimal _target;
    private decimal _maxHp = 1m;
    private decimal _hpPerLine = 1m;
    private float _shown;
    private float _flash;
    private float _alpha = 1f;
    private float _pulse = 1f;
    private float _scaleY = 1f;
    private float _dissolve;
    private bool _initialized;
    private Tween _valueTween;
    private Tween _flashTween;
    private Tween _pulseTween;

    /// <summary>현재 띠 길이 기준.</summary>
    public ShieldSpan Span
    {
        get => _span;
        set { _span = value; Refresh(); }
    }

    /// <summary>표시 중인(보간된) 보호막 양.</summary>
    public float ShownShield => _shown;

    /// <summary>
    /// 남은 보호막을 표시한다. 늘면 차오름, 줄면 HP와 같은 보간, 0이 되면 깨짐 연출.
    /// hpPerLine은 HP바 한 줄이 담당하는 체력 (A안 길이 계산).
    /// </summary>
    public void SetShieldExact(decimal shield, decimal maxHp, decimal hpPerLine)
    {
        decimal nextMax = maxHp > 0m ? maxHp : 1m;
        decimal next = Math.Max(0m, shield);
        _hpPerLine = hpPerLine > 0m ? hpPerLine : nextMax;
        if (_initialized && next == _target && nextMax == _maxHp) return;
        decimal previous = _initialized ? _target : 0m;
        _maxHp = nextMax;
        _target = next;
        float to = (float)next;

        if (!_initialized || !Application.isPlaying || !isActiveAndEnabled)
        {
            KillValueTweens();
            _initialized = true;
            _shown = to;
            _flash = 0f;
            _alpha = 1f;
            _scaleY = 1f;
            _dissolve = 0f;
            Refresh();
            return;
        }

        if (next > previous) Grant(to);
        else if (next <= 0m) Break();
        else Damage(to);
    }

    /// <summary>연출 없이 즉시 비운다 (보스 교체 등).</summary>
    public void ClearImmediate()
    {
        KillValueTweens();
        _target = 0m;
        _shown = 0f;
        _flash = 0f;
        _alpha = 1f;
        _scaleY = 1f;
        _dissolve = 0f;
        Refresh();
    }

    private void Grant(float to)
    {
        KillValueTweens();
        // 깨지는 중에 다시 부여되면 0부터 새로 차오른다.
        if (_alpha < 1f) _shown = 0f;
        _alpha = 1f;
        _scaleY = 1f;
        _dissolve = 0f;
        _valueTween = DOTween.To(() => _shown, v => { _shown = v; Refresh(); }, to, _grantDuration)
            .SetEase(_grantEase).SetUpdate(true);
        _flash = 1f;
        _flashTween = DOTween.To(() => _flash, v => { _flash = v; Refresh(); }, 0f, _grantDuration + 0.1f)
            .SetEase(Ease.OutQuad).SetUpdate(true);
    }

    private void Damage(float to)
    {
        KillValueTweens();
        _alpha = 1f;
        _scaleY = 1f;
        _dissolve = 0f;
        _valueTween = DOTween.To(() => _shown, v => { _shown = v; Refresh(); }, to, _tweenDuration)
            .SetEase(_damageEase).SetUpdate(true);
        _flash = Mathf.Max(_flash, _hitFlashStrength);
        _flashTween = DOTween.To(() => _flash, v => { _flash = v; Refresh(); }, 0f, _hitFlashDuration)
            .SetEase(Ease.OutQuad).SetUpdate(true);
    }

    // 마지막 길이 그대로 하얗게 번쩍인 뒤 무늬 칸 단위로 부서지며(재질이 있을 때) 위아래로 살짝 부풀며 사라진다.
    private void Break()
    {
        KillValueTweens();
        if (_shown <= 0f) { Refresh(); return; }
        float flashIn = Mathf.Min(0.05f, _breakDuration * 0.25f);
        var sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Append(DOTween.To(() => _flash, v => { _flash = v; Refresh(); }, 1f, flashIn));
        sequence.Append(DOTween.To(() => _dissolve, v => { _dissolve = v; Refresh(); }, 1f, _breakDuration).SetEase(Ease.OutQuad));
        sequence.Join(DOTween.To(() => _alpha, v => { _alpha = v; Refresh(); }, 0f, _breakDuration).SetEase(_bandMaterial != null ? Ease.InCubic : Ease.InQuad));
        sequence.Join(DOTween.To(() => _scaleY, v => { _scaleY = v; Refresh(); }, _breakScaleY, _breakDuration).SetEase(Ease.OutQuad));
        sequence.OnComplete(() =>
        {
            _shown = 0f;
            _flash = 0f;
            _alpha = 1f;
            _scaleY = 1f;
            _dissolve = 0f;
            Refresh();
        });
        _flashTween = sequence;
    }

    private void KillValueTweens()
    {
        _valueTween?.Kill();
        _flashTween?.Kill();
        _valueTween = _flashTween = null;
    }

    private void OnEnable()
    {
        _pulse = 1f;
        _pulseTween?.Kill();
        _pulseTween = DOTween.To(() => _pulse, v => _pulse = v, 0f, _pulsePeriod * 0.5f)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
    }

    private void OnDisable() => _pulseTween?.Kill();

    private void OnDestroy()
    {
        KillValueTweens();
        _pulseTween?.Kill();
        if (_bandInstance != null) Destroy(_bandInstance);
    }

    // HP 채움이 보간되는 동안 띠 시작점이 따라가야 하므로 보일 때만 매 프레임 배치한다.
    private void LateUpdate()
    {
        if (_shown > 0f) Refresh();
    }

    private void Refresh()
    {
        bool visible = _shown > 0f && _alpha > 0f && _hpFillMask != null;
        if (!visible)
        {
            if (_mask != null) _mask.gameObject.SetActive(false);
            if (_seam != null) _seam.enabled = false;
            if (_halo != null) _halo.enabled = false;
            if (_icon != null) _icon.enabled = false;
            return;
        }
        EnsureObjects();
        if (_bar == null) return;

        // UI_BossHpBar.ApplyBarVisual과 같은 계산: 채움 그림의 가로 자리·폭과 투명 여백.
        float artLeft = 0f, artWidth = _bar.rect.width;
        if (_hpBackground != null)
        {
            _hpBackground.rectTransform.GetWorldCorners(_corners);
            float left = _bar.InverseTransformPoint(_corners[0]).x;
            float right = _bar.InverseTransformPoint(_corners[2]).x;
            artLeft = left - _bar.rect.xMin;
            artWidth = right - left;
        }
        Vector2 padding = Vector2.zero;
        if (_hpColor != null && _hpColor.sprite != null && _hpColor.sprite.rect.width > 0f)
            padding = _fillSpriteHorizontalPadding * (artWidth / _hpColor.sprite.rect.width);
        float usableLeft = artLeft + padding.x;
        float usableRight = artLeft + artWidth - padding.y;
        float usableWidth = Mathf.Max(0f, usableRight - usableLeft);

        _hpFillMask.GetWorldCorners(_corners);
        float fillRight = Mathf.Clamp(_bar.InverseTransformPoint(_corners[2]).x - _bar.rect.xMin, usableLeft, usableRight);

        float start, width;
        if (_span == ShieldSpan.CurrentLine)
        {
            float lines = _shown / Mathf.Max(1e-6f, (float)_hpPerLine);
            width = Mathf.Clamp01(lines) * usableWidth;
            start = fillRight;
            if (start + width > usableRight) start = usableRight - width;
        }
        else
        {
            width = Mathf.Clamp01(_shown / Mathf.Max(1e-6f, (float)_maxHp)) * usableWidth;
            start = usableRight - width;
        }

        _mask.gameObject.SetActive(true);
        _mask.anchorMin = new Vector2(0f, _hpFillMask.anchorMin.y);
        _mask.anchorMax = new Vector2(0f, _hpFillMask.anchorMax.y);
        _mask.offsetMin = new Vector2(start, _hpFillMask.offsetMin.y);
        _mask.offsetMax = new Vector2(start + width, _hpFillMask.offsetMax.y);
        _mask.localScale = new Vector3(1f, Mathf.Lerp(1f, _scaleY, 0.4f), 1f);

        // 띠 그림은 채움 그림과 같은 전체 폭 배치로 두고 Mask가 자른다 (둥근 끝 모양 유지).
        var bandRect = _band.rectTransform;
        bandRect.anchorMin = new Vector2(0f, 0f);
        bandRect.anchorMax = new Vector2(0f, 1f);
        bandRect.pivot = new Vector2(0f, 0.5f);
        bandRect.anchoredPosition = new Vector2(artLeft - start, 0f);
        bandRect.sizeDelta = new Vector2(artWidth, 0f);
        Color band = Color.Lerp(_bandColor, _flashColor, _flash);
        band.a *= _alpha * Mathf.Lerp(0.85f, 1f, _pulse);
        _band.color = band;

        // 경계 이음선: HP와 맞닿는 왼쪽 끝. 잘리지 않게 Mask 밖 형제로 둔다.
        var seamRect = _seam.rectTransform;
        float barHeight = _bar.rect.height;
        seamRect.anchorMin = seamRect.anchorMax = new Vector2(0f, 0.5f);
        seamRect.pivot = new Vector2(0.5f, 0.5f);
        seamRect.anchoredPosition = new Vector2(start, 0f);
        seamRect.sizeDelta = new Vector2(_seamWidth, barHeight * _seamHeight);
        seamRect.localScale = new Vector3(1f, _scaleY, 1f);
        Color seam = Color.Lerp(_seamColor, Color.white, _flash);
        seam.a *= _alpha * Mathf.Lerp(_pulseMinAlpha, 1f, Mathf.Max(_pulse, _flash));
        _seam.color = seam;
        _seam.enabled = _seamSprite != null && width > 1f;

        if (_bandInstance != null)
        {
            float inv = artWidth > 0f ? 1f / artWidth : 0f;
            // 바 오른쪽 끝에 닿은 쪽은 보호막의 경계가 아니므로 테두리 빛을 넣지 않는다 (U를 멀리 보낸다).
            float endU = start + width >= usableRight - 0.5f ? 2f : (start + width - artLeft) * inv;
            _bandInstance.SetVector(BandUId, new Vector4((start - artLeft) * inv, endU, 0f, 0f));
            _bandInstance.SetFloat(AspectId, barHeight > 0f ? artWidth / barHeight : 1f);
            _bandInstance.SetFloat(DissolveId, _dissolve);
        }

        if (_halo != null)
        {
            var haloRect = _halo.rectTransform;
            haloRect.anchorMin = haloRect.anchorMax = new Vector2(0f, 0.5f);
            haloRect.pivot = new Vector2(0f, 0.5f);
            haloRect.anchoredPosition = new Vector2(start - _haloPadding.x, 0f);
            haloRect.sizeDelta = new Vector2(width + _haloPadding.x * 2f, barHeight * _haloPadding.y * _scaleY);
            Color halo = Color.Lerp(_haloColor, Color.white, _flash * 0.6f);
            halo.a *= _alpha * Mathf.Lerp(0.6f, 1f, Mathf.Max(_pulse, _flash));
            _halo.color = halo;
            _halo.enabled = width > 1f;
        }

        if (_icon != null)
        {
            var iconRect = _icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(start, 0f);
            float pop = 1f + 0.25f * _flash + (_scaleY - 1f);
            iconRect.sizeDelta = new Vector2(_iconSize, _iconSize);
            iconRect.localScale = new Vector3(pop, pop, 1f);
            Color icon = Color.Lerp(_iconColor, Color.white, _flash);
            icon.a *= _alpha;
            _icon.color = icon;
            _icon.enabled = true;
        }
    }

    private void EnsureObjects()
    {
        if (_mask != null) return;
        _bar = _hpFillMask.parent as RectTransform;
        if (_bar == null) return;
        int index = _hpFillMask.GetSiblingIndex() + 1;

        var maskGo = new GameObject("HpShieldMask", typeof(RectTransform), typeof(RectMask2D));
        maskGo.transform.SetParent(_bar, false);
        maskGo.transform.SetSiblingIndex(index);
        _mask = (RectTransform)maskGo.transform;

        var bandGo = new GameObject("HpShieldBand", typeof(RectTransform), typeof(Image));
        bandGo.transform.SetParent(_mask, false);
        _band = bandGo.GetComponent<Image>();
        _band.raycastTarget = false;
        if (_hpColor != null)
        {
            _band.sprite = _hpColor.sprite;
            _band.type = _hpColor.type;
            _band.pixelsPerUnitMultiplier = _hpColor.pixelsPerUnitMultiplier;
        }

        var seamGo = new GameObject("HpShieldSeam", typeof(RectTransform), typeof(Image));
        seamGo.transform.SetParent(_bar, false);
        seamGo.transform.SetSiblingIndex(index + 1);
        _seam = seamGo.GetComponent<Image>();
        _seam.raycastTarget = false;
        _seam.sprite = _seamSprite;

        if (_bandMaterial != null)
        {
            // 띠마다 경계 U가 달라 재질을 복제한다 (보스 HP바는 화면에 하나).
            _bandInstance = new Material(_bandMaterial);
            _band.material = _bandInstance;
        }
        if (_haloSprite != null)
        {
            // 바 몸통 뒤(맨 앞 형제)에 두어 알약 바깥으로만 번지게 한다.
            var haloGo = new GameObject("HpShieldHalo", typeof(RectTransform), typeof(Image));
            haloGo.transform.SetParent(_bar, false);
            haloGo.transform.SetSiblingIndex(0);
            _halo = haloGo.GetComponent<Image>();
            _halo.raycastTarget = false;
            _halo.sprite = _haloSprite;
            _halo.type = Image.Type.Sliced;
        }
        if (_iconSprite != null)
        {
            var iconGo = new GameObject("HpShieldIcon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(_bar, false);
            _icon = iconGo.GetComponent<Image>();
            _icon.raycastTarget = false;
            _icon.sprite = _iconSprite;
            _icon.preserveAspect = true;
        }
    }
}
