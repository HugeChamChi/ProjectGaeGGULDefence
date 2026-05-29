using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 재화 획득 수치 표시를 담당하는 플로터.
/// 아이콘을 포함하며, 텍스트와 함께 아이콘 애니메이션을 수행합니다.
/// </summary>
public class CurrencyFloater : FloaterBase
{
    [Header("Currency Components")]
    [SerializeField] private Image _icon;

    protected override void Awake()
    {
        base.Awake();
        if (_icon == null) _icon = GetComponentInChildren<Image>();
        
        // 재화 플로터는 처음부터 투명도가 줄어들도록 설정
        _fadeDelay = 0f;
    }

    public void SetupAndPlay(string text, DamageFloaterStyle style, bool isCritical = false)
    {
        SetupBase(text, style);
        
        // 아이콘 초기 알파 설정
        if (_icon != null)
        {
            Color c = _icon.color;
            c.a = 1f;
            _icon.color = c;
        }

        PlayAnimation(style);
    }

    protected override void AnimateExtra(Sequence seq, DamageFloaterStyle style)
    {
        if (_icon != null)
        {
            // 아이콘도 지연 시간 없이( _fadeDelay = 0 ) 즉시 페이드 아웃 시작
            seq.Join(_icon.DOFade(0, style.duration).SetEase(Ease.InQuad).SetDelay(_fadeDelay));
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        _icon?.DOKill();
    }
}
