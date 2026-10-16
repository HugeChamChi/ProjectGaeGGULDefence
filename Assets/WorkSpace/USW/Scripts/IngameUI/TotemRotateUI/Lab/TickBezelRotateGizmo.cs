using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// M4 틱 다이얼: 링 대신 60개 눈금(시계 베젤). 손가락 각도를 따라 부드럽게 도는 작은 지시침이 있고,
/// 고른 방향 둘레 눈금들이 가우시안으로 밝아지며 길어진다. 4방향 눈금은 처음부터 길게. 가장 정밀·모던한 느낌.
/// </summary>
public sealed class TickBezelRotateGizmo : TotemRotateLabGizmo
{
    private const int TickCount = 60;
    private const float Radius = 232f;
    private const float Dead = 66f;
    private const float Spread = 28f;      // 밝아지는 범위(도)
    private const float Smoothing = 18f;

    private readonly RectTransform[] _ticks = new RectTransform[TickCount];
    private readonly Image[] _tickImages = new Image[TickCount];
    private readonly RectTransform _pointer;
    private readonly Image _pointerImage;
    private readonly GameObject _cross;
    private float _focus, _focusAngle, _pointerAngle, _targetPointer;
    private bool _hasPointer;

    /// <inheritdoc/>
    public override float DeadZone => Dead;
    /// <inheritdoc/>
    public override string Title => "M4 틱 다이얼";
    /// <inheritdoc/>
    public override string Description => "60개 눈금 베젤. 손가락을 따라 도는 지시침 + 고른 방향 둘레 눈금이 부드럽게 밝아짐. 가장 정밀·모던, 대신 존재감은 약함.";

    /// <summary>눈금과 지시침을 만든다.</summary>
    public TickBezelRotateGizmo(RectTransform parent, TotemRotateLabSprites sprites) : base(parent, sprites, "Gizmo_M4_Tick")
    {
        AddImage("Shade", Root, sprites.Ring, Vector2.one * (Radius * 2f + 70f), TotemRotateLabPalette.GlassLight);
        for (int i = 0; i < TickCount; i++)
        {
            var pivot = NewRect("Tick_" + i, Root, Vector2.zero);
            pivot.localRotation = Quaternion.Euler(0f, 0f, -360f * i / TickCount);
            bool cardinal = i % (TickCount / 4) == 0;
            var tick = AddImage("Mark", pivot, sprites.RoundRect, new Vector2(cardinal ? 7f : 4f, cardinal ? 34f : 18f), TotemRotateLabPalette.Faint);
            tick.type = Image.Type.Simple;
            tick.rectTransform.pivot = new Vector2(0.5f, 0f);
            tick.rectTransform.anchoredPosition = Vector2.up * Radius;
            _ticks[i] = tick.rectTransform;
            _tickImages[i] = tick;
        }
        _pointer = NewRect("Pointer", Root, Vector2.zero);
        _pointerImage = AddImage("Needle", _pointer, sprites.Triangle, new Vector2(30f, 24f), TotemRotateLabPalette.Accent);
        _pointerImage.rectTransform.anchoredPosition = Vector2.up * (Radius - 26f);
        AddDeadZone(Dead);
        _cross = AddCancelCross();
    }

    /// <inheritdoc/>
    protected override void OnShow()
    {
        _focus = 0f;
        _hasPointer = false;
        Refresh();
    }

    /// <inheritdoc/>
    protected override void OnCandidateChanged(int candidate)
    {
        _cross.SetActive(candidate < 0);
        if (candidate >= 0) _focusAngle = 90f * candidate;
    }

    /// <inheritdoc/>
    public override void SetPointer(Vector2 delta)
    {
        if (delta.sqrMagnitude < 1f) return;
        _targetPointer = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg; // 위에서 시계 방향
        if (!_hasPointer) { _pointerAngle = _targetPointer; _hasPointer = true; }
    }

    /// <inheritdoc/>
    public override void Tick(float unscaledTime)
    {
        float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * Smoothing);
        _focus = Mathf.Lerp(_focus, Candidate >= 0 ? 1f : 0f, k);
        _pointerAngle = Mathf.LerpAngle(_pointerAngle, _targetPointer, k);
        Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < TickCount; i++)
        {
            float angle = 360f * i / TickCount;
            float distance = Mathf.Abs(Mathf.DeltaAngle(angle, _focusAngle));
            float glow = _focus * Mathf.Exp(-(distance / Spread) * (distance / Spread));
            bool cardinal = i % (TickCount / 4) == 0;
            var color = Color.Lerp(cardinal ? TotemRotateLabPalette.Hairline : TotemRotateLabPalette.Faint, TotemRotateLabPalette.Active, glow);
            if (cardinal && distance < 1f && _focus > 0.5f) color = TotemRotateLabPalette.Accent;
            _tickImages[i].color = color;
            _ticks[i].localScale = new Vector3(1f + glow * 0.4f, 1f + glow * 1.2f, 1f);
        }
        _pointer.gameObject.SetActive(_hasPointer);
        _pointer.localRotation = Quaternion.Euler(0f, 0f, -_pointerAngle);
        _pointerImage.color = Candidate >= 0 ? TotemRotateLabPalette.Accent : CancelColor;
    }
}
