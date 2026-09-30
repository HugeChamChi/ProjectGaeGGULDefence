using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

/// <summary>
/// 이펙트 실험실(FxLab 씬) 전용 — 연출을 재생하며 프레임을 PNG로 저장한다 (레퍼런스와 나란히 비교용).
/// 두 가지 방식:
///   고정 프레임 (_realtime = false): Time.captureFramerate로 시간을 프레임 단위로 고정한다. 저장이 느려도 타이밍이 정확하다.
///     단 captureFramerate는 scaled 시간만 고정하므로, 캡처 중에는 대상을 scaled 시간으로 돌리고
///     UnscaledShaderTime을 꺼서 셰이더도 _Time.y(scaled)를 쓰게 한다. 끝나면 되돌린다.
///   실시간 (_realtime = true): 실제 속도로 재생하고 1/frameRate 간격마다 GPU 비동기 읽기로 저장한다.
///     UniTask Realtime 대기처럼 고정할 수 없는 시간을 쓰는 연출(LevelUpRevealSequence 등)에 쓴다.
///     파일 번호 i = 시각 i/frameRate 에 가장 가까운 프레임 (실제 시각은 frames.txt).
/// 카메라 targetTexture를 잠시 빌려 쓰므로 캡처 중 게임 뷰는 비어 보인다.
/// </summary>
public class FxLabCapture : MonoBehaviour
{
    private static readonly int UnscaledTimeId = Shader.PropertyToID("_USWUnscaledTime");

    [Tooltip("IFxLabPlayable을 구현한 컴포넌트")]
    [SerializeField] private MonoBehaviour _target;
    [SerializeField] private Camera _camera;
    [SerializeField] private bool _captureOnStart = true;
    [SerializeField] private bool _realtime;
    [SerializeField] private int _frameRate = 30;
    [SerializeField] private float _duration = 1.2f;
    [SerializeField] private Vector2Int _resolution = new Vector2Int(540, 1168);
    [Tooltip("프로젝트 루트 기준 저장 폴더")]
    [SerializeField] private string _outputFolder = "Temp/FxCapture";

    private IFxLabPlayable Target => _target as IFxLabPlayable;

    private void Start()
    {
        if (_captureOnStart) Capture();
    }

    /// <summary>재생 + 프레임 저장을 시작한다.</summary>
    [ContextMenu("Capture")]
    public void Capture()
    {
        var token = this.GetCancellationTokenOnDestroy();
        if (_realtime) CaptureRealtimeAsync(token).Forget();
        else CaptureFixedAsync(token).Forget();
    }

    private string PrepareFolder()
    {
        string dir = Path.Combine(Directory.GetCurrentDirectory(), _outputFolder);
        if (Directory.Exists(dir)) foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private async UniTaskVoid CaptureFixedAsync(CancellationToken token)
    {
        var target = Target;
        if (target == null || _camera == null) { Debug.LogError("[FxLabCapture] 대상(IFxLabPlayable)/카메라 없음"); return; }
        string dir = PrepareFolder();

        var rt = new RenderTexture(_resolution.x, _resolution.y, 24, RenderTextureFormat.ARGB32);
        var tex = new Texture2D(_resolution.x, _resolution.y, TextureFormat.RGB24, false);
        var prevTarget = _camera.targetTexture;
        int prevCapture = Time.captureFramerate;
        bool prevUnscaled = target.UseUnscaledTime;
        var shaderTimes = FindObjectsByType<UnscaledShaderTime>(FindObjectsSortMode.None);
        try
        {
            _camera.targetTexture = rt;
            Time.captureFramerate = _frameRate;
            target.UseUnscaledTime = false;
            foreach (var st in shaderTimes) st.enabled = false;
            Shader.SetGlobalFloat(UnscaledTimeId, 0f);
            await UniTask.DelayFrame(3, cancellationToken: token);

            target.Play();
            int frames = Mathf.CeilToInt(_duration * _frameRate);
            float startTime = Time.time;
            for (int i = 0; i <= frames; i++)
            {
                await UniTask.WaitForEndOfFrame(this, token);
                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply(false);
                RenderTexture.active = prevActive;
                File.WriteAllBytes(Path.Combine(dir, $"f_{i:D3}.png"), tex.EncodeToPNG());
                if (i == 1) Debug.Log($"[FxLabCapture] deltaTime={Time.deltaTime:F4} (기대 {1f / _frameRate:F4})");
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            Debug.Log($"[FxLabCapture] 완료: {frames + 1}프레임 → {dir} (경과 scaled {Time.time - startTime:F3}s)");
        }
        finally
        {
            Time.captureFramerate = prevCapture;
            target.UseUnscaledTime = prevUnscaled;
            foreach (var st in shaderTimes) if (st != null) st.enabled = true;
            if (_camera != null) _camera.targetTexture = prevTarget;
            rt.Release();
            Destroy(rt);
            Destroy(tex);
        }
    }

    private async UniTaskVoid CaptureRealtimeAsync(CancellationToken token)
    {
        var target = Target;
        if (target == null || _camera == null) { Debug.LogError("[FxLabCapture] 대상(IFxLabPlayable)/카메라 없음"); return; }
        string dir = PrepareFolder();

        int w = _resolution.x, h = _resolution.y;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        var prevTarget = _camera.targetTexture;
        var frames = new List<(int Index, float Time, byte[] Pixels)>();
        int pending = 0;
        try
        {
            _camera.targetTexture = rt;
            await UniTask.DelayFrame(3, cancellationToken: token);

            float step = 1f / _frameRate;
            int last = Mathf.CeilToInt(_duration * _frameRate);
            float start = Time.realtimeSinceStartup;
            target.Play();
            int next = 0;
            while (next <= last)
            {
                await UniTask.WaitForEndOfFrame(this, token);
                float elapsed = Time.realtimeSinceStartup - start;
                if (elapsed + step * 0.5f >= next * step)
                {
                    int index = next;
                    pending++;
                    _ = AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, req =>
                    {
                        pending--;
                        if (!req.hasError) frames.Add((index, elapsed, req.GetData<byte>().ToArray()));
                    });
                    // 프레임이 밀려 여러 칸을 건너뛰었으면 같은 프레임으로 채우지 않고 다음 칸으로 넘어간다.
                    next = Mathf.Max(next + 1, Mathf.FloorToInt(elapsed / step + 0.5f) + 1);
                }
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            while (pending > 0) await UniTask.Yield(token);

            // 번호가 비지 않게 건너뛴 칸은 직전 프레임으로 채운다 (frames.txt에 표시).
            frames.Sort((a, b) => a.Index.CompareTo(b.Index));
            var log = new System.Text.StringBuilder();
            byte[] lastPng = null;
            float lastTime = 0f;
            int cursor = 0, filled = 0;
            for (int i = 0; i <= last; i++)
            {
                if (cursor < frames.Count && frames[cursor].Index == i)
                {
                    var f = frames[cursor++];
                    lastPng = ImageConversion.EncodeArrayToPNG(f.Pixels, GraphicsFormat.R8G8B8A8_UNorm, (uint)w, (uint)h);
                    lastTime = f.Time;
                    log.AppendLine($"{i}\t{f.Time:F3}");
                }
                else if (lastPng != null)
                {
                    filled++;
                    log.AppendLine($"{i}\t{lastTime:F3}\t(직전 프레임 반복)");
                }
                if (lastPng != null) File.WriteAllBytes(Path.Combine(dir, $"f_{i:D3}.png"), lastPng);
            }
            File.WriteAllText(Path.Combine(dir, "frames.txt"), log.ToString());
            Debug.Log($"[FxLabCapture] 실시간 완료: {last + 1}칸 (건너뛴 칸 {filled}) → {dir}");
        }
        finally
        {
            if (_camera != null) _camera.targetTexture = prevTarget;
            rt.Release();
            Destroy(rt);
        }
    }
}
