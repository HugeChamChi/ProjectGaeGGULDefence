using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public abstract class UI_Base : MonoBehaviour
{
    public Action OnClosed;
    public Action OnOpened;

    [Header("Common UI (Optional)")]
    [SerializeField] protected Button btn_Close;
    [SerializeField] protected Button btn_BackgroundClose;

    protected Canvas _canvas;

    protected virtual void Awake()
    {
        _canvas = GetComponent<Canvas>();

        // 하위 호환성 유지: 기존에 설정된 버튼이 있다면 자동으로 바인딩
        if (btn_Close != null || btn_BackgroundClose != null)
        {
            BindCloseButton(btn_Close, btn_BackgroundClose);
        }
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
        if (_canvas != null)
            _canvas.enabled = true;
        else
            gameObject.SetActive(true);

        await OpenAnimationAsync();
        OnOpened?.Invoke();
    }

    public virtual async UniTask CloseAsync()
    {
        await CloseAnimationAsync();

        if (_canvas != null)
            _canvas.enabled = false;
        else
            gameObject.SetActive(false);

        OnClosed?.Invoke();
    }

    /// <summary>
    /// 전달된 버튼들을 닫기 기능에 연결합니다.
    /// </summary>
    protected void BindCloseButton(params Button[] buttons)
    {
        foreach (var btn in buttons)
        {
            if (btn != null)
            {
                // 중복 리스너 방지를 위해 Clear 후 등록
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => CloseAsync().Forget());
            }
        }
    }

    protected virtual UniTask OpenAnimationAsync() => UniTask.CompletedTask;
    protected virtual UniTask CloseAnimationAsync() => UniTask.CompletedTask;
}
