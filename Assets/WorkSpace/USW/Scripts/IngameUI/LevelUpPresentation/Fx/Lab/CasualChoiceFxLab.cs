using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>실제 선택지 프리팹으로 다섯 가지 캐주얼 등장 연출을 비교하는 독립 실험실.</summary>
public sealed class CasualChoiceFxLab : MonoBehaviour
{
    [SerializeField] private ResearchViewSettings _art;
    [SerializeField] private LevelUpCardUI _cardPrefab;
    [SerializeField] private LevelUpData[] _samples;
    [SerializeField] private Material _scrollingBackground;
    private Material _backgroundInstance;
    private RectTransform _backgroundRect;
    private Vector2 _backgroundSize;
    private readonly List<RectTransform> _cards = new List<RectTransform>();
    private readonly List<CanvasGroup> _groups = new List<CanvasGroup>();
    private readonly List<GameObject> _particles = new List<GameObject>();
    private readonly List<Image> _tabs = new List<Image>();
    private readonly Vector2[] _positions = { new Vector2(0, 320), Vector2.zero, new Vector2(0, -320) };
    private readonly string[] _names = { "01  말랑 팝업", "02  부채 펼치기", "03  톡톡 스탬프", "04  쏴샤삭 슬라이드", "05  쏴샤삭 바운스" };
    private readonly string[] _notes = {
        "작게 웅크렸다가 통통!\n말랑한 탄성과 짧은 순차 등장",
        "한 뭉치에서 촤르륵!\n살짝 기울어진 카드가 펼쳐져 제자리로",
        "하나씩 톡! 톡! 톡!\n가벼운 착지와 알록달록한 종이 조각",
        "오른쪽에서 왼쪽으로 쏴샤삭!\n짧은 간격으로 빠르게 들어와 딱 정렬",
        "기울어져 날아와 통통!\n오른쪽에서 쏴샤삭, 말랑하게 멈추는 카드"
    };
    private RectTransform _stage;
    private TextMeshProUGUI _note, _status, _speedText;
    private Sequence _sequence;
    private int _variant;
    private bool _slow;
    public Camera PreviewCamera { get; private set; }

    private void Start()
    {
        PreviewCamera = new GameObject("Preview Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        PreviewCamera.transform.SetParent(transform, false);
        PreviewCamera.transform.localPosition = new Vector3(0, 0, -10);
        PreviewCamera.clearFlags = CameraClearFlags.SolidColor;
        PreviewCamera.backgroundColor = new Color32(25, 65, 72, 255);
        PreviewCamera.orthographic = true;
        var canvasObject = new GameObject("Casual Choice Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = PreviewCamera;
        canvas.planeDistance = 5;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        if (EventSystem.current == null)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            events.transform.SetParent(transform, false);
        }
        var root = (RectTransform)canvasObject.transform;
        _backgroundRect = ResearchUi.Stretch(ResearchUi.NewRect(root, "Scrolling Icon Background"));
        var background = _backgroundRect.gameObject.AddComponent<RawImage>();
        // 이미 열려 있던 비교 씬에도 새 배경이 적용되도록 기본 머티리얼을 제공한다.
        var backgroundMaterial = _scrollingBackground != null ? _scrollingBackground
            : Resources.Load<Material>("CasualChoice_ScrollingBackground");
        _backgroundInstance = new Material(backgroundMaterial);
        background.material = _backgroundInstance;
        background.raycastTarget = false;
        _note = Text(root, "Description", "", new Vector2(0, 625), new Vector2(960, 130), 32);
        _stage = ResearchUi.Place(ResearchUi.NewRect(root, "Cards"), Vector2.one * 0.5f,
            Vector2.one * 0.5f, new Vector2(0, 45), new Vector2(1000, 980));
        for (int i = 0; i < 3; i++)
        {
            var wrapper = ResearchUi.Place(ResearchUi.NewRect(_stage, "Choice " + (i + 1)), Vector2.one * 0.5f,
                Vector2.one * 0.5f, _positions[i], new Vector2(858, 273));
            var card = Instantiate(_cardPrefab, wrapper);
            var rt = (RectTransform)card.transform;
            ResearchUi.Place(rt, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(1100, 350));
            rt.localScale = Vector3.one * 0.78f;
            card.Setup(_samples[i], OnSelected);
            _cards.Add(wrapper);
            _groups.Add(wrapper.gameObject.AddComponent<CanvasGroup>());
        }
        _status = Text(root, "Status", "", new Vector2(0, -485), new Vector2(960, 65), 30);
        for (int i = 0; i < _names.Length; i++)
        {
            int index = i;
            var position = i < 3 ? new Vector2((i - 1) * 326, -580) : new Vector2((i - 3.5f) * 440, -680);
            var size = new Vector2(i < 3 ? 308 : 416, 84);
            var tab = Button(root, _names[i], position, size, () => PlayVariant(index));
            _tabs.Add(tab.GetComponent<Image>());
        }
        Button(root, "다시 보기", new Vector2(-200, -790), new Vector2(350, 90), Replay);
        var speed = Button(root, "속도  1×", new Vector2(200, -790), new Vector2(350, 90), () =>
        {
            _slow = !_slow;
            _speedText.text = _slow ? "속도  0.5×" : "속도  1×";
            Replay();
        });
        _speedText = speed.GetComponentInChildren<TextMeshProUGUI>();
        Text(root, "Footer", "등장 후 카드를 눌러 선택 반응도 비교해 보세요", new Vector2(0, -895), new Vector2(980, 60), 26);
        PlayVariant(0);
    }

    public void PlayVariant(int variant)
    {
        _variant = Mathf.Clamp(variant, 0, _names.Length - 1);
        Replay();
    }

    public void Replay()
    {
        StopAnimation();
        _note.text = _notes[_variant];
        _status.text = "선택지가 나오는 중…";
        for (int i = 0; i < _tabs.Count; i++)
            _tabs[i].color = i == _variant ? new Color32(225, 155, 58, 255) : new Color32(39, 112, 117, 255);
        _sequence = DOTween.Sequence().SetUpdate(true);
        _sequence.timeScale = _slow ? 0.5f : 1f;
        for (int i = 0; i < _cards.Count; i++)
        {
            var card = _cards[i];
            var group = _groups[i];
            group.alpha = 1;
            group.blocksRaycasts = false;
            card.localScale = Vector3.one;
            card.localRotation = Quaternion.identity;
            card.anchoredPosition = _positions[i];
            float at = i * 0.12f;
            if (_variant == 0)
            {
                card.localScale = new Vector3(0.72f, 0.12f, 1);
                card.anchoredPosition += new Vector2(0, -100);
                group.alpha = 0;
                _sequence.Insert(at, group.DOFade(1, 0.08f));
                _sequence.Insert(at, card.DOAnchorPos(_positions[i], 0.44f).SetEase(Ease.OutBack, 1.7f));
                _sequence.Insert(at, card.DOScale(new Vector3(1.04f, 1.1f, 1), 0.26f).SetEase(Ease.OutCubic));
                _sequence.Insert(at + 0.26f, card.DOScale(Vector3.one, 0.24f).SetEase(Ease.OutBack, 2));
            }
            else if (_variant == 1)
            {
                card.anchoredPosition = new Vector2(-70 + 35 * i, -50);
                card.localScale = Vector3.one * 0.6f;
                card.localRotation = Quaternion.Euler(0, 0, (i - 1) * -12);
                group.alpha = 0;
                at = 0.12f + i * 0.1f;
                _sequence.Insert(at, group.DOFade(1, 0.1f));
                _sequence.Insert(at, card.DOAnchorPos(_positions[i], 0.5f).SetEase(Ease.OutBack, 1.2f));
                _sequence.Insert(at, card.DOScale(1, 0.5f).SetEase(Ease.OutBack, 1.4f));
                _sequence.Insert(at, card.DOLocalRotate(Vector3.zero, 0.48f).SetEase(Ease.OutBack));
            }
            else if (_variant == 2)
            {
                at = i * 0.19f;
                card.anchoredPosition += new Vector2(i % 2 == 0 ? -35 : 35, 120);
                card.localScale = Vector3.one * 1.13f;
                card.localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? 5 : -5);
                group.alpha = 0;
                _sequence.Insert(at, group.DOFade(1, 0.08f));
                _sequence.Insert(at, card.DOAnchorPos(_positions[i], 0.2f).SetEase(Ease.InQuad));
                _sequence.Insert(at, card.DOLocalRotate(Vector3.zero, 0.2f));
                _sequence.Insert(at, card.DOScale(new Vector3(1.06f, 0.88f, 1), 0.2f));
                _sequence.Insert(at + 0.2f, card.DOScale(1, 0.3f).SetEase(Ease.OutBack, 2));
                AddConfetti(_positions[i], at + 0.19f, i);
            }
            else
            {
                // 화면 오른쪽 바깥에서 시작한다. 넓은 Game View에서도 대기 카드가 보이지 않는다.
                float startX = ((RectTransform)_stage.parent).rect.width * 0.5f + 600f;
                card.anchoredPosition = _positions[i] + new Vector2(startX, 0);
                if (_variant == 3)
                {
                    at = i * 0.085f;
                    _sequence.Insert(at, card.DOAnchorPos(_positions[i] + new Vector2(-28, 0), 0.24f).SetEase(Ease.OutCubic));
                    _sequence.Insert(at + 0.24f, card.DOAnchorPos(_positions[i], 0.1f).SetEase(Ease.OutQuad));
                }
                else
                {
                    at = i * 0.11f;
                    card.localRotation = Quaternion.Euler(0, 0, -8);
                    card.localScale = new Vector3(1.08f, 0.94f, 1);
                    _sequence.Insert(at, card.DOAnchorPos(_positions[i] + new Vector2(-48, 0), 0.25f).SetEase(Ease.OutCubic));
                    _sequence.Insert(at, card.DOLocalRotate(new Vector3(0, 0, 3), 0.25f).SetEase(Ease.OutQuad));
                    _sequence.Insert(at + 0.25f, card.DOScale(new Vector3(0.94f, 1.06f, 1), 0.08f));
                    _sequence.Insert(at + 0.25f, card.DOAnchorPos(_positions[i], 0.25f).SetEase(Ease.OutBack, 1.7f));
                    _sequence.Insert(at + 0.25f, card.DOLocalRotate(Vector3.zero, 0.25f).SetEase(Ease.OutBack));
                    _sequence.Insert(at + 0.33f, card.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack));
                }
            }
        }
        _sequence.OnComplete(() =>
        {
            foreach (var group in _groups) group.blocksRaycasts = true;
            _status.text = "마음에 드는 선택지를 골라주세요!";
        });
    }

    private void AddConfetti(Vector2 center, float at, int seed)
    {
        Color[] colors = { new Color32(255, 222, 105, 255), new Color32(255, 145, 113, 255), new Color32(110, 228, 200, 255) };
        for (int j = 0; j < 8; j++)
        {
            float side = j < 4 ? -1 : 1;
            var from = center + new Vector2(side * 385, (j % 4 - 1.5f) * 42);
            var dot = ResearchUi.NewImage(_stage, "Paper Pop", j % 2 == 0 ? _art.CircleFill : _art.RoundedFill,
                new Vector2(14 + j % 3 * 5, 18), from);
            dot.color = colors[(j + seed) % colors.Length];
            dot.rectTransform.localScale = Vector3.zero;
            _particles.Add(dot.gameObject);
            _sequence.Insert(at, dot.rectTransform.DOScale(1, 0.07f));
            _sequence.Insert(at, dot.rectTransform.DOAnchorPos(from + new Vector2(side * (35 + j % 3 * 15), 65 - j % 4 * 40), 0.4f).SetEase(Ease.OutQuad));
            _sequence.Insert(at, dot.rectTransform.DOLocalRotate(new Vector3(0, 0, side * 130), 0.4f));
            _sequence.Insert(at + 0.1f, dot.DOFade(0, 0.3f));
        }
    }

    private void OnSelected(LevelUpCardUI selected)
    {
        StopAnimation();
        _status.text = selected.GetData().chooseName + " 선택!";
        _sequence = DOTween.Sequence().SetUpdate(true);
        _sequence.timeScale = _slow ? 0.5f : 1f;
        for (int i = 0; i < _cards.Count; i++)
        {
            _groups[i].blocksRaycasts = false;
            if (selected.transform.parent == _cards[i])
            {
                _sequence.Insert(0, _cards[i].DOScale(0.95f, 0.09f));
                _sequence.Insert(0.09f, _cards[i].DOScale(1.06f, 0.28f).SetEase(Ease.OutBack));
            }
            else _sequence.Insert(0, _groups[i].DOFade(0.35f, 0.2f));
        }
    }

    private void StopAnimation()
    {
        _sequence?.Kill();
        _sequence = null;
        foreach (var particle in _particles) if (particle != null) { particle.SetActive(false); Destroy(particle); }
        _particles.Clear();
    }

    private void OnDisable() => StopAnimation();

    private void LateUpdate()
    {
        if (_backgroundRect == null || _backgroundRect.rect.size == _backgroundSize) return;
        _backgroundSize = _backgroundRect.rect.size;
        // 화면 비율이 바뀌어도 아이콘 크기와 가로/세로 비율을 유지한다.
        _backgroundInstance.SetVector("_IconTiling", new Vector4(_backgroundSize.x / 160f, _backgroundSize.y / 160f, 0, 0));
    }

    private void OnDestroy()
    {
        if (_backgroundInstance != null) Destroy(_backgroundInstance);
    }

    private TextMeshProUGUI Text(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize)
    {
        var rect = ResearchUi.Place(ResearchUi.NewRect(parent, name), Vector2.one * 0.5f, Vector2.one * 0.5f, position, size);
        return ResearchUi.NewText(rect, "Text", value, fontSize, _art.FontBold, TextAlignmentOptions.Center);
    }

    private Button Button(Transform parent, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var button = ResearchUi.NewButton(parent, label, _art.RoundedFill, new Color32(39, 112, 117, 255), label,
            32, _art.FontBold, out var image, out _);
        image.pixelsPerUnitMultiplier = _art.RoundedCornerScale;
        ResearchUi.Place((RectTransform)button.transform, Vector2.one * 0.5f, Vector2.one * 0.5f, position, size);
        button.onClick.AddListener(action);
        return button;
    }
}
