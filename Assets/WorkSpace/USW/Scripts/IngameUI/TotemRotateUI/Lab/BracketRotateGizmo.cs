using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// M3 조준 브래킷: 링 없이 토템 칸 둘레에 네 모서리 꺾쇠(타깃 고정 HUD)만 두고, 각 변에서 바깥으로 가는 가는 레일 4개.
/// 고른 쪽 레일이 흰색으로 길어지고 끝 화살촉이 민트로 켜지며, 그쪽 꺾쇠가 벌어진다. 전술 HUD처럼 가장 담백한 안.
/// </summary>
public sealed class BracketRotateGizmo : TotemRotateLabGizmo
{
    private const float Half = 112f;      // 꺾쇠 사각형 반 변
    private const float Arm = 42f;        // 꺾쇠 팔 길이
    private const float Thickness = 6f;
    private const float RailStart = Half + 24f;
    private const float RailLength = 90f;
    private const float RailActiveLength = 136f;
    private const float Spread = 14f;
    private const float Dead = 66f;

    private readonly RectTransform[] _corners = new RectTransform[4];
    private readonly Vector2[] _cornerHome = new Vector2[4];
    private readonly RectTransform[] _rails = new RectTransform[4];
    private readonly Image[] _railLines = new Image[4];
    private readonly Image[] _heads = new Image[4];
    private readonly Tween[] _tweens = new Tween[12];
    private readonly GameObject _cross;

    /// <inheritdoc/>
    public override float DeadZone => Dead;
    /// <inheritdoc/>
    public override string Title => "M3 조준 브래킷";
    /// <inheritdoc/>
    public override string Description => "링 없이 칸 둘레 네 모서리 꺾쇠 + 바깥 레일 4개. 고른 쪽 레일이 길어지고 꺾쇠가 벌어짐. 보드를 가장 덜 가리는 전술 HUD.";

    /// <summary>꺾쇠 4개와 레일 4개를 만든다.</summary>
    public BracketRotateGizmo(RectTransform parent, TotemRotateLabSprites sprites) : base(parent, sprites, "Gizmo_M3_Bracket")
    {
        AddImage("Plate", Root, sprites.RoundRect, Vector2.one * Half * 2f, TotemRotateLabPalette.GlassLight);
        for (int i = 0; i < 4; i++)
        {
            // 모서리: 0 오른쪽 위, 1 오른쪽 아래, 2 왼쪽 아래, 3 왼쪽 위.
            var sign = new Vector2(i < 2 ? 1f : -1f, i == 0 || i == 3 ? 1f : -1f);
            var corner = NewRect("Corner_" + i, Root, Vector2.zero);
            _cornerHome[i] = sign * Half;
            corner.anchoredPosition = _cornerHome[i];
            Bar(corner, new Vector2(Arm, Thickness), new Vector2(-sign.x * (Arm - Thickness) * 0.5f, 0f));
            Bar(corner, new Vector2(Thickness, Arm), new Vector2(0f, -sign.y * (Arm - Thickness) * 0.5f));
            _corners[i] = corner;

            var rail = NewRect("Rail_" + i, Root, Vector2.zero);
            rail.localRotation = Quaternion.Euler(0f, 0f, AngleOf(i));
            _railLines[i] = AddImage("Line", rail, sprites.RoundRect, new Vector2(4f, RailLength), TotemRotateLabPalette.Faint);
            _railLines[i].type = Image.Type.Simple;
            _railLines[i].rectTransform.pivot = new Vector2(0.5f, 0f);
            _railLines[i].rectTransform.anchoredPosition = Vector2.up * RailStart;
            _heads[i] = AddImage("Head", rail, sprites.Chevron, Vector2.one * 40f, TotemRotateLabPalette.Faint);
            _heads[i].rectTransform.anchoredPosition = Vector2.up * (RailStart + RailLength);
            _rails[i] = rail;
        }
        AddDeadZone(Dead);
        _cross = AddCancelCross();
    }

    private void Bar(RectTransform parent, Vector2 size, Vector2 position)
    {
        var bar = AddImage("Bar", parent, Sprites.RoundRect, size, TotemRotateLabPalette.Active);
        bar.type = Image.Type.Simple;
        bar.rectTransform.anchoredPosition = position;
    }

    /// <inheritdoc/>
    protected override void OnCandidateChanged(int candidate)
    {
        _cross.SetActive(candidate < 0);
        foreach (var tween in _tweens) tween?.Kill();
        for (int i = 0; i < 4; i++)
        {
            bool on = i == candidate;
            _railLines[i].color = on ? TotemRotateLabPalette.Active : TotemRotateLabPalette.Faint;
            _heads[i].color = on ? TotemRotateLabPalette.Accent : TotemRotateLabPalette.Hairline;
            float length = on ? RailActiveLength : RailLength;
            _tweens[i] = _railLines[i].rectTransform.DOSizeDelta(new Vector2(on ? 6f : 4f, length), 0.16f).SetEase(Ease.OutCubic).SetUpdate(true);
            _tweens[4 + i] = _heads[i].rectTransform.DOAnchorPos(Vector2.up * (RailStart + length), 0.16f).SetEase(Ease.OutCubic).SetUpdate(true);

            // 고른 방향 쪽 두 모서리만 그 방향으로 벌어진다.
            var corner = _cornerHome[i];
            var push = candidate >= 0 && Vector2.Dot(corner.normalized, Directions[candidate]) > 0f ? Directions[candidate] * Spread : Vector2.zero;
            _tweens[8 + i] = _corners[i].DOAnchorPos(corner + push, 0.16f).SetEase(Ease.OutBack, 2f).SetUpdate(true);
        }
    }

    /// <inheritdoc/>
    protected override void OnHide()
    {
        foreach (var tween in _tweens) tween?.Kill();
        for (int i = 0; i < 4; i++) _corners[i].anchoredPosition = _cornerHome[i];
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        foreach (var tween in _tweens) tween?.Kill();
        base.Dispose();
    }
}
