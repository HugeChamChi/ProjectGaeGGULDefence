using DG.Tweening;
using UnityEngine;

/// <summary>충전 완료 버튼을 실제 화면 왼쪽 밖에서 Safe Area 안으로 이동한다. 스킬 판단은 Presenter가 담당한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class ChiefSkillReadyBanner : MonoBehaviour
{
    [SerializeField] private RectTransform _bannerRect;
    [SerializeField] private RectTransform _safeAreaRoot;
    [SerializeField, Min(0f)] private float _edgeInset = 16f;
    [Tooltip("배경 이미지 왼쪽 투명 여백의 가로 비율. 보이는 그림의 끝을 Safe Area에 맞춘다.")]
    [SerializeField, Range(0f, 1f)] private float _leftTransparentPaddingRatio;
    [SerializeField, Min(0f)] private float _hiddenGap = 8f;
    [SerializeField, Min(0f)] private float _inDuration = 0.28f;
    [SerializeField, Min(0f)] private float _outDuration = 0.18f;
    private RectTransform _parent;
    private Canvas _canvas;
    private CanvasGroup _group;
    private float _authoredY;
    private float _progress;
    private bool _charged;
    private Tween _tween;
    private readonly Vector3[] _corners = new Vector3[4];

    /// <summary>충전 완료 표시 목표. 사용 가능 여부와 별개라 일시정지/스턴 시 위치가 바뀌지 않는다.</summary>
    public bool IsCharged => _charged;
    /// <summary>현재 펼침 진행도. 0은 화면 밖, 1은 Safe Area 안.</summary>
    public float RevealProgress => _progress;

    private void Awake()
    {
        if (_bannerRect == null) _bannerRect = transform as RectTransform;
        _parent = _bannerRect != null ? _bannerRect.parent as RectTransform : null;
        _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        _group = GetComponent<CanvasGroup>();
        if (_bannerRect != null) _authoredY = _bannerRect.anchoredPosition.y;
        _progress = 0f;
        ApplyLayout();
    }

    /// <summary>표시 목표가 바뀔 때만 트윈한다. 현재 위치에서 반전하며 게임 시간 정지와 독립적이다.</summary>
    public void SetCharged(bool charged, bool immediate = false)
    {
        if (_charged == charged && !immediate) return;
        _charged = charged;
        _tween?.Kill();
        _tween = null;
        if (!isActiveAndEnabled || immediate)
        {
            _progress = charged ? 1f : 0f;
            ApplyLayout();
            return;
        }
        float duration = charged ? _inDuration : _outDuration;
        _tween = DOTween.To(() => _progress, value => _progress = value, charged ? 1f : 0f, duration)
            .SetEase(charged ? Ease.OutCubic : Ease.InCubic).SetUpdate(true);
        ApplyLayout();
    }

    private void LateUpdate() => ApplyLayout();

    private Rect ScreenRectInParent(Rect pixels)
    {
        Camera camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, pixels.min, camera, out var min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, pixels.max, camera, out var max);
        return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
    }

    private void ApplyLayout()
    {
        if (_bannerRect == null || _parent == null || _canvas == null || _group == null || Screen.width <= 0 || Screen.height <= 0) return;
        var screen = ScreenRectInParent(new Rect(0f, 0f, Screen.width, Screen.height));
        var safe = ScreenRectInParent(Screen.safeArea);
        if (_safeAreaRoot != null)
        {
            _safeAreaRoot.GetWorldCorners(_corners);
            Vector2 min = _parent.InverseTransformPoint(_corners[0]);
            Vector2 max = _parent.InverseTransformPoint(_corners[2]);
            safe = Rect.MinMaxRect(Mathf.Max(safe.xMin, min.x), Mathf.Max(safe.yMin, min.y), Mathf.Min(safe.xMax, max.x), Mathf.Min(safe.yMax, max.y));
        }
        Vector2 size = Vector2.Scale(_bannerRect.rect.size, new Vector2(Mathf.Abs(_bannerRect.localScale.x), Mathf.Abs(_bannerRect.localScale.y)));
        Vector2 anchors = new Vector2(Mathf.Lerp(_bannerRect.anchorMin.x, _bannerRect.anchorMax.x, _bannerRect.pivot.x),
            Mathf.Lerp(_bannerRect.anchorMin.y, _bannerRect.anchorMax.y, _bannerRect.pivot.y));
        Vector2 anchorPoint = _parent.rect.min + Vector2.Scale(_parent.rect.size, anchors);
        CalculatePositions(screen, safe, size, _bannerRect.pivot, anchorPoint.y + _authoredY, _edgeInset, _hiddenGap, out var hidden, out var shown);
        shown.x -= size.x * _leftTransparentPaddingRatio;
        _bannerRect.anchoredPosition = Vector2.LerpUnclamped(hidden, shown, _progress) - anchorPoint;
        _group.alpha = _progress <= 0f ? 0f : 1f;
        _group.blocksRaycasts = _group.interactable = _charged && _progress >= 0.999f;
    }

    /// <summary>부모 로컬 좌표에서 양끝 위치를 계산한다. 고정1080폭 부모나 비중앙 피벗도 지원한다.</summary>
    public static void CalculatePositions(Rect screen, Rect safe, Vector2 size, Vector2 pivot, float preferredY,
        float inset, float gap, out Vector2 hidden, out Vector2 shown)
    {
        float horizontalInset = Mathf.Min(inset, Mathf.Max(0f, (safe.width - size.x) * 0.5f));
        float verticalInset = Mathf.Min(inset, Mathf.Max(0f, (safe.height - size.y) * 0.5f));
        float bottom = safe.yMin + verticalInset + size.y * pivot.y;
        float top = safe.yMax - verticalInset - size.y * (1f - pivot.y);
        float y = bottom <= top ? Mathf.Clamp(preferredY, bottom, top) : safe.center.y;
        shown = new Vector2(safe.xMin + horizontalInset + size.x * pivot.x, y);
        hidden = new Vector2(screen.xMin - gap - size.x * (1f - pivot.x), y);
    }

    private void OnDisable()
    {
        _tween?.Kill();
        _tween = null;
        _charged = false;
        _progress = 0f;
        ApplyLayout();
    }
    private void OnDestroy() => _tween?.Kill();
}
