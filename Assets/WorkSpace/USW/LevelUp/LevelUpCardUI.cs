using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 레벨업 카드 하나의 UI (스프라이트 애니메이션 방식)
///
/// ─ 프리팹 구성 ────────────────────────────────────────
///   CardRoot  (LevelUpCardUI + Button)
///     ├── CardImage      (Image — 커버 & 애니메이션 표시)
///     └── DescriptionText(TMP_Text — 설명 텍스트)
///
/// ─ Inspector 연결 ─────────────────────────────────────
///   cardImage, descriptionText, button
///
/// ─ 동작 ───────────────────────────────────────────────
///   클릭        → 스프라이트 애니메이션 루프 재생
///   Deselect()  → 애니메이션 정지, 커버 이미지 복귀
///   확인 버튼   → LevelUpUI.OnConfirmClicked() (별도 버튼)
/// </summary>
public class LevelUpCardUI : MonoBehaviour
{
    [SerializeField] private Image    cardImage;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button   button;

    [Header("Scale Animation")]
    [SerializeField] private float selectedScale  = 1.3f;
    [SerializeField] private float scaleDuration  = 0.25f;

    private LevelUpData           _data;
    private Action<LevelUpCardUI> _onCardClicked;
    private Coroutine             _animCoroutine;

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

        if (cardImage != null)
            cardImage.sprite = data?.icon;

        if (descriptionText != null)
            descriptionText.text = data?.description ?? string.Empty;
    }

    // ── 선택/해제 ──────────────────────────────────────────────

    /// <summary>이 카드가 선택됨 → 스프라이트 애니메이션 재생</summary>
    public void Select()
    {
        bool hasAnim = _data != null
                    && _data.animationFrames != null
                    && _data.animationFrames.Length > 0;

        if (hasAnim)
        {
            StopAnim();
            _animCoroutine = StartCoroutine(PlayAnimation());
        }

        transform.DOKill();
        transform.DOScale(selectedScale, scaleDuration).SetEase(Ease.InOutElastic);
    }

    /// <summary>다른 카드가 선택됨 → 애니메이션 정지 + 커버 복귀</summary>
    public void Deselect()
    {
        StopAnim();

        if (cardImage != null && _data?.icon != null)
            cardImage.sprite = _data.icon;

        transform.DOKill();
        transform.DOScale(1f, scaleDuration).SetEase(Ease.InOutQuad);
    }

    public LevelUpData GetData() => _data;

    // ── 내부 ───────────────────────────────────────────────────

    private void OnClick()
    {
        Debug.Log($"[LevelUpCardUI] 카드 클릭됨 : {_data?.description}");
        _onCardClicked?.Invoke(this);
    }

    private IEnumerator PlayAnimation()
    {
        var   frames   = _data.animationFrames;
        float interval = 1f / Mathf.Max(_data.frameRate, 1f);
        int   index    = 0;

        while (true)
        {
            if (cardImage != null)
                cardImage.sprite = frames[index];

            index = (index + 1) % frames.Length;
            yield return new WaitForSeconds(interval);
        }
    }

    private void StopAnim()
    {
        if (_animCoroutine != null)
        {
            StopCoroutine(_animCoroutine);
            _animCoroutine = null;
        }
    }

    private void OnDestroy()
    {
        transform.DOKill();
        StopAnim();
    }
}
