using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
public class SpriteFrame : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 4f;
    private CancellationTokenSource _cts;
    private void Awake()
    {
        // 직접 할당 안 했으면 자동으로 가져옴
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }
    private void OnEnable() => Play();
    private void OnDisable() => Stop();
    public void Play()
    {
        Stop();
        if (frames == null || frames.Length == 0) return;
        if (spriteRenderer == null) return;
        _cts = new CancellationTokenSource();
        LoopAsync(_cts.Token).Forget();
    }
    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
    private async UniTaskVoid LoopAsync(CancellationToken ct)
    {
        int index = 0;
        int delay = Mathf.RoundToInt(1000f / fps);
        while (!ct.IsCancellationRequested)
        {
            spriteRenderer.sprite = frames[index];
            index = (index + 1) % frames.Length;
            await UniTask.Delay(delay, cancellationToken: ct);
        }
    }
}