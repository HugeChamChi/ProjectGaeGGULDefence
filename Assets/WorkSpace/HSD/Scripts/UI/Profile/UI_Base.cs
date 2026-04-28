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
        await OpenAnimationAsync();
        Open();
    }

    public virtual async UniTask CloseAsync()
    {
        await CloseAnimationAsync();
        Close();
    }

    protected virtual async UniTask OpenAnimationAsync()
    {

    }

    protected virtual async UniTask CloseAnimationAsync()
    {

    }
}
    
