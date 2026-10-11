using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>상시 글로우 없이 네 가지 선택지 등장 동작을 비교하는 실험실.</summary>
public sealed class ChoiceEntranceLab : MonoBehaviour
{
    [SerializeField] private LevelUpCardUI _prefab;
    [SerializeField] private RectTransform _container;
    [SerializeField] private LevelUpData[] _samples;
    [SerializeField] private UiFxParticleEmitter _motes;
    [SerializeField] private TMP_Text _status;
    [SerializeField] private Image[] _variantButtons;
    private readonly LevelUpCardUI[] _cards = new LevelUpCardUI[3];
    private readonly CanvasGroup[] _groups = new CanvasGroup[3];
    private readonly RectTransform[] _rects = new RectTransform[3];
    private readonly Vector2[] _positions = new Vector2[3];
    private readonly Image[] _lines = new Image[3];
    private static readonly string[] Names = { "A · 잔잔한 등장", "B · 차례로 등장", "C · 부드러운 확대", "D · 얇은 선 연출" };
    private Sequence _sequence;
    private int _variant;
    private int _tier = 2;
    private bool _ready;
    private bool _loop;
    private bool _slow;
    private float _nextReplay;

    /// <summary>현재 시안 번호(0~3).</summary>
    public int Variant => _variant;
    /// <summary>등장 연출 재생 여부.</summary>
    public bool IsAnimating => _sequence != null && _sequence.IsActive() && _sequence.IsPlaying();

    private void Start()
    {
        for (int i = 0; i < _cards.Length; i++)
        {
            var card = Instantiate(_prefab, _container);
            _cards[i] = card;
            _rects[i] = (RectTransform)card.transform;
            _groups[i] = card.GetComponent<CanvasGroup>();
            if (_groups[i] == null) _groups[i] = card.gameObject.AddComponent<CanvasGroup>();
            _groups[i].blocksRaycasts = false;
            _groups[i].interactable = false;
            var line = new GameObject("EntranceLine", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            line.rectTransform.SetParent(card.transform, false);
            line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0f, 1f);
            line.rectTransform.pivot = new Vector2(0f, 0.5f);
            line.rectTransform.anchoredPosition = new Vector2(20f, 10f);
            line.rectTransform.sizeDelta = new Vector2(0f, 2f);
            line.raycastTarget = false;
            _lines[i] = line;
        }
        ApplyData();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_container);
        for (int i = 0; i < _cards.Length; i++) _positions[i] = _rects[i].anchoredPosition;
        var layout = _container.GetComponent<LayoutGroup>();
        if (layout != null) layout.enabled = false;
        _ready = true;
        if (_motes != null) _motes.Play();
        Replay();
    }

    private void Update()
    {
        if (_loop && _ready && !IsAnimating && Time.unscaledTime >= _nextReplay) Replay();
    }

    /// <summary>시안을 선택하고 처음부터 재생한다.</summary>
    public void ShowVariant(int index) { _variant = Mathf.Clamp(index, 0, 3); Replay(); }
    /// <summary>동일한 등급과 카드로 현재 시안을 다시 재생한다.</summary>
    public void Replay()
    {
        if (!_ready) return;
        _sequence?.Kill();
        _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        float pace = _slow ? 2f : 1f;
        for (int i = 0; i < _cards.Length; i++)
        {
            var rt = _rects[i];
            var group = _groups[i];
            var line = _lines[i];
            rt.anchoredPosition = _positions[i];
            rt.localScale = Vector3.one;
            group.alpha = 0f;
            line.color = new Color(1f, 0.78f, 0.45f, 0f);
            line.rectTransform.sizeDelta = new Vector2(0f, 2f);
            float delay = (_variant == 0 ? i * 0.045f : i * 0.11f) * pace;
            if (_variant == 0)
            {
                rt.anchoredPosition -= new Vector2(0f, 18f);
                _sequence.Insert(delay, rt.DOAnchorPos(_positions[i], 0.42f * pace).SetEase(Ease.OutSine));
            }
            else if (_variant == 1)
            {
                rt.anchoredPosition -= new Vector2(45f, 0f);
                _sequence.Insert(delay, rt.DOAnchorPos(_positions[i], 0.38f * pace).SetEase(Ease.OutCubic));
            }
            else if (_variant == 2)
            {
                rt.localScale = Vector3.one * 0.95f;
                _sequence.Insert(delay, rt.DOScale(1f, 0.44f * pace).SetEase(Ease.OutCubic));
            }
            else
            {
                line.color = new Color(1f, 0.78f, 0.45f, 0.65f);
                _sequence.Insert(delay, line.rectTransform.DOSizeDelta(new Vector2(Mathf.Max(0f, rt.rect.width - 40f), 2f), 0.40f * pace).SetEase(Ease.OutCubic));
                _sequence.Insert(delay + 0.22f * pace, line.DOFade(0f, 0.24f * pace));
            }
            _sequence.Insert(delay, group.DOFade(1f, 0.38f * pace).SetEase(Ease.OutSine));
        }
        _sequence.OnComplete(() => _nextReplay = Time.unscaledTime + 1.8f);
        RefreshLabels();
    }

    /// <summary>카드 등급을 바꾸고 재생한다. 0=레어, 1=에픽, 2=레전더리.</summary>
    public void ShowTier(int index) { _tier = Mathf.Clamp(index, 0, 2); if (!_ready) return; ApplyData(); Replay(); }
    /// <summary>동작을 자세히 보기 위한 절반 속도 재생을 전환한다.</summary>
    public void ToggleSlow() { _slow = !_slow; Replay(); }
    /// <summary>등장 후 1.8초 머무르고 반복하는 비교 재생을 전환한다.</summary>
    public void ToggleLoop() { _loop = !_loop; _nextReplay = Time.unscaledTime + 1.8f; RefreshLabels(); }

    private void ApplyData()
    {
        var data = _samples.Where(d => d != null && d.tier == (Tier)((int)Tier.Rare + _tier)).Take(3).ToArray();
        for (int i = 0; i < _cards.Length; i++)
            if (i < data.Length) _cards[i].Setup(data[i], null);
    }

    private void RefreshLabels()
    {
        if (_status != null) _status.text = $"{Names[_variant]}  |  속도 {(_slow ? "0.5" : "1")}  |  반복 {(_loop ? "ON" : "OFF")}\n등장 연출만 비교 · 대기 중에는 은은하게";
        for (int i = 0; i < _variantButtons.Length; i++)
            _variantButtons[i].color = i == _variant ? new Color(0.22f, 0.42f, 0.36f, 1f) : new Color(0.16f, 0.16f, 0.18f, 1f);
    }

    private void OnDisable()
    {
        _sequence?.Kill();
        if (_motes != null) _motes.Stop(true);
        for (int i = 0; i < _cards.Length; i++)
        {
            if (_rects[i] == null) continue;
            _rects[i].anchoredPosition = _positions[i];
            _rects[i].localScale = Vector3.one;
            _groups[i].alpha = 1f;
            _lines[i].color = Color.clear;
        }
    }
}
