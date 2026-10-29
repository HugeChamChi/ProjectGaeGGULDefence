using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 대상별 버프·디버프 상태 팝업 묶음(StatusPopupBundle)을 띄운다 (사용자 확정 A안, 2026-10-22).
/// 같은 대상에 이어서 걸린 효과는 진행 중인 묶음에 한 줄로 붙고, 그 묶음이 사라지는 중이면 새 묶음을 연다.
/// 묶음은 풀에서 재사용하고, 매 프레임 대상의 몸통 위치를 따라간다(대상이 사라지면 마지막 위치에 남는다).
/// 컨테이너는 Screen Space 캔버스(화면 좌표로 배치)나 World Space 캔버스(월드 좌표에 두고, 메인 카메라 기준으로
/// 화면에서 ReferenceWidth 기준 캔버스와 같은 크기로 보이게 배율 보정) 아래 RectTransform이다.
/// 인게임에서는 StatusPopupService가 코드로 만들어 Configure로 컨테이너·스타일을 넘긴다.
/// </summary>
public class StatusPopupSpawner : MonoBehaviour
{
    // 스타일 수치(글자 크기 등)를 저작한 기준 화면 폭(캔버스 단위). 실험실 캔버스 1080 기준.
    private const float ReferenceWidth = 1080f;
    // 원근 카메라 등 화면 크기를 구할 수 없을 때의 월드 배율.
    private const float FallbackWorldScale = 0.01f;

    [SerializeField] private RectTransform _container;
    [SerializeField] private StatusPopupStyle _style;
    [Tooltip("대상 피벗에서 몸통 중심까지의 월드 오프셋")]
    [SerializeField] private Vector3 _bodyOffset;

    private readonly List<Entry> _active = new List<Entry>();
    private readonly Stack<StatusPopupBundle> _pool = new Stack<StatusPopupBundle>();
    private Camera _containerCamera;
    private Camera _mainCamera;
    private bool _worldSpace;

    private class Entry
    {
        public Transform Target;
        public Vector3 BodyOffset;
        public float Scale;
        public Camera SourceCamera;
        public Vector3 LastWorld;
        public StatusPopupBundle Bundle;
    }

    private void Awake() => RefreshCameras();

    /// <summary>코드로 만든 스포너에 컨테이너와 스타일을 지정한다.</summary>
    public void Configure(RectTransform container, StatusPopupStyle style)
    {
        _container = container;
        _style = style;
        RefreshCameras();
    }

    private void RefreshCameras()
    {
        _mainCamera = Camera.main;
        var canvas = _container != null ? _container.GetComponentInParent<Canvas>() : null;
        _containerCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        _worldSpace = canvas != null && canvas.renderMode == RenderMode.WorldSpace;
    }

    /// <summary>
    /// 대상의 몸통 위에 "문구 ▲/▼" 한 줄을 띄운다. 짧은 시간 안에 같은 대상에 다시 부르면 같은 묶음에 줄이 쌓인다.
    /// </summary>
    /// <param name="target">효과가 걸린 유닛(월드 Transform 또는 UI RectTransform)</param>
    /// <param name="text">표시 문구 (예: "공격속도")</param>
    /// <param name="isBuff">true = 버프(▲), false = 디버프(▼)</param>
    public void Show(Transform target, string text, bool isBuff) => Show(target, text, isBuff, null, 1f, _bodyOffset);

    /// <summary>
    /// 색·크기·몸통 위치를 정해 띄운다. 새 묶음을 열 때만 scale·bodyOffset이 쓰이고, 진행 중인 묶음에는 줄만 붙는다.
    /// </summary>
    /// <param name="arrowUp">true = ▲, false = ▼</param>
    /// <param name="color">글자·화살표 색. null이면 스타일의 버프/디버프 색</param>
    /// <param name="scale">묶음 배율 (대상 크기에 맞춤)</param>
    /// <param name="bodyOffset">대상 피벗에서 몸통 중심까지의 월드 오프셋</param>
    public void Show(Transform target, string text, bool arrowUp, Color? color, float scale, Vector3 bodyOffset)
    {
        if (target == null || _style == null || _container == null || string.IsNullOrEmpty(text)) return;

        var entry = FindLive(target);
        if (entry == null || !entry.Bundle.Add(text, arrowUp, color))
        {
            entry = Open(target, scale, bodyOffset);
            entry.Bundle.Add(text, arrowUp, color);
        }
        Place(entry);
    }

    private Entry FindLive(Transform target)
    {
        for (int i = 0; i < _active.Count; i++)
            if (_active[i].Target == target && !_active[i].Bundle.IsExiting) return _active[i];
        return null;
    }

    private Entry Open(Transform target, float scale, Vector3 bodyOffset)
    {
        StatusPopupBundle bundle;
        if (_pool.Count > 0)
        {
            bundle = _pool.Pop();
        }
        else
        {
            var go = new GameObject("StatusPopup", typeof(RectTransform));
            go.transform.SetParent(_container, false);
            bundle = go.AddComponent<StatusPopupBundle>();
            bundle.OnFinished += HandleFinished;
        }

        bundle.transform.SetAsLastSibling();
        bundle.Begin(_style);
        bundle.transform.localScale = Vector3.one * scale;
        var entry = new Entry
        {
            Target = target,
            BodyOffset = bodyOffset,
            Scale = scale,
            SourceCamera = SourceCameraOf(target),
            LastWorld = target.position + bodyOffset,
            Bundle = bundle,
        };
        _active.Add(entry);
        return entry;
    }

    private void HandleFinished(StatusPopupBundle bundle)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
            if (_active[i].Bundle == bundle) _active.RemoveAt(i);
        _pool.Push(bundle);
    }

    private void LateUpdate()
    {
        for (int i = 0; i < _active.Count; i++) Place(_active[i]);
    }

    private void Place(Entry entry)
    {
        if (entry.Target != null) entry.LastWorld = entry.Target.position + entry.BodyOffset;
        if (_worldSpace)
        {
            var bundle = entry.Bundle.transform;
            bundle.position = new Vector3(entry.LastWorld.x, entry.LastWorld.y, _container.position.z);
            bundle.localScale = Vector3.one * (entry.Scale * WorldScale());
            return;
        }
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(entry.SourceCamera, entry.LastWorld);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_container, screen, _containerCamera, out var local)) return;
        entry.Bundle.transform.localPosition = new Vector3(local.x, local.y, 0f);
    }

    // 월드 캔버스 1단위가 화면에서 ReferenceWidth 기준 캔버스 1단위와 같은 크기가 되는 배율.
    private float WorldScale()
    {
        if (_mainCamera == null) _mainCamera = Camera.main;
        float lossy = _container.lossyScale.x;
        if (_mainCamera == null || !_mainCamera.orthographic || Screen.height <= 0 || lossy <= 0f) return FallbackWorldScale;
        float worldPerPixel = _mainCamera.orthographicSize * 2f / Screen.height;
        float pixelsPerReferenceUnit = Screen.width / ReferenceWidth;
        return worldPerPixel * pixelsPerReferenceUnit / lossy;
    }

    // UI 대상은 그 캔버스의 카메라(Overlay면 null), 월드 대상은 메인 카메라로 화면 좌표를 구한다.
    private Camera SourceCameraOf(Transform target)
    {
        if (target is RectTransform)
        {
            var canvas = target.GetComponentInParent<Canvas>();
            if (canvas != null)
                return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }
        return _mainCamera;
    }
}
