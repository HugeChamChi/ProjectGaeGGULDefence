using UnityEngine;
using TMPro;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;
using DG.Tweening;

public class DamageFloater : MonoBehaviour
{
    private TextMeshProUGUI _tmp;
    private CancellationTokenSource _cts;

    private void Awake()
    {
        _tmp = GetComponent<TextMeshProUGUI>();
    }

    public void SetupAndPlay(string text, DamageFloaterStyle style = null, bool isCritical = false)
    {
        if (_tmp == null) _tmp = GetComponent<TextMeshProUGUI>();

        if (style == null)
        {
            style = ScriptableObject.CreateInstance<DamageFloaterStyle>();
            if (isCritical)
            {
                style.textColor = Color.red;
                style.fontSize = 60f;
                style.isBold = true;
            }
        }

        // 1. 스타일 적용
        if (style.fontAsset != null) _tmp.font = style.fontAsset;
        if (style.fontMaterial != null) _tmp.fontSharedMaterial = style.fontMaterial;
        
        _tmp.color = style.textColor;
        _tmp.fontSize = style.fontSize;
        _tmp.fontStyle = style.isBold ? FontStyles.Bold : FontStyles.Normal;
        _tmp.text = text;

        // 2. 랜덤 오프셋
        float randomX = UnityEngine.Random.Range(-style.randomizeOffsetX, style.randomizeOffsetX);
        float randomY = UnityEngine.Random.Range(-style.randomizeOffsetY, style.randomizeOffsetY);
        transform.localPosition += new Vector3(randomX, randomY, 0);

        // 3. DOTween 애니메이션
        PlayAnimation(style);
    }

    private void PlayAnimation(DamageFloaterStyle style)
    {
        // 이전 트윈 제거
        transform.DOKill();
        _tmp.DOKill();

        // 초기 상태
        transform.localScale = Vector3.zero;
        Color c = _tmp.color;
        c.a = 1f;
        _tmp.color = c;

        // 시퀀스 구성
        Sequence seq = DOTween.Sequence();

        // [등장] 살짝 커졌다 돌아오며 등장 (Juicy)
        seq.Append(transform.DOScale(1.2f, 0.1f).SetEase(Ease.OutBack));
        seq.Append(transform.DOScale(1.0f, 0.1f));

        // [상승] UI라면 anchoredPosition, 아니면 LocalPosition 사용
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
        seq.Join(transform.DOScale(0.5f, style.duration).SetEase(Ease.InQuad).SetDelay(0.2f));
        seq.Join(_tmp.DOFade(0, style.duration).SetEase(Ease.InQuad).SetDelay(0.2f));

        seq.OnComplete(() =>
        {
            RM.Destroy(gameObject);
        });
    }

    private void OnDisable()
    {
        transform.DOKill();
        _tmp.DOKill();
    }
}
