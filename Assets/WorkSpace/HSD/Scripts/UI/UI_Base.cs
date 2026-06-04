using System;
using VContainer;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

public abstract class UI_Base : MonoBehaviour
{
    protected static AudioManager _audioManager;

    public static void Inject(AudioManager audioManager)
    {
        _audioManager = audioManager;
    }

    public Action OnClosed;
    public Action OnOpened;

    [SerializeField] private string openSoundName = "01.Popup";

    [Header("Common UI (Optional)")]
    [SerializeField] protected Button btn_Close;
    [SerializeField] protected Button btn_BackgroundClose;

    protected Canvas _canvas;
    [Header("Animation")]
    [SerializeField] protected GaeGGUL.Animation.Anim_InOutBase targetAnim;

    protected virtual void Awake()
    {
        _canvas = GetComponent<Canvas>();
        
        // 인스펙터에서 할당하지 않았다면 현재 오브젝트에서 검색 (하위 호환성)
        if (targetAnim == null)
            targetAnim = GetComponent<GaeGGUL.Animation.Anim_InOutBase>();

        // 하위 호환성 유지: 기존에 설정된 버튼이 있다면 자동으로 바인딩
        if (btn_Close != null || btn_BackgroundClose != null)
        {
            BindCloseButton(btn_Close, btn_BackgroundClose);
        }
    }

#if ODIN_INSPECTOR
    [Button]
#else
    [ContextMenu("Open")]
#endif
    public virtual void Open()
    {
        if (_audioManager != null)
        {
            _audioManager.PlaySFX(openSoundName);
        }
        OpenAsync().Forget();
    }

#if ODIN_INSPECTOR
    [Button]
#else
    [ContextMenu("Close")]
#endif
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

    protected virtual async UniTask OpenAnimationAsync()
    {
        if (btn_BackgroundClose != null) btn_BackgroundClose.gameObject.SetActive(true);

        if (targetAnim != null)
        {
            await targetAnim.PlayIn();
        }
        else
        {
            await UniTask.CompletedTask;
        }
    }

    protected virtual async UniTask CloseAnimationAsync()
    {
        if (targetAnim != null)
        {
            await targetAnim.PlayOut();
        }
        else
        {
            await UniTask.CompletedTask;
        }

        if (btn_BackgroundClose != null) btn_BackgroundClose.gameObject.SetActive(false);
    }
}
