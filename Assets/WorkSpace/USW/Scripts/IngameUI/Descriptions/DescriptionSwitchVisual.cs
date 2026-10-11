using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>Positions the shared switch thumb, including authored test-scene references.</summary>
public sealed class DescriptionSwitchVisual : MonoBehaviour
{
    public Toggle Toggle;
    public Image Track;
    public RectTransform Knob;
    public readonly TMP_Text[] Labels = new TMP_Text[2];
    [SerializeField, Min(0f)] private float _transitionSeconds = 0.2f;
    private Image _knobImage;
    private Tween _transition;
    private bool _hasState;
    private bool _lastState;
    private float _progress;

    private static readonly Color TrackOff = new Color(0.28f, 0.28f, 0.32f);
    private static readonly Color TrackOn = new Color(0.56f, 0.35f, 0.77f);
    private static readonly Color LabelOff = new Color(0.65f, 0.65f, 0.70f);

    private void LateUpdate() => Refresh();

    /// <summary>Animates visual state changes, including changes made without Toggle notifications.</summary>
    public void Refresh()
    {
        if (Toggle == null) return;
        if (Track != null)
        {
            if (Track.sprite == null) Track.sprite = DescriptionToggleView.TrackSprite;
        }
        if (Knob != null)
        {
            if (_knobImage == null) _knobImage = Knob.GetComponent<Image>();
            if (_knobImage != null && _knobImage.sprite == null) _knobImage.sprite = DescriptionToggleView.KnobSprite;
        }
        if (_hasState && _lastState == Toggle.isOn) return;
        bool immediate = !_hasState || !Application.isPlaying || !isActiveAndEnabled || _transitionSeconds <= 0f;
        _hasState = true;
        _lastState = Toggle.isOn;
        _transition?.Kill();
        _transition = null;
        float target = _lastState ? 1f : 0f;
        if (immediate)
        {
            _progress = target;
            ApplyVisual();
        }
        else
        {
            _transition = DOTween.To(() => _progress, value => { _progress = value; ApplyVisual(); }, target, _transitionSeconds)
                .SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject)
                .OnComplete(() => _transition = null);
        }
    }

    private void ApplyVisual()
    {
        if (Track != null) Track.color = Color.Lerp(TrackOff, TrackOn, _progress);
        if (Knob != null) Knob.anchoredPosition = new Vector2(Mathf.Lerp(24f, 68f, _progress), 0f);
        for (int i = 0; i < Labels.Length; i++)
        {
            if (Labels[i] == null) continue;
            bool selected = _lastState == (i == 1);
            Labels[i].color = Color.Lerp(LabelOff, Color.white, i == 1 ? _progress : 1f - _progress);
            Labels[i].fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    private void OnEnable() => _hasState = false;

    private void OnDisable()
    {
        _transition?.Kill();
        _transition = null;
        _hasState = false;
    }

    private void OnDestroy() => _transition?.Kill();
}
