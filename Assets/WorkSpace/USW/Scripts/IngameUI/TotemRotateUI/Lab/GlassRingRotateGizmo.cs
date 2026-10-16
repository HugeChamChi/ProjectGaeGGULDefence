using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// M1 글래스 링: 어두운 반투명 유리 띠 + 안팎 가는 흰 선. 고른 방향의 사분면만 흰색으로 차오르고,
/// 손가락 쪽으로 살짝 끌려가는(최대 12°) 탄성이 있어 "잡고 돌린다"는 손맛을 준다. 금색 대신 흰색·민트로 모던하게.
/// </summary>
public sealed class GlassRingRotateGizmo : TotemRotateLabGizmo
{
    private const float Radius = 228f;
    private const float Band = 40f;
    private const float Dead = 66f;
    private const float MaxTug = 12f;
    private const float TugRatio = 0.3f;

    private readonly RectTransform _pivot;
    private readonly CanvasGroup _highlight;
    private readonly GameObject _cross;
    private Tween _fade;
    private float _baseAngle, _tug, _shownAngle;
    private bool _visible;

    /// <inheritdoc/>
    public override float DeadZone => Dead;
    /// <inheritdoc/>
    public override string Title => "M1 글래스 링";
    /// <inheritdoc/>
    public override string Description => "어두운 반투명 유리 띠 + 가는 흰 선. 고른 사분면만 흰색으로 차고 손가락 쪽으로 살짝 끌려감. 금색 없이 흰색·민트.";

    /// <summary>유리 띠·선·점·사분면 강조를 만든다.</summary>
    public GlassRingRotateGizmo(RectTransform parent, TotemRotateLabSprites sprites) : base(parent, sprites, "Gizmo_M1_Glass")
    {
        float outer = (Radius + Band * 0.5f) * 2f;
        AddImage("Band", Root, sprites.Ring, Vector2.one * outer, TotemRotateLabPalette.Glass);
        AddImage("Outer", Root, sprites.ThinRing, Vector2.one * outer, TotemRotateLabPalette.Hairline);
        AddImage("Inner", Root, sprites.ThinRing, Vector2.one * (outer - Band * 2f), TotemRotateLabPalette.Faint);
        for (int i = 0; i < 4; i++)
            AddImage("Dot_" + i, Root, sprites.Disc, Vector2.one * 10f, TotemRotateLabPalette.Hairline).rectTransform.anchoredPosition = Directions[i] * Radius;

        _pivot = NewRect("Quadrant", Root, Vector2.zero);
        _highlight = _pivot.gameObject.AddComponent<CanvasGroup>();
        AddArc("Fill", _pivot, sprites.Ring, outer, 0.25f, TotemRotateLabPalette.WithAlpha(TotemRotateLabPalette.Active, 0.88f));
        AddArc("Glow", _pivot, sprites.ThinRing, outer + 18f, 0.25f, TotemRotateLabPalette.WithAlpha(TotemRotateLabPalette.Accent, 0.9f));
        var arrow = AddImage("Arrow", _pivot, sprites.Triangle, new Vector2(30f, 22f), TotemRotateLabPalette.Ink);
        arrow.rectTransform.anchoredPosition = Vector2.up * Radius;
        AddDeadZone(Dead);
        _cross = AddCancelCross();
    }

    /// <inheritdoc/>
    protected override void OnShow()
    {
        _fade?.Kill();
        _highlight.alpha = 0f;
        _visible = false;
    }

    /// <inheritdoc/>
    protected override void OnCandidateChanged(int candidate)
    {
        _cross.SetActive(candidate < 0);
        _fade?.Kill();
        if (candidate < 0)
        {
            _visible = false;
            _fade = _highlight.DOFade(0f, 0.1f).SetUpdate(true);
            return;
        }
        float target = AngleOf(candidate);
        if (!_visible) _shownAngle = target;
        _baseAngle = _shownAngle + Mathf.DeltaAngle(_shownAngle, target);
        _visible = true;
        _fade = _highlight.DOFade(1f, 0.1f).SetUpdate(true);
    }

    /// <inheritdoc/>
    public override void SetPointer(Vector2 delta)
    {
        if (Candidate < 0 || delta.sqrMagnitude < 1f) { _tug = 0f; return; }
        float pointer = -Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
        _tug = Mathf.Clamp(Mathf.DeltaAngle(_baseAngle, pointer) * TugRatio, -MaxTug, MaxTug);
    }

    /// <inheritdoc/>
    public override void Tick(float unscaledTime)
    {
        float goal = _baseAngle + _tug;
        _shownAngle = Mathf.LerpAngle(_shownAngle, goal, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 22f));
        _pivot.localRotation = Quaternion.Euler(0f, 0f, _shownAngle);
    }

    /// <inheritdoc/>
    protected override void OnHide()
    {
        _fade?.Kill();
        _visible = false;
        _tug = 0f;
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        _fade?.Kill();
        base.Dispose();
    }
}
