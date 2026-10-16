using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>원본 토템 아트의 밝은 잔상이 선택 방향으로 겹쳐지는 시안.</summary>
public sealed class EchoRotateGizmo : ModernRotateGizmo
{
    private const float EchoReach = 105f;
    private readonly Image _echo;
    /// <inheritdoc/>
    public override string Title => "N5 토템 잔상";
    /// <inheritdoc/>
    public override string Description => "토템 자체의 단색 잔상이 방향을 예고. 별도의 기계적 HUD 대신 카툰 실루엣을 사용.";
    /// <summary>현재 토템 아이콘을 공유하는 잔상을 만든다.</summary>
    public EchoRotateGizmo(RectTransform parent, TotemRotateLabSprites sprites, TMP_FontAsset font) : base(parent, sprites, font, "N5_Echo")
    {
        _echo = AddImage("TotemEcho", Root, null, new Vector2(130f, 140f), new Color(Paper.r, Paper.g, Paper.b, .55f));
        _echo.preserveAspect = true;
    }
    /// <summary>범위를 바꿀 때 원본 토템 아이콘도 함께 바꾼다.</summary>
    public void SetIcon(Sprite icon) => _echo.sprite = icon;
    /// <inheritdoc/>
    protected override void OnCandidateChanged(int candidate)
    {
        State(candidate); _echo.gameObject.SetActive(candidate >= 0);
        if (candidate >= 0) _echo.rectTransform.anchoredPosition = Directions[candidate] * EchoReach;
    }
}
