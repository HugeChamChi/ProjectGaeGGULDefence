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
    [SerializeField] protected Vector2 start = new Vector2(.6f, .6f);
    [SerializeField] protected Vector2 end = Vector2.one;
    [SerializeField] protected float openDuration = 0.2f;
    [SerializeField] protected float closeDuration = 0.2f;
    [SerializeField] protected AnimationCurve openEase;
    [SerializeField] protected AnimationCurve closeEase;

    protected override async UniTask OpenAnimationAsync()
    {
        background.SetActive(true);
        var t = gameObject.transform;
        t.localScale = start;
        await t.DOScale(end, openDuration).SetEase(openEase).ToUniTask();
    }

    protected override async UniTask CloseAnimationAsync()
    {
        var t = gameObject.transform;
        await t.DOScale(start, closeDuration).SetEase(closeEase).ToUniTask();
        background.SetActive(false);
    }
}
