using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.EventSystems;

/// <summary>
/// 레벨업 카드 하나의 UI (스프라이트 애니메이션 방식)
///
/// ─ 프리팹 구성 ────────────────────────────────────────
///   CardRoot  (LevelUpCardUI + Button)
///     ├── BorderImage    (Image — 등급 테두리)
///     ├── IconImage      (Image — 카드 아이콘 & 애니메이션)
///     └── DescriptionText(TMP_Text — 설명 텍스트)
///
/// ─ Inspector 연결 ─────────────────────────────────────
///   iconImage, borderImage, descriptionText, button
///   borderSprites[4] (0=Normal 1=Rare 2=Epic 3=Legend)
/// </summary>
public class LevelUpCardUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image    iconImage;
    [SerializeField] private Image    borderImage;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Button   button;

    [Header("Tier Border Sprites (0=Normal 1=Rare 2=Epic 3=Legend)")]
    [SerializeField] private Sprite[] borderSprites;

    [Header("Scale Animation")]
    [SerializeField] private float selectedScale  = 1.3f;
    [SerializeField] private float scaleDuration  = 0.25f;

    private LevelUpData           _data;
    private Action<LevelUpCardUI> _onCardClicked;
    private CancellationTokenSource _animCts;
    [Header("Hold to view field")]
    [SerializeField, Min(0.1f)] private float _holdSeconds = 0.4f;
    private UI_Peekthrough _peek;
    private int? _pointerId;
    private int? _releasedPointerId;
    private float _pressedAt;
    private bool _suppressClick;

    /// <summary>카드가 길게 눌렸을 때 사용할 레벨업 창의 필드 보기를 연결한다.</summary>
    public void ConfigurePeek(UI_Peekthrough peek) => _peek = peek;

    /// <summary>이 카드로 필드보기가 시작(true)/종료(false)될 때 — 대상 유닛 강조 연출용.</summary>
    public event Action<LevelUpCardUI, bool> OnPeekChanged;
    private bool _peeking;

    private void SetPeeking(bool peeking)
    {
        if (_peeking == peeking) return;
        _peeking = peeking;
        OnPeekChanged?.Invoke(this, peeking);
    }

    /// <summary>짧은 선택과 긴 필드 보기를 구분하는 입력을 시작한다.</summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || _pointerId.HasValue ||
            button == null || !button.IsInteractable()) return;
        _pointerId = eventData.pointerId;
        _releasedPointerId = null;
        _pressedAt = Time.unscaledTime;
        _suppressClick = false;
    }

    private void Update()
    {
        if (_pointerId.HasValue && !_suppressClick && _peek != null &&
            Time.unscaledTime - _pressedAt >= _holdSeconds)
        {
            _suppressClick = true;
            if (_peek.TryBeginPeek(this)) SetPeeking(true);
        }
    }

    /// <summary>긴 누름의 해제는 카드를 선택하지 않고 필드 보기만 종료한다.</summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (_pointerId != eventData.pointerId) return;
        // A release can arrive before Update on the threshold frame.
        if (_peek != null && Time.unscaledTime - _pressedAt >= _holdSeconds) _suppressClick = true;
        _releasedPointerId = eventData.pointerId;
        _pointerId = null;
        _peek?.EndPeek(this);
        SetPeeking(false);
    }

    /// <summary>카드 밖으로 이동하면 해당 누름을 취소한다.</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (_pointerId == eventData.pointerId) CancelPress();
    }

    private void CancelPress()
    {
        _pointerId = null;
        _releasedPointerId = null;
        _suppressClick = true;
        _peek?.EndPeek(this);
        SetPeeking(false);
    }

    private void OnDisable() => CancelPress();

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) CancelPress();
    }

    private void Awake()
    {
        if (button == null)
            Debug.LogError("[LevelUpCardUI] Button 연결 안됨");

    }

    // ── 초기화 ─────────────────────────────────────────────────

    public void Setup(LevelUpData data, Action<LevelUpCardUI> onCardClicked, string resolvedDescription = null)
    {
        _data          = data;
        _onCardClicked = onCardClicked;

        if (iconImage != null)
            iconImage.sprite = data?.icon;

        if (descriptionText != null)
            descriptionText.text = resolvedDescription ?? data?.description ?? string.Empty;

        if (nameText != null)
            nameText.text = data.chooseName;

        ApplyTierSprites(data?.tier ?? Tier.Normal);
    }

    // ── 선택/해제 ──────────────────────────────────────────────

    public void Select()
    {
        bool hasAnim = _data != null
                    && _data.animationFrames != null
                    && _data.animationFrames.Length > 0;

        if (hasAnim)
            StartAnim();

        transform.SetAsLastSibling();

        transform.DOKill();
        transform.DOScale(selectedScale, scaleDuration).SetEase(Ease.InOutElastic).SetUpdate(true);
    }

    public void Deselect()
    {
        StopAnim();

        if (iconImage != null && _data?.icon != null)
            iconImage.sprite = _data.icon;

        transform.DOKill();
        transform.DOScale(1f, scaleDuration).SetEase(Ease.InOutQuad).SetUpdate(true);
    }

    public LevelUpData GetData() => _data;

    /// <summary>등장 연출의 빛 덮개가 카드 외곽 모양을 따르도록 쓰는 테두리 이미지.</summary>
    public Image RevealShape => borderImage;

    /// <summary>선택 후 획득 연출에 쓰는 선택지 아이콘 (애니메이션 프레임이 아닌 기본 아이콘).</summary>
    public Sprite IconSprite => _data?.icon != null ? _data.icon : iconImage != null ? iconImage.sprite : null;

    // ── 내부 ───────────────────────────────────────────────────

    /// <summary>같은 손가락의 짧은 탭만 한 번 선택한다.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (_releasedPointerId != eventData.pointerId || eventData.button != PointerEventData.InputButton.Left) return;
        _releasedPointerId = null;
        if (!_suppressClick && button != null && button.IsInteractable()) _onCardClicked?.Invoke(this);
    }

    private void ApplyTierSprites(Tier tier)
    {
        int idx = (int)tier;

        if (borderImage != null && borderSprites != null && idx < borderSprites.Length)
            borderImage.sprite = borderSprites[idx];
    }

    private void StartAnim()
    {
        StopAnim();
        _animCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        PlayAnimationAsync(_animCts.Token).Forget(e => { if (e is not System.OperationCanceledException) UnityEngine.Debug.LogException(e); });
    }

    private void StopAnim()
    {
        if (_animCts == null) return;
        _animCts.Cancel();
        _animCts.Dispose();
        _animCts = null;
    }

    private async UniTask PlayAnimationAsync(CancellationToken token)
    {
        try
        {
            var   frames   = _data.animationFrames;
            float interval = 1f / Mathf.Max(_data.frameRate, 1f);
            int   index    = 0;

            while (true)
            {
                token.ThrowIfCancellationRequested();

                if (iconImage != null)
                    iconImage.sprite = frames[index];

                index = (index + 1) % frames.Length;
                await UniTask.Delay(
                    TimeSpan.FromSeconds(interval),
                    cancellationToken: token);
            }
        }
        catch (OperationCanceledException) { }
    }

    private void OnDestroy()
    {
        transform.DOKill();
        StopAnim();
    }
}
