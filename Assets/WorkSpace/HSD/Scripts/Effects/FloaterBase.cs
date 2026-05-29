using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// 모든 플로터 UI의 기반 클래스.
/// 텍스트의 공통 애니메이션 및 설정을 관리합니다.
/// </summary>
public abstract class FloaterBase : MonoBehaviour
{
    [Header("Base Components")]
    [SerializeField] protected TextMeshProUGUI _tmp;
    
    protected float _fadeDelay = 0.2f; // 페이드 아웃 시작 지연 시간

    protected virtual void Awake()
    {
        if (_tmp == null) _tmp = GetComponent<TextMeshProUGUI>();
    }

    /// <summary>
    /// 플로터의 기본 스타일과 텍스트를 설정합니다.
    /// </summary>
    protected virtual void SetupBase(string text, DamageFloaterStyle style)
    {
        if (style == null) return;

        // 1. 텍스트 스타일 적용
        if (_tmp != null)
        {
            if (style.fontAsset != null) _tmp.font = style.fontAsset;
            if (style.fontMaterial != null) _tmp.fontSharedMaterial = style.fontMaterial;

            _tmp.color = style.textColor;
            _tmp.fontSize = style.fontSize;
            _tmp.fontStyle = style.isBold ? FontStyles.Bold : FontStyles.Normal;
            _tmp.text = text;
        }

        // 2. 랜덤 오프셋
        float randomX = Random.Range(-style.randomizeOffsetX, style.randomizeOffsetX);
        float randomY = Random.Range(-style.randomizeOffsetY, style.randomizeOffsetY);
        transform.localPosition += new Vector3(randomX, randomY, 0);
    }

    protected virtual void PlayAnimation(DamageFloaterStyle style)
    {
        if (style == null) return;

        // 이전 트윈 제거
        transform.DOKill();
        _tmp?.DOKill();

        // 초기 상태
        transform.localScale = Vector3.zero;
        
        if (_tmp != null)
        {
            Color c = _tmp.color;
            c.a = 1f;
            _tmp.color = c;
        }

        // 시퀀스 구성
        Sequence seq = DOTween.Sequence();

        // [등장]
        seq.Append(transform.DOScale(1.2f, 0.1f).SetEase(Ease.OutBack));
        seq.Append(transform.DOScale(1.0f, 0.1f));

        // [상승]
        RectTransform rect = transform as RectTransform;
        if (rect != null)
        {
            seq.Join(rect.DOAnchorPosY(rect.anchoredPosition.y + style.moveDistance, style.duration).SetEase(Ease.OutQuad));
        }
        else
        {
            seq.Join(transform.DOLocalMoveY(transform.localPosition.y + style.moveDistance, style.duration).SetEase(Ease.OutQuad));
        }
        
        // [작아지며 소멸]
        seq.Join(transform.DOScale(0.5f, style.duration).SetEase(Ease.InQuad).SetDelay(_fadeDelay));
        
        if (_tmp != null)
            seq.Join(_tmp.DOFade(0, style.duration).SetEase(Ease.InQuad).SetDelay(_fadeDelay));
        
        // 추가 애니메이션 훅 (서브클래스용)
        AnimateExtra(seq, style);

        seq.OnComplete(() =>
        {
            RM.Destroy(gameObject);
        });
    }

    /// <summary>
    /// 아이콘 등 추가적인 컴포넌트의 애니메이션을 위한 훅입니다.
    /// </summary>
    protected virtual void AnimateExtra(Sequence seq, DamageFloaterStyle style) { }

    protected virtual void OnDisable()
    {
        transform.DOKill();
        _tmp?.DOKill();
    }
}
