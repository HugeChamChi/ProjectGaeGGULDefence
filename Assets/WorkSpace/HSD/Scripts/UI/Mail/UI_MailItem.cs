using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Inline mail title, full content, reward, expiry and claim in one list row.</summary>
public class UI_MailItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI contentText;
    [SerializeField] private Button receiveButton;
    [SerializeField] private TextMeshProUGUI _rewardText;
    [SerializeField] private TextMeshProUGUI _expiryText;
    [SerializeField] private TextMeshProUGUI _claimText;
    [SerializeField] private Button _readButton;
    [SerializeField] private LayoutElement _layoutElement;
    private Post _post;
    private float _lastWidth;

    /// <summary>Compatibility boundary for older callers.</summary>
    public void Init(Post post, Action onReceive) => Bind(post, false, null, onReceive);
    /// <summary>Binds stable identity; title/body taps mark read without opening another window.</summary>
    public void Bind(Post post, bool busy, Action onRead, Action onReceive)
    {
        _post = post;
        titleText.textWrappingMode = _rewardText.textWrappingMode = TextWrappingModes.Normal;
        titleText.enableAutoSizing = _rewardText.enableAutoSizing = false;
        titleText.text = (post.IsRead ? "" : "● ") + post.title;
        contentText.text = string.IsNullOrEmpty(post.ValidationError) ? post.content ?? "" : (post.content ?? "") + "\n" + post.ValidationError;
        _rewardText.text = post.RewardSummary;
        receiveButton.onClick.RemoveAllListeners(); receiveButton.onClick.AddListener(() => onReceive?.Invoke());
        _readButton.onClick.RemoveAllListeners(); _readButton.onClick.AddListener(() => onRead?.Invoke());
        Layout(); RefreshAvailability(busy);
    }
    /// <summary>Expiry is refreshed locally; backend still validates actual claimability.</summary>
    public void RefreshAvailability(bool busy)
    {
        if (_post == null) return;
        _expiryText.text = _post.ExpiryLabel + (_post.IsRead ? " · 읽음" : "");
        receiveButton.interactable = !busy && _post.isCanReceive && !_post.IsExpired;
        _claimText.text = _post.IsExpired ? "만료" : !string.IsNullOrEmpty(_post.ValidationError) ? "확인 필요" : _post.postReward.Count > 0 ? "받기" : "확인";
    }
    private void LateUpdate()
    { if (_post != null && !Mathf.Approximately(_lastWidth, ((RectTransform)transform).rect.width)) Layout(); }
    private void Layout()
    {
        _lastWidth = ((RectTransform)transform).rect.width;
        float textWidth = Mathf.Max(100, _lastWidth - 208);
        float titleHeight = Mathf.Max(40, titleText.GetPreferredValues(titleText.text, textWidth, float.PositiveInfinity).y);
        float bodyHeight = string.IsNullOrWhiteSpace(contentText.text) ? 0 : Mathf.Max(30, contentText.GetPreferredValues(contentText.text, textWidth, float.PositiveInfinity).y);
        float rewardHeight = Mathf.Max(34, _rewardText.GetPreferredValues(_rewardText.text, textWidth, float.PositiveInfinity).y);
        float bodyY = 16 + titleHeight;
        float rewardY = bodyY + bodyHeight + 8;
        float expiryY = rewardY + rewardHeight + 6;
        Place(titleText.rectTransform, 12, titleHeight);
        Place(contentText.rectTransform, bodyY, bodyHeight);
        Place(_rewardText.rectTransform, rewardY, rewardHeight);
        Place(_expiryText.rectTransform, expiryY, 30);
        _layoutElement.preferredHeight = Mathf.Max(172, expiryY + 40);
    }
    private static void Place(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
        rect.offsetMin = new Vector2(20, -y-height); rect.offsetMax = new Vector2(-188, -y);
    }
}
