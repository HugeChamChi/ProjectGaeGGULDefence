using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// M5 글래스 칩: 4방향에 반투명 유리 알약 버튼(가는 흰 테두리 + 화살표). 가운데에서 각 칩까지 희미한 연결선.
/// 고른 칩만 불투명 흰색으로 차오르고 화살표가 어둡게 반전된다(iOS 제어 센터 느낌).
/// </summary>
public sealed class GlassChipRotateGizmo : TotemRotateLabGizmo
{
    private const float Distance = 226f;
    private const float Dead = 66f;
    private static readonly Vector2 ChipSize = new Vector2(112f, 84f);

    private readonly RectTransform[] _chips = new RectTransform[4];
    private readonly Image[] _fills = new Image[4];
    private readonly Image[] _arrows = new Image[4];
    private readonly Image[] _links = new Image[4];
    private readonly Tween[] _pops = new Tween[4];
    private readonly GameObject _cross;

    /// <inheritdoc/>
    public override float DeadZone => Dead;
    /// <inheritdoc/>
    public override string Title => "M5 글래스 칩";
    /// <inheritdoc/>
    public override string Description => "4방향 반투명 유리 알약 + 가는 테두리. 고른 칩만 흰색으로 차고 화살표가 반전. 버튼처럼 읽혀 처음 보는 사람도 바로 이해.";

    /// <summary>칩 4개와 연결선을 만든다.</summary>
    public GlassChipRotateGizmo(RectTransform parent, TotemRotateLabSprites sprites) : base(parent, sprites, "Gizmo_M5_Chip")
    {
        for (int i = 0; i < 4; i++)
        {
            var link = NewRect("Link_" + i, Root, Vector2.zero);
            link.localRotation = Quaternion.Euler(0f, 0f, AngleOf(i));
            _links[i] = AddImage("Line", link, sprites.RoundRect, new Vector2(3f, Distance - ChipSize.y * 0.5f - Dead - 8f), TotemRotateLabPalette.Faint);
            _links[i].type = Image.Type.Simple;
            _links[i].rectTransform.pivot = new Vector2(0.5f, 0f);
            _links[i].rectTransform.anchoredPosition = Vector2.up * (Dead + 4f);

            var chip = NewRect("Chip_" + i, Root, ChipSize);
            chip.anchoredPosition = Directions[i] * Distance;
            _fills[i] = AddImage("Glass", chip, sprites.RoundRect, ChipSize, TotemRotateLabPalette.Glass);
            AddImage("Rim", chip, sprites.Frame, ChipSize, TotemRotateLabPalette.Hairline);
            _arrows[i] = AddImage("Arrow", chip, sprites.Chevron, Vector2.one * 40f, TotemRotateLabPalette.Active);
            _arrows[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, AngleOf(i));
            _chips[i] = chip;
        }
        AddDeadZone(Dead);
        _cross = AddCancelCross();
    }

    /// <inheritdoc/>
    protected override void OnCandidateChanged(int candidate)
    {
        _cross.SetActive(candidate < 0);
        for (int i = 0; i < 4; i++)
        {
            bool on = i == candidate;
            _fills[i].color = on ? TotemRotateLabPalette.Active : TotemRotateLabPalette.Glass;
            _arrows[i].color = on ? TotemRotateLabPalette.Ink : TotemRotateLabPalette.WithAlpha(Color.white, candidate >= 0 ? 0.55f : 0.9f);
            _links[i].color = on ? TotemRotateLabPalette.WithAlpha(Color.white, 0.8f) : TotemRotateLabPalette.Faint;
            _pops[i]?.Kill();
            _pops[i] = _chips[i].DOScale(on ? 1.12f : 1f, 0.16f).SetEase(on ? Ease.OutBack : Ease.OutQuad, 2.4f).SetUpdate(true);
        }
    }

    /// <inheritdoc/>
    protected override void OnHide()
    {
        for (int i = 0; i < 4; i++) { _pops[i]?.Kill(); _chips[i].localScale = Vector3.one; }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        for (int i = 0; i < 4; i++) _pops[i]?.Kill();
        base.Dispose();
    }
}
