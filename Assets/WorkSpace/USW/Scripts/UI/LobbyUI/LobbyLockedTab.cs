using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>봉인된 로비 탭. 선택을 막고 누르면 자물쇠 흔들림과 짧은 안내를 보여 준다.</summary>
public class LobbyLockedTab : MonoBehaviour
{
    private const float ShakeSeconds = 0.45f;
    private const float ShakeAngle = 18f;
    private const float NoticeRise = 36f;
    private const float NoticeFadeSeconds = 0.18f;
    private const float NoticeHoldSeconds = 0.9f;

    [SerializeField] private bool _locked = true;
    [SerializeField] private RectTransform _lockBadge;
    [SerializeField] private CanvasGroup _notice;

    private Vector2 _noticeOrigin;
    private Sequence _denied;

    /// <summary>탭이 아직 봉인 상태인지 여부.</summary>
    public bool IsLocked => _locked;

    private void Awake()
    {
        if (_notice == null) return;
        _noticeOrigin = ((RectTransform)_notice.transform).anchoredPosition;
        _notice.alpha = 0f;
        _notice.blocksRaycasts = false;
    }

    /// <summary>봉인 탭을 눌렀을 때 자물쇠를 흔들고 안내 문구를 띄웠다가 숨긴다.</summary>
    public void PlayDenied()
    {
        _denied?.Kill(true);
        _denied = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        if (_lockBadge != null)
        {
            _lockBadge.localRotation = Quaternion.identity;
            _denied.Insert(0f, _lockBadge.DOPunchRotation(new Vector3(0f, 0f, ShakeAngle), ShakeSeconds, 10, 0.6f));
            _denied.Insert(0f, _lockBadge.DOPunchScale(Vector3.one * 0.18f, ShakeSeconds * 0.6f, 6, 0.5f));
        }
        if (_notice != null)
        {
            var rect = (RectTransform)_notice.transform;
            rect.anchoredPosition = _noticeOrigin;
            _notice.alpha = 0f;
            _denied.Insert(0f, _notice.DOFade(1f, NoticeFadeSeconds));
            _denied.Insert(0f, rect.DOAnchorPos(_noticeOrigin + Vector2.up * NoticeRise, NoticeFadeSeconds).SetEase(Ease.OutCubic));
            _denied.Insert(NoticeFadeSeconds + NoticeHoldSeconds, _notice.DOFade(0f, NoticeFadeSeconds));
        }
    }

    private void OnDisable()
    {
        _denied?.Kill(true);
        _denied = null;
        if (_lockBadge != null) _lockBadge.localRotation = Quaternion.identity;
        if (_notice != null) _notice.alpha = 0f;
    }
}
