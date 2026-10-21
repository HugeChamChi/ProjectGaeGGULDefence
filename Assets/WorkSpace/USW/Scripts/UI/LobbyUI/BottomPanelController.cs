using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using DG.Tweening;

/// <summary>하단 메뉴와 방향성 있는 로비 페이지 전환을 함께 재생한다.</summary>
public class BottomPanelController : MonoBehaviour
{
    [System.Serializable]
    public class PanelButton
    {
        public Button _button;
        public RectTransform _mainRect;
        public Image _background;
        public Image _light;
        public Image _innerShadow;
        public GameObject _panel;
        public Image _buttonImage;
        public Sprite _activeSprite;
        public Sprite _inactiveSprite;
        public Image _selectionFill;
        public CanvasGroup _label;
        [System.NonSerialized] internal RectTransform Rect;
        [System.NonSerialized] internal CanvasGroup Group;
        [System.NonSerialized] internal Vector2 Origin;
        [System.NonSerialized] internal Vector2 IconOrigin;
        [System.NonSerialized] internal Vector3 IconScale;
        [System.NonSerialized] internal UnityAction Click;
        [System.NonSerialized] internal LobbyLockedTab Lock;
    }

    [SerializeField] private float _slideSeconds = 0.32f;
    /// <summary>왼쪽에서 오른쪽으로 정렬된 페이지와 버튼.</summary>
    public PanelButton[] panelButtons = new PanelButton[5];
    private int _currentIndex = 2;
    private int _pendingIndex = -1;
    private Sequence _transition;
    private bool _initialized;
    private LobbyBottomMotion _bottomMotion;
    /// <summary>현재 선택한 페이지 번호.</summary>
    public int CurrentIndex => _currentIndex;
    /// <summary>화면 이동 진행 여부.</summary>
    public bool IsTransitioning => _transition != null && _transition.IsActive();

    private void Start()
    {
        for (int i = 0; i < panelButtons.Length; i++)
        {
            var item = panelButtons[i];
            if (item._button == null || item._panel == null || !(item._panel.transform is RectTransform))
            { Debug.LogError("로비 탭 참조가 연결되지 않았습니다.", this); enabled = false; return; }
            item.Rect = (RectTransform)item._panel.transform;
            item.Origin = item.Rect.anchoredPosition;
            item.Group = item._panel.GetComponent<CanvasGroup>();
            if (item.Group == null) item.Group = item._panel.AddComponent<CanvasGroup>();
            if (item._buttonImage != null)
            {
                item.IconOrigin = item._buttonImage.rectTransform.anchoredPosition;
                item.IconScale = item._buttonImage.rectTransform.localScale;
            }
            item.Lock = item._button.GetComponent<LobbyLockedTab>();
            int index = i;
            item.Click = () => SelectButton(index);
            item._button.onClick.AddListener(item.Click);
        }
        _bottomMotion = new LobbyBottomMotion(panelButtons, _currentIndex);
        _initialized = true;
        Settle();
    }

    /// <summary>클릭한 탭 방향으로 페이지를 이동한다. 연속 입력은 마지막 요청으로 이어진다.</summary>
    public void SelectButton(int index)
    {
        if (!_initialized || !isActiveAndEnabled || index < 0 || index >= panelButtons.Length) return;
        var locked = panelButtons[index].Lock;
        if (locked != null && locked.IsLocked) { locked.PlayDenied(); return; }
        _bottomMotion.Select(index);
        if (IsTransitioning) { _pendingIndex = index; return; }
        if (index == _currentIndex) return;
        var previous = panelButtons[_currentIndex];
        var next = panelButtons[index];
        float direction = index > _currentIndex ? 1f : -1f;
        float width = ((RectTransform)next.Rect.parent).rect.width;
        previous.Group.blocksRaycasts = previous.Group.interactable = false;
        next.Group.blocksRaycasts = next.Group.interactable = false;
        next.Rect.anchoredPosition = next.Origin + Vector2.right * (direction * width);
        next.Group.alpha = 1f;
        next._panel.SetActive(true);
        _currentIndex = index;
        _transition = DOTween.Sequence().SetUpdate(true);
        _transition.Join(previous.Rect.DOAnchorPos(previous.Origin - Vector2.right * (direction * width), _slideSeconds).SetEase(Ease.OutCubic));
        _transition.Join(next.Rect.DOAnchorPos(next.Origin, _slideSeconds).SetEase(Ease.OutCubic));
        _transition.OnComplete(() =>
        {
            _transition = null;
            previous._panel.SetActive(false);
            previous.Rect.anchoredPosition = previous.Origin;
            next.Group.blocksRaycasts = next.Group.interactable = true;
            int pending = _pendingIndex;
            _pendingIndex = -1;
            if (pending >= 0) SelectButton(pending);
        });
    }

    private void Settle()
    {
        _bottomMotion?.Settle(_currentIndex);
        for (int i = 0; i < panelButtons.Length; i++)
        {
            var item = panelButtons[i];
            bool selected = i == _currentIndex;
            item.Rect.anchoredPosition = item.Origin;
            item.Group.alpha = 1f;
            item.Group.blocksRaycasts = item.Group.interactable = selected;
            item._panel.SetActive(selected);
        }
    }

    private void OnDisable()
    {
        _transition?.Kill();
        _transition = null;
        _pendingIndex = -1;
        if (_initialized) Settle();
    }

    private void OnDestroy()
    {
        _bottomMotion?.Dispose();
        foreach (var item in panelButtons)
        {
            if (item._button != null && item.Click != null) item._button.onClick.RemoveListener(item.Click);
        }
    }
}
