using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>4차 시안의 손가락 회피 상태 라벨과 공통 색. 판정은 실험실이 소유한다.</summary>
public abstract class ModernRotateGizmo : TotemRotateLabGizmo
{
    protected const float SafeOffset = 215f;
    protected const float MotionRate = 12f;
    protected static readonly Color Ink = new Color(.18f, .22f, .19f, .96f);
    protected static readonly Color Paper = new Color(.96f, .93f, .81f, 1f);
    protected static readonly Color Accent = new Color(.91f, .72f, .35f, 1f);
    protected static readonly string[] Names = { "위", "오른쪽", "아래", "왼쪽" };
    private readonly TMP_FontAsset _font;
    private readonly TMP_Text _state;
    private Vector2 _pointer;
    /// <inheritdoc/>
    public override float DeadZone => 66f;
    /// <inheritdoc/>
    public override bool ShowsHint => false;
    /// <inheritdoc/>
    protected override bool AnimatesIn => false;
    /// <summary>정보 라벨을 토템 위에 만들고 손가락 접근 시 옆으로 피한다.</summary>
    protected ModernRotateGizmo(RectTransform parent, TotemRotateLabSprites sprites, TMP_FontAsset font, string name)
        : base(parent, sprites, name)
    {
        _font = font;
        var plate = AddImage("State", Root, sprites.RoundRect, new Vector2(300f, 68f), Ink);
        plate.rectTransform.anchoredPosition = Vector2.up * SafeOffset;
        _state = Label(plate.transform, "", Vector2.zero, new Vector2(290f, 64f), 27f);
    }
    /// <inheritdoc/>
    public override void SetPointer(Vector2 delta) => _pointer = delta;
    /// <inheritdoc/>
    public override void Tick(float unscaledTime)
    {
        var target = Vector2.up * SafeOffset;
        if (Mathf.Abs(_pointer.x) < 235f && _pointer.y > 100f)
            target = Vector2.left * SafeOffset + Vector2.up * 40f;
        var rect = (RectTransform)_state.transform.parent;
        rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, target, 1f - Mathf.Exp(-MotionRate * Time.unscaledDeltaTime));
    }
    /// <summary>확정 후보 또는 취소를 손가락 바깥에 표시한다.</summary>
    protected void State(int candidate)
    {
        _state.text = candidate < 0 ? "취소 · 가운데에서 놓기" : Names[candidate] + " · 놓으면 적용";
        _state.color = candidate < 0 ? new Color(1f, .63f, .5f) : Paper;
    }
    /// <summary>공유 폰트로 레이캐스트 없는 글자를 만든다.</summary>
    protected TMP_Text Label(Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var label = NewRect("Label", parent, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.rectTransform.anchoredPosition = position;
        label.font = _font;
        if (_font != null) label.fontSharedMaterial = _font.material;
        label.text = text; label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center; label.color = Paper; label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }
}
