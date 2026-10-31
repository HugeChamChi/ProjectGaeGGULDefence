using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>Owns HUD tuck motion and restores its captured state after the last hide owner releases it.</summary>
public sealed class IngameHudMotion : MonoBehaviour
{
    [SerializeField] private RectTransform _buttons;
    [SerializeField] private RectTransform[] _sideTargets;
    [SerializeField, Min(0f)] private float _downDistance = 600f;
    [SerializeField, Min(0f)] private float _rightDistance = 340f;
    [SerializeField, Min(0f)] private float _hideSeconds = .3f;
    [SerializeField, Min(0f)] private float _showSeconds = .4f;
    private readonly HashSet<object> _owners = new HashSet<object>();
    private readonly List<Target> _targets = new List<Target>();
    private Sequence _motion;

    private sealed class Target
    {
        internal RectTransform Rect;
        internal CanvasGroup Group;
        internal Vector2 Home, Offset;
        internal float Alpha;
        internal bool Interactable, Raycasts;
    }

    /// <summary>Includes the return animation so other presenters do not overwrite the captured HUD.</summary>
    public bool IsCaptured => _targets.Count > 0;

    /// <summary>Hides all HUD controls once, even when sale and choices overlap.</summary>
    public void Acquire(object owner)
    {
        if (owner == null || !_owners.Add(owner) || _owners.Count > 1) return;
        if (!IsCaptured)
        {
            // The pager reparents Buttons at initialization. Move the whole page, including its graph.
            var lower = _buttons != null && _buttons.parent != null && _buttons.parent.name == "CombatHudPager"
                ? _buttons.parent as RectTransform : _buttons;
            Capture(lower, Vector2.down * _downDistance);
            if (_sideTargets != null)
                foreach (var rect in _sideTargets)
                    if (rect != null && (lower == null || !rect.IsChildOf(lower)))
                        Capture(rect, Vector2.right * _rightDistance);
        }
        Animate(true);
    }

    /// <summary>Releases only this caller; cancellation cannot reveal another owner's hidden HUD.</summary>
    public void Release(object owner, bool animate = true)
    {
        if (owner == null || !_owners.Remove(owner) || _owners.Count > 0) return;
        if (animate) Animate(false);
        else Restore();
    }

    private void Capture(RectTransform rect, Vector2 offset)
    {
        if (rect == null || _targets.Exists(t => t.Rect == rect)) return;
        var group = rect.GetComponent<CanvasGroup>();
        if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
        _targets.Add(new Target { Rect = rect, Group = group, Home = rect.anchoredPosition,
            Offset = offset, Alpha = group.alpha, Interactable = group.interactable, Raycasts = group.blocksRaycasts });
    }

    private void Animate(bool hidden)
    {
        _motion?.Kill();
        if (!IsCaptured) return;
        _motion = DOTween.Sequence().SetUpdate(true);
        foreach (var target in _targets)
        {
            if (target.Rect == null || target.Group == null) continue;
            target.Group.alpha = target.Alpha;
            target.Group.interactable = target.Group.blocksRaycasts = false;
            _motion.Join(target.Rect.DOAnchorPos(hidden ? target.Home + target.Offset : target.Home,
                hidden ? _hideSeconds : _showSeconds).SetEase(hidden ? Ease.InBack : Ease.OutBack));
        }
        _motion.OnComplete(() =>
        {
            _motion = null;
            if (!hidden) Restore();
            else foreach (var target in _targets) if (target.Group != null) target.Group.alpha = 0f;
        });
    }

    private void Restore()
    {
        _motion?.Kill();
        _motion = null;
        foreach (var target in _targets)
        {
            if (target.Rect != null) target.Rect.anchoredPosition = target.Home;
            if (target.Group == null) continue;
            target.Group.alpha = target.Alpha;
            target.Group.interactable = target.Interactable;
            target.Group.blocksRaycasts = target.Raycasts;
        }
        _targets.Clear();
    }

    private void OnDisable() { _owners.Clear(); Restore(); }
    private void OnDestroy() { _owners.Clear(); Restore(); }
}
