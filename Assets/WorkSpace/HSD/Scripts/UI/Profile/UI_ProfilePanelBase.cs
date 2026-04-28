using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

public abstract class UI_ProfilePanelBase : UI_Base
{
    [SerializeField] protected GameObject background;

    [Header("Player Info")]
    [SerializeField] protected TextMeshProUGUI playerName;

    [Header("Components")]
    [SerializeField] protected UI_ProfileIconSlot ui_ProfileIconSlot;

    [Header("Animations")]
    [SerializeField] protected Vector2 start;
    [SerializeField] protected Vector2 end;
    [SerializeField] protected float duration;

    protected override async UniTask OpenAnimationAsync()
    {
        background.SetActive(true);
        var t = gameObject.transform;
        t.localScale = start;
        await t.DOScale(end, duration).SetEase(Ease.OutBounce).ToUniTask();
    }

    protected override async UniTask CloseAnimationAsync()
    {
        var t = gameObject.transform;
        await t.DOScale(start, duration).SetEase(Ease.InBounce).ToUniTask();
        background.SetActive(false);
    }
}
