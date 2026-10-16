using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A1 궤도 링: 토템 둘레 얇은 링 + 4방향 눈금 위로, 고른 방향의 90° 금색 호와 꺾쇠가 스프링처럼 튕기며 따라간다.
/// 가운데 점선 원 안으로 돌아오면 호가 꺼지고 빨간 ×가 뜬다.
/// </summary>
public sealed class OrbitRotateGizmo : TotemRotateLabGizmo
{
    private const float Radius = 228f;
    private const float Dead = 66f;
    private static readonly Color Gold = new Color(1f, 0.79f, 0.24f, 1f);

    private readonly RectTransform _arcPivot;
    private readonly CanvasGroup _arcGroup;
    private readonly GameObject _cross;
    private Tween _spin, _fade;
    private bool _arcVisible;

    /// <inheritdoc/>
    public override float DeadZone => Dead;
    /// <inheritdoc/>
    public override string Title => "A1 궤도 링";
    /// <inheritdoc/>
    public override string Description => "빛나는 링 위로 90° 금색 호가 튕기며 따라감. 깔끔·트렌디, 링 반경이 손가락 밖. Image(Filled Radial360) 1장 + 회전 트윈.";

    /// <summary>링·눈금·호·꺾쇠·취소 표시를 만든다.</summary>
    public OrbitRotateGizmo(RectTransform parent, TotemRotateLabSprites sprites) : base(parent, sprites, "Gizmo_A_Orbit")
    {
        AddImage("GlowBand", Root, sprites.Ring, Vector2.one * (Radius * 2f + 40f), new Color(1f, 1f, 1f, 0.16f));
        AddImage("Line", Root, sprites.ThinRing, Vector2.one * Radius * 2f, new Color(1f, 1f, 1f, 0.85f));
        for (int i = 0; i < 4; i++)
        {
            var tick = AddImage("Tick_" + i, Root, sprites.RoundRect, new Vector2(10f, 46f), new Color(1f, 1f, 1f, 0.9f));
            tick.rectTransform.anchoredPosition = Directions[i] * Radius;
            tick.rectTransform.localRotation = Quaternion.Euler(0f, 0f, AngleOf(i));
        }
        AddDeadZone(Dead);

        _arcPivot = NewRect("ArcPivot", Root, Vector2.zero);
        _arcGroup = _arcPivot.gameObject.AddComponent<CanvasGroup>();
        var arc = AddImage("Arc", _arcPivot, sprites.Ring, Vector2.one * (Radius * 2f + 44f), Gold);
        arc.type = Image.Type.Filled;
        arc.fillMethod = Image.FillMethod.Radial360;
        arc.fillOrigin = (int)Image.Origin360.Top;
        arc.fillClockwise = true;
        arc.fillAmount = 0.25f;
        arc.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f); // 위 기준 -45°~+45°
        var chevron = AddImage("Chevron", _arcPivot, sprites.Chevron, Vector2.one * 74f, Gold);
        chevron.rectTransform.anchoredPosition = Vector2.up * (Radius + 64f);

        _cross = NewRect("Cancel", Root, Vector2.zero).gameObject;
        for (int i = 0; i < 2; i++)
        {
            var bar = AddImage("Bar_" + i, _cross.transform, sprites.RoundRect, new Vector2(12f, 52f), CancelColor);
            bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
        }
    }

    /// <inheritdoc/>
    protected override void OnCandidateChanged(int candidate)
    {
        _cross.SetActive(candidate < 0);
        _fade?.Kill();
        if (candidate < 0)
        {
            _arcVisible = false;
            _arcGroup.alpha = 0f;
            return;
        }
        var target = new Vector3(0f, 0f, AngleOf(candidate));
        _spin?.Kill();
        if (!_arcVisible)
        {
            _arcPivot.localRotation = Quaternion.Euler(target);
            _arcPivot.localScale = Vector3.one * 0.92f;
            _fade = DOTween.Sequence().SetUpdate(true)
                .Join(_arcGroup.DOFade(1f, 0.1f))
                .Join(_arcPivot.DOScale(1f, 0.22f).SetEase(Ease.OutBack, 2.5f));
            _arcVisible = true;
            return;
        }
        _spin = _arcPivot.DOLocalRotate(target, 0.24f, RotateMode.Fast).SetEase(Ease.OutBack, 1.6f).SetUpdate(true);
    }

    /// <inheritdoc/>
    protected override void OnHide()
    {
        _spin?.Kill();
        _fade?.Kill();
        _arcVisible = false;
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        _spin?.Kill();
        _fade?.Kill();
        base.Dispose();
    }
}
