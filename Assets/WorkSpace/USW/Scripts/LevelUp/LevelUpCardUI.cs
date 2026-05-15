using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// 레벨업 카드 하나의 UI (스프라이트 애니메이션 방식)
///
/// ─ 프리팹 구성 ────────────────────────────────────────
///   CardRoot  (LevelUpCardUI + Button)
///     ├── BorderImage    (Image — 등급 테두리)
///     ├── IconBorderImage(Image — 아이콘 테두리)
///     ├── IconImage      (Image — 카드 아이콘 & 애니메이션)
///     └── DescriptionText(TMP_Text — 설명 텍스트)
///
/// ─ Inspector 연결 ─────────────────────────────────────
///   iconImage, iconBorderImage, borderImage, descriptionText, button
///   borderSprites[4], iconBorderSprites[4] (0=Normal 1=Rare 2=Epic 3=Legend)
/// </summary>
public class LevelUpCardUI : MonoBehaviour
{
    [SerializeField] private Image    iconImage;
    [SerializeField] private Image    iconBorderImage;
    [SerializeField] private Image    borderImage;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text tierText;
    [SerializeField] private Button   button;

    [Header("Tier Border Sprites (0=Normal 1=Rare 2=Epic 3=Legend)")]
    [SerializeField] private Sprite[] borderSprites;

    [Header("Tier Icon Border Sprites (0=Normal 1=Rare 2=Epic 3=Legend)")]
    [SerializeField] private Sprite[] iconBorderSprites;

    [Header("Scale Animation")]
    [SerializeField] private float selectedScale  = 1.3f;
    [SerializeField] private float scaleDuration  = 0.25f;

    private LevelUpData           _data;
    private Action<LevelUpCardUI> _onCardClicked;
    private CancellationTokenSource _animCts;

    private void Awake()
    {
        if (button == null)
            Debug.LogError("[LevelUpCardUI] Button 연결 안됨");

        button?.onClick.AddListener(OnClick);
    }

    // ── 초기화 ─────────────────────────────────────────────────

    public void Setup(LevelUpData data, Action<LevelUpCardUI> onCardClicked)
    {
        _data          = data;
        _onCardClicked = onCardClicked;

        if (iconImage != null)
            iconImage.sprite = data?.icon;

        if (descriptionText != null)
            descriptionText.text = data?.description ?? string.Empty;

        if (tierText != null)
            tierText.text = TierToLabel(data?.tier ?? Tier.Normal);

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

    // ── 내부 ───────────────────────────────────────────────────

    private void OnClick() => _onCardClicked?.Invoke(this);

    private static string TierToLabel(Tier tier) => tier switch
    {
        Tier.Normal => "노말",
        Tier.Rare   => "레어",
        Tier.Epic   => "에픽",
        Tier.Legend => "전설",
        _           => "노말",
    };

    private void ApplyTierSprites(Tier tier)
    {
        int idx = (int)tier;

        if (borderImage != null && borderSprites != null && idx < borderSprites.Length)
            borderImage.sprite = borderSprites[idx];

        if (iconBorderImage != null && iconBorderSprites != null && idx < iconBorderSprites.Length)
            iconBorderImage.sprite = iconBorderSprites[idx];
    }

    private void StartAnim()
    {
        StopAnim();
        _animCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        PlayAnimationAsync(_animCts.Token).Forget(Debug.LogException);
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
