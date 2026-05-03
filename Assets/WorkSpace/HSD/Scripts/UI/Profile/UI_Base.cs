using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class UI_Base : MonoBehaviour
{
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
    }

    public virtual async UniTask CloseAsync()
    {
        await CloseAnimationAsync();
        gameObject.SetActive(false);
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
    
