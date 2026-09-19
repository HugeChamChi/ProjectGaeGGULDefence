using UnityEngine;
using UnityEngine.Rendering;

/// <summary>렌더링 중에만 카메라를 흔들어 터치 좌표 계산의 카메라 기준점을 유지한다.</summary>
[RequireComponent(typeof(Camera))]
public sealed class BattleCameraShake : MonoBehaviour
{
    private Camera _camera;
    private Vector3 _position;
    private Vector3 _offset;
    private bool _applied;
    private float _remaining, _duration, _intensity, _phase;
    /// <summary>게임 시간 기준 흔들림을 시작한다. 강한 기존 흔들림을 약한 요청으로 덮지 않는다.</summary>
    public void Play(float seconds, float intensity)
    {
        _duration = _remaining = Mathf.Max(_remaining, seconds);
        _intensity = Mathf.Max(_intensity, intensity);
    }
    private void Awake() => _camera = GetComponent<Camera>();
    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginRender;
        RenderPipelineManager.endCameraRendering += EndRender;
        Camera.onPreCull += BeginCamera;
        Camera.onPostRender += EndCamera;
    }
    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (_remaining <= 0f) { _offset = Vector3.zero; _intensity = 0f; return; }
        _remaining = Mathf.Max(0f, _remaining - Time.deltaTime);
        _phase += Time.deltaTime;
        float fade = _duration > 0f ? _remaining / _duration : 0f;
        _offset = new Vector3(Mathf.Sin(_phase * 73f), Mathf.Sin(_phase * 91f), 0f) * (_intensity * fade);
    }
    private void BeginRender(ScriptableRenderContext context, Camera camera) => BeginCamera(camera);
    private void EndRender(ScriptableRenderContext context, Camera camera) => EndCamera(camera);
    private void BeginCamera(Camera camera)
    {
        if (camera != _camera || _applied || _remaining <= 0f) return;
        _position = transform.position;
        transform.position += _offset;
        _applied = true;
    }
    private void EndCamera(Camera camera)
    {
        if (camera != _camera || !_applied) return;
        transform.position = _position;
        _applied = false;
    }
    /// <summary>전투 종료 시 흔들림과 임시 카메라 이동을 정리한다.</summary>
    public void Stop()
    {
        EndCamera(_camera);
        _remaining = _intensity = 0f;
        _offset = Vector3.zero;
    }
    private void OnDisable()
    {
        Stop();
        RenderPipelineManager.beginCameraRendering -= BeginRender;
        RenderPipelineManager.endCameraRendering -= EndRender;
        Camera.onPreCull -= BeginCamera;
        Camera.onPostRender -= EndCamera;
    }
}
