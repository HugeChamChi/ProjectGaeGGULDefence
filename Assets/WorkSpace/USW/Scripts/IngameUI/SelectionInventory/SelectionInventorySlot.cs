using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 획득 선택지 보관함의 한 칸. 등급색 테두리·채움, 카드 아이콘, 모서리 대상 유닛 얼굴을 표시한다.
/// 빈칸 상태는 옅은 판만 남겨 "채워지는 중"으로 보이게 한다.
/// </summary>
public sealed class SelectionInventorySlot : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private Image _border;
    [SerializeField] private Image _fill;
    [SerializeField] private Image _icon;
    [SerializeField] private SelectionInventoryFace _face;
    [SerializeField] private Color _emptyColor = new Color(1f, 1f, 1f, 0.045f);
    [SerializeField, Range(0f, 1f)] private float _instantIconAlpha = 0.45f;
    [SerializeField, Min(0f)] private float _punchScale = 0.08f;
    [SerializeField, Min(0.01f)] private float _punchSeconds = 0.26f;

    private Color _tierColor;
    private Action<SelectionInventorySlot> _onClick;

    /// <summary>표시 중인 칸 내용. 빈칸이면 null.</summary>
    public SelectionInventoryItem Item { get; private set; }

    private void Awake()
    {
        if (_button != null) _button.onClick.AddListener(() => { if (Item != null) _onClick?.Invoke(this); });
    }

    /// <summary>획득한 카드를 표시한다.</summary>
    public void Bind(SelectionInventoryItem item, Color tierText, Color tierFill, Sprite face, Action<SelectionInventorySlot> onClick)
    {
        Item = item;
        _onClick = onClick;
        _tierColor = tierText;
        _border.enabled = true;
        _border.color = tierText;
        _fill.color = tierFill;
        _icon.enabled = true;
        _icon.sprite = item.Card.icon;
        _icon.color = new Color(1f, 1f, 1f, item.Instant ? _instantIconAlpha : 1f);
        if (_face != null) _face.Set(face);
        if (_button != null) _button.interactable = true;
    }

    /// <summary>아직 채워지지 않은 칸.</summary>
    public void BindEmpty()
    {
        Item = null;
        _onClick = null;
        _border.enabled = false;
        _fill.color = _emptyColor;
        _icon.enabled = false;
        if (_face != null) _face.Set(null);
        if (_button != null) _button.interactable = false;
    }

    /// <summary>선택 강조. 켜질 때 살짝 튄다.</summary>
    public void SetSelected(bool selected, Color highlight, bool punch)
    {
        if (Item == null) return;
        _border.color = selected ? highlight : _tierColor;
        if (selected && punch) Punch();
    }

    /// <summary>왼쪽 위부터 차례로 튀어나오는 등장 연출.</summary>
    public void Appear(float delay, float seconds)
    {
        _group.DOKill();
        transform.DOKill();
        transform.localScale = Vector3.one;
        _group.alpha = 0f;
        _group.DOFade(1f, seconds * 0.5f).SetDelay(delay).SetUpdate(true).SetLink(gameObject);
        if (Item == null) return; // 빈칸은 옅게 떠오르기만
        transform.localScale = Vector3.one * 0.55f;
        transform.DOScale(1f, seconds).SetDelay(delay).SetEase(Ease.OutBack, 1.8f).SetUpdate(true).SetLink(gameObject);
        if (_face == null || !_face.gameObject.activeSelf) return;
        _face.transform.localScale = Vector3.zero;
        _face.transform.DOScale(1f, 0.22f).SetDelay(delay + seconds * 0.6f).SetEase(Ease.OutBack, 2.5f)
            .SetUpdate(true).SetLink(_face.gameObject);
    }

    private void Punch()
    {
        transform.DOComplete();
        transform.localScale = Vector3.one;
        transform.DOPunchScale(Vector3.one * _punchScale, _punchSeconds, 6, 0.6f).SetUpdate(true).SetLink(gameObject);
    }
}
