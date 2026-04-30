using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public abstract class UI_ProfilePanelBase : UI_Base
{
    [SerializeField] protected GameObject background => backgroundCloseButton.gameObject;

    [Header("UI")]
    [SerializeField] protected TextMeshProUGUI playerName;
    [SerializeField] protected Button closeButton;
    [SerializeField] protected Button backgroundCloseButton;

    [Header("Components")]
    [SerializeField] protected UI_PlayerProfileIconSlot ui_ProfileIconSlot;

    [Header("Animations")]
    [SerializeField] protected Vector2 start = new Vector2(.6f, .6f);
    [SerializeField] protected Vector2 end = Vector2.one;
    [SerializeField] protected float openDuration = 0.2f;
    [SerializeField] protected float closeDuration = 0.2f;
    [SerializeField] protected AnimationCurve openEase;
    [SerializeField] protected AnimationCurve closeEase;

    protected virtual void OnEnable()
    {
        closeButton.onClick.AddListener(Close);
        backgroundCloseButton.onClick.AddListener(Close);
    }

    protected virtual void OnDisable()
    {
        closeButton.onClick.RemoveListener(Close);
        backgroundCloseButton.onClick.RemoveListener(Close);
    }

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
