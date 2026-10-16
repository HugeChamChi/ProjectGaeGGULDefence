using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>네 버튼 대신 선택 방향에 한 장의 반투명 면이 펼쳐진다.</summary>
public sealed class FoldRotateGizmo : ModernRotateGizmo
{
    private const float FaceReach = 110f;
    private readonly RectTransform _fold;
    private readonly Image _face;
    /// <inheritdoc/>
    public override string Title => "N2 접힌 면";
    /// <inheritdoc/>
    public override string Description => "한 장의 반투명 종이가 선택 쪽으로 펼쳐짐. 실제 블러 없이 면·접힌 가장자리만 사용.";
    /// <summary>종이 면과 접힌 가장자리를 생성한다.</summary>
    public FoldRotateGizmo(RectTransform parent, TotemRotateLabSprites sprites, TMP_FontAsset font) : base(parent, sprites, font, "N2_Fold")
    {
        _fold = NewRect("Fold", Root, Vector2.zero);
        _face = AddImage("TranslucentFace", _fold, null, new Vector2(190f, 126f), new Color(.93f, .86f, .64f, .68f));
        _face.rectTransform.anchoredPosition = Vector2.up * FaceReach;
        var edge = AddImage("FoldedEdge", _fold, null, new Vector2(190f, 22f), Paper);
        edge.rectTransform.anchoredPosition = Vector2.up * (FaceReach + 52f);
        var seam = AddImage("Seam", _fold, null, new Vector2(190f, 2f), Ink);
        seam.rectTransform.anchoredPosition = Vector2.up * (FaceReach + 40f);
    }
    /// <inheritdoc/>
    protected override void OnCandidateChanged(int candidate)
    {
        State(candidate); _fold.gameObject.SetActive(candidate >= 0);
        if (candidate >= 0) { _fold.localRotation = Quaternion.Euler(0, 0, AngleOf(candidate)); _fold.localScale = new Vector3(1f, .35f, 1f); }
    }
    /// <inheritdoc/>
    public override void Tick(float unscaledTime)
    {
        base.Tick(unscaledTime);
        _fold.localScale = Vector3.Lerp(_fold.localScale, Vector3.one, 1f - Mathf.Exp(-MotionRate * Time.unscaledDeltaTime));
    }
}
