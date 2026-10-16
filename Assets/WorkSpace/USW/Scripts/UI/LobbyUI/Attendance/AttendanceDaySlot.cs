using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One prefab-authored attendance day card: reward icon, amount, day pill and received/today states.</summary>
public sealed class AttendanceDaySlot : MonoBehaviour
{
    /// <summary>Presentation state of the day.</summary>
    public enum State { Locked, Claimable, Claimed }

    private static readonly Color PillLocked = new(.24f, .20f, .36f, 1);
    private static readonly Color PillToday = new(1f, .74f, .26f, 1);
    private static readonly Color PillClaimed = new(.20f, .18f, .26f, 1);
    private static readonly Color LabelLocked = new(.84f, .80f, .95f, 1);
    private static readonly Color LabelToday = new(.20f, .12f, .02f, 1);
    private const float GlowPulseSeconds = .6f;

    [SerializeField] private Button _button;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _amount;
    [SerializeField] private Image _dayPill;
    [SerializeField] private TMP_Text _dayLabel;
    [SerializeField] private GameObject _claimedMark;
    [SerializeField] private Image _glow;
    private State _state;

    /// <summary>Touch target for claiming.</summary>
    public Button Button => _button;
    /// <summary>Current presentation state.</summary>
    public State Current => _state;

    /// <summary>Shows the reward and state for one day.</summary>
    public void Bind(int day, Sprite icon, int amount, State state)
    {
        _state = state;
        if (_icon != null) { _icon.sprite = icon; _icon.enabled = icon != null; _icon.preserveAspect = true; }
        if (_amount != null) _amount.text = "x" + amount.ToString("N0");
        if (_claimedMark != null) _claimedMark.SetActive(state == State.Claimed);
        if (_dayPill != null) _dayPill.color = state == State.Claimable ? PillToday : state == State.Claimed ? PillClaimed : PillLocked;
        if (_dayLabel != null)
        {
            _dayLabel.text = state == State.Claimable ? "받기" : day + "일차";
            _dayLabel.color = state == State.Claimable ? LabelToday : LabelLocked;
        }
        if (_glow != null)
        {
            _glow.DOKill();
            _glow.gameObject.SetActive(state == State.Claimable);
            if (state == State.Claimable)
            {
                var color = _glow.color; color.a = 1; _glow.color = color;
                _glow.DOFade(.35f, GlowPulseSeconds).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
            }
        }
    }

    /// <summary>Short confirmation bounce after a successful claim.</summary>
    public void PlayClaimed()
    {
        transform.DOKill(true);
        transform.DOPunchScale(Vector3.one * .12f, .35f, 6, .6f).SetUpdate(true).SetLink(gameObject);
    }

    /// <summary>Small refusal shake for a day that cannot be claimed.</summary>
    public void PlayRefused()
    {
        transform.DOKill(true);
        transform.DOShakePosition(.25f, new Vector3(8, 0, 0), 20, 0, false, true).SetUpdate(true).SetLink(gameObject);
    }

    /// <summary>Stops looping effects while the popup is hidden.</summary>
    public void StopEffects() { if (_glow != null) _glow.DOKill(); transform.DOKill(true); }

    private void OnDisable() => StopEffects();
}
