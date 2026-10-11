using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>종료 시 마지막 보스의 HP 감소율을 세 가지 담백한 후보로 표현한다.</summary>
public class ResultBossProgressView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TextMeshProUGUI _caption;
    [SerializeField] private GameObject[] _variants;
    [SerializeField] private RectTransform _hpFill;
    [SerializeField] private RectTransform _stamp;
    [SerializeField] private RectTransform _stampReveal;
    [SerializeField] private RectTransform _lineFill;
    [SerializeField] private RectTransform _marker;
    [SerializeField] private Image _finish;
    [SerializeField, Min(0.1f)] private float _duration = 1.05f;
    [SerializeField, Range(0f, 1f)] private float _nearFinish = 0.9f;
    [SerializeField] private bool _showRemainingOnly;
    private ResultBossProgressStyle _style;
    private float _shown;

    /// <summary>HP 기록이 있을 때만 표시하고 선택한 후보를 초기화한다.</summary>
    public void Prepare(ResultScreenData data)
    {
        bool visible = data.LastBossDamageRatio >= 0f;
        gameObject.SetActive(visible);
        _style = (ResultBossProgressStyle)Mathf.Clamp((int)data.BossProgressStyle, 0, _variants.Length - 1);
        for (int i = 0; i < _variants.Length; i++) _variants[i].SetActive(i == (int)_style);
        _group.alpha = 0f;
        _stamp.localScale = Vector3.one * 1.15f;
        _finish.color = new Color(1f, 1f, 1f, 0.35f);
        _marker.localScale = Vector3.one;
        Render(0f);
    }

    /// <summary>결과 Sequence가 소유할 HP 연출을 만든다. 건너뛰기와 재생 취소도 함께 처리된다.</summary>
    public Sequence CreateAnimation(float ratio)
    {
        var sequence = DOTween.Sequence();
        sequence.Append(_group.DOFade(1f, 0.16f));
        if (_style == ResultBossProgressStyle.PartialStamp)
            sequence.Join(_stamp.DOScale(1f, 0.2f).SetEase(Ease.OutBack));
        sequence.Append(DOTween.To(() => _shown, Render, Mathf.Clamp01(ratio), _duration).SetEase(Ease.OutCubic));
        if (_style == ResultBossProgressStyle.FinishLine && ratio >= _nearFinish)
        {
            sequence.Append(_marker.DOScale(1.45f, 0.12f).SetEase(Ease.OutQuad));
            sequence.Join(_finish.DOColor(new Color(0.45f, 0.95f, 0.8f, 1f), 0.12f));
            sequence.Append(_marker.DOScale(1f, 0.18f).SetEase(Ease.OutQuad));
        }
        return sequence;
    }

    /// <summary>건너뛰기와 자연 완료 모두에서 마지막 잔여 HP를 확정한다.</summary>
    public void Settle(float ratio)
    {
        _group.alpha = 1f;
        _stamp.localScale = Vector3.one;
        _marker.localScale = Vector3.one;
        if (_style == ResultBossProgressStyle.FinishLine && ratio >= _nearFinish)
            _finish.color = new Color(0.45f, 0.95f, 0.8f, 1f);
        Render(Mathf.Clamp01(ratio));
    }

    private void Render(float progress)
    {
        _shown = progress;
        int damage = Mathf.RoundToInt(progress * 100f);
        _caption.text = _showRemainingOnly ? $"{100 - damage}% 남음" : $"HP {damage}% 감소  ·  {100 - damage}% 남음";
        _hpFill.localScale = new Vector3(1f - progress, 1f, 1f);
        _stampReveal.sizeDelta = new Vector2(_stamp.rect.width * progress, _stampReveal.sizeDelta.y);
        _lineFill.localScale = new Vector3(progress, 1f, 1f);
        _marker.anchoredPosition = new Vector2(-280f + 560f * progress, 0f);
    }
}
