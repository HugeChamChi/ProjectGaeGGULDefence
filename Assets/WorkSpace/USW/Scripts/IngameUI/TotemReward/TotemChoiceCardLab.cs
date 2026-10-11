using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Card-format comparison for the standalone totem choice lab.</summary>
public sealed class TotemChoiceCardLab : MonoBehaviour
{
    public TotemChoiceMotionLab Diagonal;
    public GameObject DiagonalStage;
    public GameObject CardStage;
    public Button DiagonalTab;
    public Button CardTab;
    public Button Replay;
    public RectTransform[] Cards;
    public RectTransform[] Icons;
    public CanvasGroup[] Groups;
    public Button[] Choices;
    public GameObject[] SelectedBadges;
    public TMP_Text Status;

    private readonly Vector2[] _home = new Vector2[3];
    private Sequence _sequence;
    private bool _ready;

    private void Awake()
    {
        for (int i = 0; i < Cards.Length; i++)
        {
            _home[i] = Cards[i].anchoredPosition;
            int index = i;
            Choices[i].onClick.AddListener(() => Select(index));
        }
        DiagonalTab.onClick.AddListener(() => ShowCards(false));
        CardTab.onClick.AddListener(() => ShowCards(true));
        Replay.onClick.AddListener(Play);
    }

    private void Start() => ShowCards(true);

    public void ShowCards(bool show)
    {
        _sequence?.Kill();
        Diagonal.enabled = !show;
        DiagonalStage.SetActive(!show);
        CardStage.SetActive(show);
        var selected = new Color(1f, .80f, .27f);
        var idle = new Color(.17f, .23f, .29f);
        DiagonalTab.targetGraphic.color = show ? idle : selected;
        CardTab.targetGraphic.color = show ? selected : idle;
        if (show) Play();
        else Diagonal.Play(Diagonal.Motion);
    }

    public void Play()
    {
        _sequence?.Kill();
        _ready = false;
        Status.text = "원하는 토템 카드를 눌러주세요";
        _sequence = DOTween.Sequence().SetUpdate(true);
        for (int i = 0; i < Cards.Length; i++)
        {
            float at = i * .085f;
            Cards[i].anchoredPosition = _home[i] + new Vector2(0, -130f);
            Cards[i].localScale = new Vector3(.92f, .92f, 1f);
            Icons[i].localScale = Vector3.one * .7f;
            Icons[i].localRotation = Quaternion.Euler(0, 0, i == 1 ? 10f : -10f);
            Groups[i].alpha = 0;
            Groups[i].interactable = false;
            SelectedBadges[i].SetActive(false);
            _sequence.Insert(at, Groups[i].DOFade(1, .07f));
            _sequence.Insert(at, Cards[i].DOAnchorPos(_home[i], .26f).SetEase(Ease.OutQuart));
            _sequence.Insert(at, Cards[i].DOScale(1f, .28f).SetEase(Ease.OutBack, 1.4f));
            _sequence.Insert(at + .06f, Icons[i].DOScale(1f, .24f).SetEase(Ease.OutBack, 2f));
            _sequence.Insert(at + .06f, Icons[i].DOLocalRotate(Vector3.zero, .22f).SetEase(Ease.OutCubic));
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
        Status.text = new[] { "등급 상승 토템을 선택했어요!", "그림자 토템을 선택했어요!", "풍차 토템을 선택했어요!" }[index];
        SelectedBadges[index].SetActive(true);
        _sequence = DOTween.Sequence().SetUpdate(true);
        for (int i = 0; i < Cards.Length; i++)
        {
            Groups[i].interactable = false;
            _sequence.Insert(0, Groups[i].DOFade(i == index ? 1f : .42f, .15f));
            _sequence.Insert(0, Cards[i].DOScale(i == index ? 1.025f : .975f, .18f).SetEase(Ease.OutCubic));
        }
        _sequence.Insert(0, Icons[index].DOPunchScale(Vector3.one * .17f, .28f, 5, .5f));
    }

    public void PreviewFrame(float seconds)
    {
        Play();
        _sequence.Pause();
        _sequence.Goto(seconds, false);
    }

    private void OnDisable() => _sequence?.Kill();
}
