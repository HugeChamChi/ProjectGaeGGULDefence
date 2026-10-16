using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// M2 명일방주식 원판: 반투명 흰 원판 + 안쪽 가는 마름모 + 손가락을 따라오는 가운데 손잡이.
/// 고른 방향의 마름모 꼭짓점 쪽 삼각 조각이 밝아진다. 명일방주처럼 <b>원 밖으로 끌고 놓으면 취소</b>(가운데도 취소 유지).
/// 지금 게임의 마름모를 버리지 않고 정돈한 안.
/// </summary>
public sealed class VeilDiscRotateGizmo : TotemRotateLabGizmo
{
    private const float Radius = 250f;
    private const float Dead = 66f;
    private const float CancelRadius = 290f;
    private const float HandleReach = 200f;
    private const float DiamondHalfDiagonal = 200f;

    private readonly Image _veil, _edge, _handle;
    private readonly RectTransform _wedgePivot;
    private readonly CanvasGroup _wedge;
    private readonly GameObject _cross;
    private Tween _spin, _fade;
    private bool _visible, _outside;

    /// <inheritdoc/>
    public override float DeadZone => Dead;
    /// <inheritdoc/>
    public override float CancelBeyond => CancelRadius;
    /// <inheritdoc/>
    public override string IdleHint => "방향으로 끌기 · 원 밖으로 끌고 놓으면 <color=#FF7A6B>취소</color>";
    /// <inheritdoc/>
    public override string Title => "M2 명일방주식 원판";
    /// <inheritdoc/>
    public override string Description => "반투명 흰 원판 + 가는 마름모 + 따라오는 손잡이. 고른 꼭짓점 쪽이 밝아짐. 원 밖으로 끌면 취소(명일방주 규칙). 지금 마름모를 정돈한 안.";

    /// <summary>원판·마름모·삼각 조각·손잡이를 만든다.</summary>
    public VeilDiscRotateGizmo(RectTransform parent, TotemRotateLabSprites sprites) : base(parent, sprites, "Gizmo_M2_Veil")
    {
        _veil = AddImage("Veil", Root, sprites.Disc, Vector2.one * Radius * 2f, TotemRotateLabPalette.Veil);
        _edge = AddImage("Edge", Root, sprites.ThinRing, Vector2.one * Radius * 2f, TotemRotateLabPalette.Hairline);
        float side = DiamondHalfDiagonal * Mathf.Sqrt(2f);
        var diamond = AddImage("Diamond", Root, sprites.Frame, Vector2.one * side, TotemRotateLabPalette.WithAlpha(Color.white, 0.5f));
        diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        for (int i = 0; i < 4; i++)
            AddImage("Vertex_" + i, Root, sprites.Disc, Vector2.one * 12f, TotemRotateLabPalette.Hairline).rectTransform.anchoredPosition = Directions[i] * DiamondHalfDiagonal;

        _wedgePivot = NewRect("WedgePivot", Root, Vector2.zero);
        _wedge = _wedgePivot.gameObject.AddComponent<CanvasGroup>();
        var wedge = AddImage("Wedge", _wedgePivot, sprites.Triangle, new Vector2(DiamondHalfDiagonal * 1.1f, DiamondHalfDiagonal * 0.92f), TotemRotateLabPalette.WithAlpha(Color.white, 0.4f));
        wedge.rectTransform.anchoredPosition = Vector2.up * (DiamondHalfDiagonal * 0.52f);
        var tip = AddImage("Tip", _wedgePivot, sprites.Disc, Vector2.one * 22f, TotemRotateLabPalette.Accent);
        tip.rectTransform.anchoredPosition = Vector2.up * DiamondHalfDiagonal;

        _handle = AddImage("Handle", Root, sprites.Disc, Vector2.one * 78f, TotemRotateLabPalette.WithAlpha(Color.white, 0.92f));
        AddImage("HandleRim", _handle.transform, sprites.ThinRing, Vector2.one * 96f, TotemRotateLabPalette.Hairline);
        _cross = AddCancelCross();
    }

    /// <inheritdoc/>
    protected override void OnShow()
    {
        _fade?.Kill();
        _wedge.alpha = 0f;
        _visible = false;
        _handle.rectTransform.anchoredPosition = Vector2.zero;
        SetOutside(false, true);
    }

    /// <inheritdoc/>
    public override void SetPointer(Vector2 delta)
    {
        _handle.rectTransform.anchoredPosition = Vector2.ClampMagnitude(delta, HandleReach);
        SetOutside(delta.magnitude > CancelRadius);
    }

    private void SetOutside(bool outside, bool force = false)
    {
        if (_outside == outside && !force) return;
        _outside = outside;
        _cross.SetActive(Candidate < 0 && !outside);
        _edge.color = outside ? CancelColor : TotemRotateLabPalette.Hairline;
        _veil.color = outside ? TotemRotateLabPalette.WithAlpha(CancelColor, 0.12f) : TotemRotateLabPalette.Veil;
        _handle.color = outside ? CancelColor : TotemRotateLabPalette.WithAlpha(Color.white, 0.92f);
    }

    /// <inheritdoc/>
    protected override void OnCandidateChanged(int candidate)
    {
        _cross.SetActive(candidate < 0 && !_outside);
        _fade?.Kill();
        if (candidate < 0)
        {
            _visible = false;
            _fade = _wedge.DOFade(0f, 0.1f).SetUpdate(true);
            return;
        }
        var target = new Vector3(0f, 0f, AngleOf(candidate));
        _spin?.Kill();
        if (!_visible) _wedgePivot.localRotation = Quaternion.Euler(target);
        else _spin = _wedgePivot.DOLocalRotate(target, 0.16f, RotateMode.Fast).SetEase(Ease.OutCubic).SetUpdate(true);
        _visible = true;
        _fade = _wedge.DOFade(1f, 0.1f).SetUpdate(true);
    }

    /// <inheritdoc/>
    protected override void OnHide()
    {
        _spin?.Kill();
        _fade?.Kill();
        _visible = false;
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        _spin?.Kill();
        _fade?.Kill();
        base.Dispose();
    }
}
