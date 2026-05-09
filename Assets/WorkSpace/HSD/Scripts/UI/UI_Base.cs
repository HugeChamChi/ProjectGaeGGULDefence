using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public abstract class UI_Base : MonoBehaviour
{
    public Action OnClosed;
    public Action OnOpened;

    [Header("Common UI")]
    [SerializeField] protected Button btn_Close;
    [SerializeField] protected Button btn_BackgroundClose;

    protected virtual void Awake()
    {
        // 공통 닫기 버튼 자동 바인딩
        if (btn_Close != null) btn_Close.onClick.AddListener(Close);
        if (btn_BackgroundClose != null) btn_BackgroundClose.onClick.AddListener(Close);
    }

    [Button]
    public virtual void Open()
    {
        OpenAsync().Forget();
    }

    [Button]
    public virtual void Close()
    {
        CloseAsync().Forget();
    }

    public virtual async UniTask OpenAsync()
    {
        gameObject.SetActive(true);
        await OpenAnimationAsync();
        OnOpened?.Invoke();
    }

    public virtual async UniTask CloseAsync()
    {
        await CloseAnimationAsync();
        gameObject.SetActive(false);
        OnClosed?.Invoke();
    }

    protected virtual UniTask OpenAnimationAsync()
    {
        return UniTask.CompletedTask;
    }

    protected virtual UniTask CloseAnimationAsync()
    {
        return UniTask.CompletedTask;
    }
}
    
