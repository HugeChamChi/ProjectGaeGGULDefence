using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>중앙을 비우고 선택 방향에 두 선이 합쳐지는 시안.</summary>
public sealed class StrokeRotateGizmo : ModernRotateGizmo
{
    private const float Reach = 145f;
    private const float HalfSpan = 65f;
    private readonly RectTransform _stroke;
    private readonly Image[] _lines = new Image[2];
    private float _reveal;
    /// <inheritdoc/>
    public override string Title => "N3 합쳐지는 선";
    /// <inheritdoc/>
    public override string Description => "선택 방향의 두 짧은 획이 하나의 밑줄로 닫힘. 원·바늘·손가락 연결선 없음.";
    /// <summary>양 끝에서 들어오는 선 두 개를 생성한다.</summary>
    public StrokeRotateGizmo(RectTransform parent, TotemRotateLabSprites sprites, TMP_FontAsset font) : base(parent, sprites, font, "N3_Stroke")
    {
        _stroke = NewRect("Stroke", Root, Vector2.zero);
        for (int i = 0; i < 2; i++) _lines[i] = AddImage("Stroke" + i, _stroke, null, new Vector2(HalfSpan, 7f), Paper);
    }
    /// <inheritdoc/>
    protected override void OnCandidateChanged(int candidate)
    {
        State(candidate); _stroke.gameObject.SetActive(candidate >= 0); _reveal = 0f;
        if (candidate >= 0) _stroke.localRotation = Quaternion.Euler(0, 0, AngleOf(candidate));
    }
    /// <inheritdoc/>
    public override void Tick(float unscaledTime)
    {
        base.Tick(unscaledTime);
        _reveal = Mathf.MoveTowards(_reveal, 1f, Time.unscaledDeltaTime * 5f);
        for (int i = 0; i < 2; i++) _lines[i].rectTransform.anchoredPosition = new Vector2((i == 0 ? -1f : 1f) * (HalfSpan * .5f + (1f - _reveal) * 40f), Reach);
    }
}
