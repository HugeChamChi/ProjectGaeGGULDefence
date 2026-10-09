using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Standalone, unscaled-time preview of alternating diagonal totem entrances.</summary>
public sealed class TotemChoiceMotionLab : MonoBehaviour
{
    public RectTransform[] Bands;
    public RectTransform[] Icons;
    public RectTransform[] Copy;
    public CanvasGroup[] Groups;
    public CanvasGroup[] SpeedLines;
    public Button[] Choices;
    public Button[] Modes;
    public Button Replay;
    public TMP_Text Status;
    public TMP_Text ModeLabel;
    [Range(0, 1)] public int Motion = 1;

    private readonly Vector2[] _home = new Vector2[3];
    private readonly Vector2[] _iconHome = new Vector2[3];
    private readonly Vector2[] _copyHome = new Vector2[3];
    private Sequence _sequence;
    private bool _ready;
    private static readonly string[] Labels = { "01  DIAGONAL SLASH", "02  DIAGONAL RUSH" };

    private void Awake()
    {
        for (int i = 0; i < 3; i++)
        {
            _home[i] = Bands[i].anchoredPosition;
            _iconHome[i] = Icons[i].anchoredPosition;
            _copyHome[i] = Copy[i].anchoredPosition;
            int index = i;
            Choices[i].onClick.AddListener(() => Select(index));
        }
        for (int i = 0; i < Modes.Length; i++)
        {
            int index = i;
            Modes[i].onClick.AddListener(() => Play(index));
        }
        Replay.onClick.AddListener(() => Play(Motion));
    }

    private void Start() => Play(Motion);

    public void Play(int mode)
    {
        _sequence?.Kill();
        Motion = Mathf.Clamp(mode, 0, 1);
        _ready = false;
        Status.text = "마음에 드는 토템을 골라주세요!";
        ModeLabel.text = Labels[Motion];
        for (int i = 0; i < Modes.Length; i++)
            Modes[i].GetComponent<Image>().color = i == Motion ? new Color(1f, .79f, .16f) : new Color(.21f, .24f, .33f);

        _sequence = DOTween.Sequence().SetUpdate(true);
        for (int i = 0; i < 3; i++)
        {
            var band = Bands[i];
            var icon = Icons[i];
            var copy = Copy[i];
            float direction = i == 1 ? 1f : -1f;
            float at = i * .095f;
            var diagonal = new Vector2(direction, direction * .414f);
            band.anchoredPosition = _home[i];
            band.localScale = Vector3.one;
            band.localRotation = Quaternion.identity;
            icon.anchoredPosition = _iconHome[i];
            icon.localScale = Vector3.one;
            icon.localRotation = Quaternion.identity;
            copy.anchoredPosition = _copyHome[i];
            Groups[i].alpha = 0f;
            Groups[i].interactable = false;
            SpeedLines[i].alpha = 0f;

            _sequence.Insert(at, Groups[i].DOFade(1f, .035f));
            if (Motion == 0)
            {
                band.anchoredPosition += diagonal * 1250f;
                _sequence.Insert(at, band.DOAnchorPos(_home[i] - diagonal * 24f, .19f).SetEase(Ease.OutQuart));
                _sequence.Insert(at + .19f, band.DOAnchorPos(_home[i], .095f).SetEase(Ease.OutQuad));
                icon.localScale = Vector3.one * .68f;
                _sequence.Insert(at + .10f, icon.DOScale(1f, .23f).SetEase(Ease.OutBack, 2f));
                copy.anchoredPosition += new Vector2(direction * 95f, 0f);
                _sequence.Insert(at + .065f, copy.DOAnchorPos(_copyHome[i], .22f).SetEase(Ease.OutQuart));
            }
            else
            {
                band.anchoredPosition += diagonal * 1160f;
                band.localScale = new Vector3(1.15f, .65f, 1f);
                _sequence.Insert(at, band.DOAnchorPos(_home[i] - diagonal * 18f, .22f).SetEase(Ease.OutExpo));
                _sequence.Insert(at + .22f, band.DOAnchorPos(_home[i], .10f).SetEase(Ease.OutQuad));
                _sequence.Insert(at, band.DOScale(1f, .29f).SetEase(Ease.OutBack, 1.6f));
                icon.anchoredPosition += diagonal * 190f;
                icon.localRotation = Quaternion.Euler(0f, 0f, direction * 20f);
                _sequence.Insert(at + .075f, icon.DOAnchorPos(_iconHome[i], .24f).SetEase(Ease.OutBack));
                _sequence.Insert(at + .075f, icon.DOLocalRotate(Vector3.zero, .27f).SetEase(Ease.OutBack));
            }
            _sequence.Insert(at + .10f, SpeedLines[i].DOFade(.85f, .035f));
            _sequence.Insert(at + .135f, SpeedLines[i].DOFade(0f, .15f));
        }
        _sequence.OnComplete(() =>
        {
            _ready = true;
            foreach (var group in Groups) group.interactable = true;
        });
    }

    public void Select(int index)
    {
        if (!_ready) return;
        _sequence?.Kill();
        _ready = false;
        Status.text = new[] { "등급 상승 토템 선택!", "그림자 토템 선택!", "풍차 토템 선택!" }[index];
        _sequence = DOTween.Sequence().SetUpdate(true);
        for (int i = 0; i < 3; i++)
        {
            Groups[i].interactable = false;
            _sequence.Insert(0f, Groups[i].DOFade(i == index ? 1f : .3f, .14f));
        }
        _sequence.Insert(0f, Icons[index].DOPunchScale(Vector3.one * .18f, .30f, 5, .5f));
        _sequence.Insert(0f, Copy[index].DOPunchAnchorPos(new Vector2(0f, 12f), .24f, 4, .5f));
    }

    // Used by the lab's visual checks to inspect deterministic animation frames.
    public void PreviewFrame(int mode, float seconds)
    {
        Play(mode);
        _sequence.Pause();
        _sequence.Goto(seconds, false);
    }

    private void OnDisable() => _sequence?.Kill();
}
