using UnityEngine;
using UnityEngine.UI;

/// <summary>KHJ 색 면을 둥근 검은 테두리에 담고 원본 안쪽 선을 제외하는 재사용 스킨.</summary>
public sealed class DamageChartSkinView
{
    private const float BorderWidth = 5f;
    private const float OuterSliceScale = 5f;
    private const float InnerSliceScale = 6f;
    private readonly Image _surface, _mask, _fill;
    private bool _active;

    /// <summary>밝기 강조 트윈을 적용할 배경 면.</summary>
    public Image TintTarget => _active ? _fill : _surface;

    /// <summary>배경 이미지의 자식으로 스킨을 한 번 생성한다.</summary>
    public DamageChartSkinView(Image surface, DamageChartUiFactory ui)
    {
        _surface = surface;
        _mask = ui.Image("SkinMask", surface.transform, Vector2.zero, Vector2.one, Color.white);
        _mask.rectTransform.offsetMin = Vector2.one * BorderWidth;
        _mask.rectTransform.offsetMax = -Vector2.one * BorderWidth;
        _mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        _fill = ui.Image("SkinFill", _mask.transform, Vector2.zero, Vector2.one, Color.white);
        _mask.gameObject.SetActive(false);
    }

    /// <summary>스프라이트가 없는 기존 시안은 원래 단색 면으로 복원한다.</summary>
    public void Apply(Sprite fill, Sprite shape, Color tint, float borderWidth = BorderWidth)
    {
        _mask.rectTransform.offsetMin = Vector2.one * borderWidth;
        _mask.rectTransform.offsetMax = -Vector2.one * borderWidth;
        _active = fill != null && shape != null;
        _mask.gameObject.SetActive(_active);
        _surface.sprite = _active ? shape : null;
        _surface.type = _active ? Image.Type.Sliced : Image.Type.Simple;
        _surface.pixelsPerUnitMultiplier = OuterSliceScale;
        _surface.color = _active ? Color.black : tint;
        if (!_active) return;
        _mask.sprite = shape;
        _mask.type = Image.Type.Sliced;
        _mask.pixelsPerUnitMultiplier = InnerSliceScale;
        _fill.sprite = fill;
        _fill.type = Image.Type.Simple;
        _fill.color = tint;
        SendToBack();
    }

    /// <summary>막대와 글자가 배경 면 위에 보이도록 배경만 첫 자식으로 유지한다.</summary>
    public void SendToBack() => _mask.rectTransform.SetAsFirstSibling();
}
