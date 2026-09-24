using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>
/// 드래그 판매 띠. 유닛/토템을 이동 드래그하면 하단 버튼(족장스킬·출격·강화)은 아래로, 사이드 탭(햄버거·가방)은 오른쪽으로
/// 텐션감 있게 빠지고, 그 자리에 반투명 검은 띠 + 휴지통이 올라온다. 띠 위에서 놓으면 DragSellService가 판매한다.
/// 모든 트윈은 unscaled 시간 — 토템 홀드 슬로우 중에도 같은 속도로 움직인다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class DragSellBarUI : MonoBehaviour
{
    [Inject] private DragSellService _service;

    [Header("밀려날 버튼")]
    [SerializeField] private RectTransform[] _slideDownTargets;
    [SerializeField, Min(0f)] private float _slideDownDistance = 460f;
    [Tooltip("버튼마다 시작 시차(초). 0이면 한꺼번에")]
    [SerializeField, Min(0f)] private float _slideDownStagger = 0f;
    [SerializeField] private RectTransform[] _slideRightTargets;
    [SerializeField, Min(0f)] private float _slideRightDistance = 220f;

    [Header("판매 띠")]
    [SerializeField] private RectTransform _zoneRect;
    [SerializeField] private Image _zoneImage;
    [SerializeField] private RectTransform _trashIcon;
    [SerializeField] private Image _trashImage;
    [SerializeField] private Color _zoneColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color _zoneHoverColor = new Color(0f, 0f, 0f, 0.78f);
    [SerializeField] private Color _trashColor = Color.white;
    [SerializeField] private Color _trashHoverColor = new Color(1f, 0.36f, 0.3f, 1f);
    [SerializeField, Min(1f)] private float _trashHoverScale = 1.25f;
    [Tooltip("띠가 나타날 때 아래에서 올라오는 거리")]
    [SerializeField, Min(0f)] private float _zoneRise = 60f;

    [Header("타이밍")]
    [SerializeField, Min(0f)] private float _hideDuration = 0.3f;
    [SerializeField, Min(0f)] private float _showDuration = 0.4f;
    [SerializeField, Min(0f)] private float _zoneFadeDuration = 0.16f;
    [SerializeField, Min(0f)] private float _hoverDuration = 0.12f;

    private CanvasGroup _group;
    private Vector2[] _downHome;
    private Vector2[] _rightHome;
    private Vector2 _zoneHome;
    private Sequence _sequence;
    private Tween _hoverTween;

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        _downHome = Capture(_slideDownTargets);
        _rightHome = Capture(_slideRightTargets);
        if (_zoneRect != null) _zoneHome = _zoneRect.anchoredPosition;
        if (_zoneImage != null) { _zoneImage.color = _zoneColor; _zoneImage.raycastTarget = false; }
        if (_trashImage != null) { _trashImage.color = _trashColor; _trashImage.raycastTarget = false; }
    }

    private void OnEnable()
    {
        if (_service == null) return;
        _service.RegisterZone(this);
        _service.OnMoveDragChanged += HandleMoveDrag;
        _service.OnHoverChanged += HandleHover;
    }

    private void Start()
    {
        // 씬 자동 주입은 OnEnable 이후라 첫 활성화에서는 여기서 연결한다.
        if (_service == null) { Debug.LogError("[DragSellBarUI] DragSellService 미주입 — LifetimeScope autoInject 확인", this); return; }
        _service.RegisterZone(this);
        _service.OnMoveDragChanged -= HandleMoveDrag;
        _service.OnMoveDragChanged += HandleMoveDrag;
        _service.OnHoverChanged -= HandleHover;
        _service.OnHoverChanged += HandleHover;
    }

    private void OnDisable()
    {
        if (_service != null)
        {
            _service.OnMoveDragChanged -= HandleMoveDrag;
            _service.OnHoverChanged -= HandleHover;
            _service.UnregisterZone(this);
        }
        _sequence?.Kill();
        _hoverTween?.Kill();
        Restore(_slideDownTargets, _downHome);
        Restore(_slideRightTargets, _rightHome);
        if (_group != null) _group.alpha = 0f;
    }

    private readonly Vector3[] _corners = new Vector3[4];

    /// <summary>드래그 중 손가락(월드 좌표)이 판매 띠 안에 있는지.</summary>
    public bool ContainsWorldPoint(Vector2 worldPosition)
    {
        // 표시 트윈 진행 여부와 무관하게 판정한다 — 빨리 놓아도, 놓는 순간 숨김이 시작돼도 같은 결과.
        if (_zoneRect == null) return false;
        var cam = Camera.main;
        if (cam == null) return false;
        Vector2 screen = cam.WorldToScreenPoint(worldPosition);
        var canvas = _zoneRect.GetComponentInParent<Canvas>()?.rootCanvas;
        Camera uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        // 띠는 화면 맨 아래까지이므로 띠 윗변보다 아래면 판매로 본다 (좌우·아래 끝 여유).
        _zoneRect.GetWorldCorners(_corners);
        Vector2 top = RectTransformUtility.WorldToScreenPoint(uiCam, _corners[1]);
        return screen.y <= top.y;
    }

    private void HandleMoveDrag(bool moving)
    {
        _sequence?.Kill();
        _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        if (moving)
        {
            for (int i = 0; i < _slideDownTargets.Length; i++)
            {
                var t = _slideDownTargets[i];
                if (t == null) continue;
                _sequence.Insert(i * _slideDownStagger,
                    t.DOAnchorPos(_downHome[i] + Vector2.down * _slideDownDistance, _hideDuration).SetEase(Ease.InBack));
            }
            for (int i = 0; i < _slideRightTargets.Length; i++)
            {
                var t = _slideRightTargets[i];
                if (t == null) continue;
                _sequence.Insert(i * _slideDownStagger,
                    t.DOAnchorPos(_rightHome[i] + Vector2.right * _slideRightDistance, _hideDuration).SetEase(Ease.InBack));
            }
            if (_zoneRect != null)
            {
                _zoneRect.anchoredPosition = _zoneHome + Vector2.down * _zoneRise;
                _sequence.Insert(_hideDuration * 0.5f, _zoneRect.DOAnchorPos(_zoneHome, _showDuration).SetEase(Ease.OutBack));
            }
            _sequence.Insert(_hideDuration * 0.5f, _group.DOFade(1f, _zoneFadeDuration));
        }
        else
        {
            _sequence.Insert(0f, _group.DOFade(0f, _zoneFadeDuration));
            if (_zoneRect != null)
                _sequence.Insert(0f, _zoneRect.DOAnchorPos(_zoneHome + Vector2.down * _zoneRise, _zoneFadeDuration).SetEase(Ease.InQuad));
            for (int i = 0; i < _slideDownTargets.Length; i++)
            {
                var t = _slideDownTargets[i];
                if (t == null) continue;
                _sequence.Insert(_zoneFadeDuration * 0.5f + i * _slideDownStagger,
                    t.DOAnchorPos(_downHome[i], _showDuration).SetEase(Ease.OutBack));
            }
            for (int i = 0; i < _slideRightTargets.Length; i++)
            {
                var t = _slideRightTargets[i];
                if (t == null) continue;
                _sequence.Insert(_zoneFadeDuration * 0.5f + i * _slideDownStagger,
                    t.DOAnchorPos(_rightHome[i], _showDuration).SetEase(Ease.OutBack));
            }
        }
    }

    private void HandleHover(bool hover)
    {
        _hoverTween?.Kill();
        var seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        if (_zoneImage != null) seq.Join(_zoneImage.DOColor(hover ? _zoneHoverColor : _zoneColor, _hoverDuration));
        if (_trashImage != null) seq.Join(_trashImage.DOColor(hover ? _trashHoverColor : _trashColor, _hoverDuration));
        if (_trashIcon != null)
            seq.Join(_trashIcon.DOScale(hover ? _trashHoverScale : 1f, _hoverDuration * 1.5f).SetEase(hover ? Ease.OutBack : Ease.OutQuad));
        _hoverTween = seq;
    }

    private static Vector2[] Capture(RectTransform[] targets)
    {
        var result = new Vector2[targets != null ? targets.Length : 0];
        for (int i = 0; i < result.Length; i++)
            if (targets[i] != null) result[i] = targets[i].anchoredPosition;
        return result;
    }

    private static void Restore(RectTransform[] targets, Vector2[] home)
    {
        if (targets == null || home == null) return;
        for (int i = 0; i < targets.Length && i < home.Length; i++)
        {
            if (targets[i] == null) continue;
            targets[i].DOKill();
            targets[i].anchoredPosition = home[i];
        }
    }
}
