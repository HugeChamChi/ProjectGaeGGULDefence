using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 토템 회전 UI·카메라 시안 실험실(FxLab_TotemRotateUI) — 사용자 10-15 "회전 UI가 그냥 마름모라 애매, 더 트렌디하게 + 그때 시점/카메라도".
/// 인게임과 같은 배경·칸 위치·원근 카메라·범위 머티리얼·홀드 게이지로 보드를 다시 세우고,
/// 10-15 4차: 회전 UI 7종(현재 + A1 + N1~N5) × 카메라 2종(포커스 줌/스포트라이트 확정)을
/// 버튼으로 바꿔 직접 눌러(0.45초 홀드 → 끌기) 비교한다. M1~M5는 버튼에서 제외했다.
/// 실제 TotemRotationUI·DragHandler는 건드리지 않는다. 시간 느려짐은 이 실험실의 표시용 시계에만 적용한다(Time.timeScale 미사용).
/// </summary>
public sealed class TotemRotateUILab : MonoBehaviour, IFxLabPlayable
{
    /// <summary>회전 UI 버튼 이름.</summary>
    public static readonly string[] StyleLabels = { "현재", "A1 궤도", "N4 범위", "R1 사선", "R2 스윕", "R3 전후", "R4 흐름", "R5 웨이브" };
    /// <summary>카메라 버튼 이름.</summary>
    public static readonly string[] CameraLabels = { "포커스 줌", "스포트라이트" };
    private static readonly string[] CameraDescriptions =
    {
        "토템을 고정점으로 1.3배 당김 + 비네트. 토템이 손가락 밑에서 안 움직임. Camera 위치 트윈만으로 구현.",
        "토템 주변만 남기고 화면을 어둡게. 카메라는 그대로라 방향감 유지. 풀스크린 Image 1장."
    };
    private static readonly string[] DirectionNames = { "위", "오른쪽", "아래", "왼쪽" };
    private static readonly Vector2[] DirectionVectors = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
    private const float SpotlightHoleCells = 2.2f;

    private const int Columns = 6;
    private const int Rows = 4;
    // IngameScene "/// Map/bg/Grid_Root" 칸 월드 위치(10-15 기준). 행 0이 아래.
    private static readonly float[] ColumnX = { -2.70f, -1.62f, -0.53f, 0.55f, 1.64f, 2.73f };
    private static readonly float[] RowY = { -3.03f, -1.94f, -0.85f, 0.24f };
    private const float CellZ = -0.02f;
    private const float CellWorld = 1.08f;
    private static readonly Vector3 BackgroundPosition = new Vector3(0f, 0.88f, 0f);
    private const float MapScale = 0.8f;
    private static readonly Vector2Int TotemCell = new Vector2Int(2, 1);
    private static readonly Vector2Int[] UnitCells = { new Vector2Int(0, 3), new Vector2Int(4, 2), new Vector2Int(1, 0), new Vector2Int(5, 1), new Vector2Int(2, 2), new Vector2Int(3, 0) };
    private static readonly Color DisabledTint = new Color(0.1f, 0.1f, 0.5f, 1f);
    private static readonly Color FallbackTint = new Color(1f, 0.5f, 0.05f, 1f);
    private static readonly int StripeColorId = Shader.PropertyToID("_StripeColor");
    private static readonly int FillColorId = Shader.PropertyToID("_FillColor");

    private static readonly Color PanelColor = new Color(0.05f, 0.08f, 0.11f, 0.9f);
    private static readonly Color ButtonColor = new Color(0.13f, 0.19f, 0.24f, 0.97f);
    private static readonly Color SelectedColor = new Color(0.78f, 0.55f, 0.12f, 1f);
    private static readonly Color TextColor = new Color(0.94f, 0.96f, 0.95f, 1f);

    [Header("인게임 에셋 참조 (빌더가 연결)")]
    [SerializeField] private Camera _camera;
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private Sprite _background;
    [SerializeField] private Sprite _cellOverlay;
    [SerializeField] private Material _rangeMaterial;
    [SerializeField] private Material _holdRingMaterial;
    [SerializeField] private TotemData[] _totems = System.Array.Empty<TotemData>();
    [SerializeField] private Sprite[] _unitSprites = System.Array.Empty<Sprite>();

    [Header("조작 (TotemInteractionSettings 값과 같게)")]
    [SerializeField, Min(0.1f)] private float _holdSeconds = 0.45f;
    [SerializeField, Min(0f)] private float _feedbackDelaySeconds = 0.2f;
    [SerializeField, Range(0.1f, 1f)] private float _holdSlowScale = 0.2f;
    [SerializeField, Min(0.01f)] private float _slowRampSeconds = 0.12f;
    [SerializeField, Min(1f)] private float _originalDeadZonePixels = 24f;
    [SerializeField, Min(1f)] private float _moveCancelCanvas = 40f;
    [SerializeField, Min(0.1f)] private float _holdRingWorldSize = 2.2f;
    [SerializeField, Min(0f)] private float _keepRangeAfterCommitSeconds = 0.6f;

    [Header("보드 표시")]
    [Tooltip("인게임 토템 Visual 3.6 × spawnScale 0.6 × 칸 0.8")]
    [SerializeField, Min(0.01f)] private float _totemScale = 1.728f;
    [SerializeField, Min(0.1f)] private float _unitWorldHeight = 0.95f;
    [SerializeField, Min(0f)] private float _bobHeight = 0.05f;

    [Header("카메라 시안")]
    [SerializeField, Range(1f, 2f)] private float _focusZoom = 1.3f;

    private enum Phase { Idle, Holding, Armed }

    private TotemRotateLabSprites _sprites;
    private TotemRotateLabCamera _cameraRig;
    private TotemHoldRingView _ring;
    private readonly SpriteRenderer[,] _cells = new SpriteRenderer[Columns, Rows];
    private Material _cellDefaultMaterial;
    private MaterialPropertyBlock _block;
    private readonly List<(Transform transform, Vector3 basePosition, float phase)> _standing = new List<(Transform, Vector3, float)>();
    private SpriteRenderer _totem;
    private Transform _worldRoot;
    private readonly List<(string name, List<Vector2Int> effect, List<Vector2Int> disabled, Color tint, Sprite sprite)> _shapes =
        new List<(string, List<Vector2Int>, List<Vector2Int>, Color, Sprite)>();
    private readonly List<Vector2> _projectedRange = new List<Vector2>(Columns * Rows);

    private Canvas _canvas;
    private RectTransform _gizmoLayer;
    private TotemRotateLabGizmo[] _gizmos;
    private TotemRotateLabRangeView _rangeView;
    private Image _spotlight, _vignette, _slowTint, _burst, _finger;
    private RectTransform _hint;
    private TMP_Text _hintText, _toast, _slowChip, _description, _status;
    private Button[] _styleButtons, _cameraButtons;
    private Button _shapeButton, _demoButton, _fingerButton;
    private Tween _toastTween, _burstTween, _totemPunch;

    private int _style = 1;
    private int _cameraMode = TotemRotateLabCamera.Focus;
    private int _shape;
    private int _facing;
    private int _candidate = -1;
    private Phase _phase;
    private float _pressAt, _armedAt = -1f, _keepRangeUntil = -1f;
    private Vector2 _pressScreen;
    private bool _pointerWasDown, _ignoreGesture, _outside;
    private bool _demo = true, _showFinger = true, _unscaled = true;
    private float _demoStart;
    private bool _posing;
    private int _poseDirection;
    private float _labTime, _labScale = 1f;

    /// <summary>현재 회전 UI 시안 인덱스.</summary>
    public int StyleIndex => _style;
    /// <summary>현재 카메라 시안 인덱스.</summary>
    public int CameraIndex => _cameraMode;
    /// <summary>현재 확정된 방향(0 위·1 오른쪽·2 아래·3 왼쪽).</summary>
    public int Facing => _facing;
    /// <summary>이 실험실의 표시 시계 선택.</summary>
    public bool UseUnscaledTime { get => _unscaled; set => _unscaled = value; }
    private float Now => _unscaled ? Time.unscaledTime : Time.time;
    private float Delta => _unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
    private TotemRotateLabGizmo Gizmo => _gizmos[_style];

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;
        _sprites = new TotemRotateLabSprites();
        _block = new MaterialPropertyBlock();
        BuildShapes();
        BuildWorld();
        BuildUi();
        _cameraRig = new TotemRotateLabCamera(_camera) { FocusZoom = _focusZoom };
        _ring = new TotemHoldRingView(_holdRingMaterial, _holdRingWorldSize);
        SelectShape(0);
        SelectStyle(_style);
        SelectCamera(_cameraMode);
        Play();
    }

    private void OnDestroy()
    {
        _toastTween?.Kill(); _burstTween?.Kill(); _totemPunch?.Kill();
        if (_gizmos != null) foreach (var gizmo in _gizmos) gizmo?.Dispose();
        _ring?.Destroy();
        _cameraRig?.Restore();
        _rangeView?.Dispose();
        _sprites?.Dispose();
    }

    /// <summary>데모를 처음부터 재생한다.</summary>
    public void Play()
    {
        _posing = false;
        _demo = true;
        _demoStart = Now;
        ResetGesture();
        RefreshToggles();
    }

    /// <summary>회전 UI 시안을 고른다.</summary>
    public void SelectStyle(int index)
    {
        if (index < 0 || index >= _gizmos.Length) return;
        bool armed = _phase == Phase.Armed;
        Gizmo.Hide();
        _style = index;
        if (armed) { Gizmo.Show(); Gizmo.SetCandidate(_candidate); }
        Highlight(_styleButtons, _style);
        RefreshDescription();
    }

    /// <summary>카메라 시안을 고른다.</summary>
    public void SelectCamera(int index)
    {
        if (index < 0 || index >= CameraLabels.Length) return;
        _cameraMode = index;
        Highlight(_cameraButtons, _cameraMode);
        RefreshDescription();
    }

    /// <summary>토템 범위 모양(실제 토템 / 예시 T자)을 순서대로 바꾼다.</summary>
    public void NextShape() => SelectShape((_shape + 1) % _shapes.Count);

    /// <summary>데모 재생과 직접 조작을 전환한다.</summary>
    public void ToggleDemo()
    {
        if (_demo || _posing) { _demo = false; _posing = false; ResetGesture(); RefreshToggles(); }
        else Play();
    }

    /// <summary>캡처용: 데모를 멈추고 토템을 누른 채 direction(-1이면 가운데 취소 영역)으로 끌어 둔 상태를 유지한다.</summary>
    public void PoseForCapture(int direction)
    {
        _demo = false;
        ResetGesture();
        _pointerWasDown = false;
        _posing = true;
        _poseDirection = Mathf.Clamp(direction, -1, 3);
        RefreshToggles();
    }

    /// <summary>손가락 크기 표시(약 9mm)를 켜고 끈다.</summary>
    public void ToggleFinger() { _showFinger = !_showFinger; RefreshToggles(); }

    private void SelectShape(int index)
    {
        _shape = Mathf.Clamp(index, 0, _shapes.Count - 1);
        var shape = _shapes[_shape];
        if (_totem != null && shape.sprite != null) _totem.sprite = shape.sprite;
        foreach (var gizmo in _gizmos) gizmo.SetShape(shape.effect, shape.disabled);
        foreach (var gizmo in _gizmos)
            if (gizmo is EchoRotateGizmo echo) echo.SetIcon(_totem.sprite);
        _facing = 0;
        ResetGesture();
        if (_shapeButton != null) _shapeButton.GetComponentInChildren<TMP_Text>().text = "범위: " + shape.name;
    }

    // ───────────────────────────── 보드 ─────────────────────────────

    private void BuildShapes()
    {
        foreach (var data in _totems)
        {
            if (data == null) continue;
            var tint = data.HasEffectGroups ? data.EffectGroups[0].Color : FallbackTint;
            _shapes.Add((data.name.Replace("Data", ""), data.GetEffectPreviewOffsets(), data.GetAttackDisabledPreviewOffsets(), tint, data.icon));
        }
        // 실제 회전 가능 토템은 범위가 1~2칸뿐이라, 회전 결과를 보기 쉬운 예시 모양을 하나 둔다.
        _shapes.Add(("예시 T자", new List<Vector2Int> { new Vector2Int(0, 1), new Vector2Int(-1, 1), new Vector2Int(1, 1), new Vector2Int(0, 2) },
            new List<Vector2Int> { new Vector2Int(0, -1) }, FallbackTint, null));
    }

    private void BuildWorld()
    {
        _worldRoot = new GameObject("LabWorld").transform;
        var bg = NewSprite("Battlefield_BG", _worldRoot, _background, "Default", 0);
        bg.transform.position = BackgroundPosition;
        bg.transform.localScale = Vector3.one * MapScale;

        for (int c = 0; c < Columns; c++)
        for (int r = 0; r < Rows; r++)
        {
            var cell = NewSprite($"GridCell_{c}_{r}", _worldRoot, _cellOverlay, "Default", 1);
            cell.transform.position = CellCenter(c, r);
            cell.transform.localScale = Vector3.one * MapScale;
            cell.color = new Color(1f, 1f, 1f, 0f);
            _cells[c, r] = cell;
        }
        _cellDefaultMaterial = _cells[0, 0].sharedMaterial;
        _rangeView = new TotemRotateLabRangeView(_cells, _cellDefaultMaterial, _rangeMaterial, _sprites.Pixel, CellCenter, TotemCell, CellWorld, _worldRoot);

        for (int i = 0; i < UnitCells.Length && _unitSprites.Length > 0; i++)
        {
            var cellPos = UnitCells[i];
            var sprite = _unitSprites[i % _unitSprites.Length];
            var unit = NewSprite("Unit_" + sprite.name, _worldRoot, sprite, "Unit", (Rows - cellPos.y) * 10);
            float height = sprite.bounds.size.y;
            unit.transform.localScale = Vector3.one * (height > 0f ? _unitWorldHeight / height : 1f);
            unit.transform.position = CellCenter(cellPos.x, cellPos.y) + Vector3.back * 0.05f;
            _standing.Add((unit.transform, unit.transform.position, i * 1.7f));
        }

        _totem = NewSprite("Totem", _worldRoot, _shapes.Count > 0 ? _shapes[0].sprite : null, "Unit", (Rows - TotemCell.y) * 10);
        _totem.transform.position = TotemWorld + Vector3.back * 0.05f;
        _totem.transform.localScale = Vector3.one * _totemScale;
        _standing.Add((_totem.transform, _totem.transform.position, 0.6f));
    }

    private static SpriteRenderer NewSprite(string name, Transform parent, Sprite sprite, string layer, int order)
    {
        var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(parent, false);
        renderer.sprite = sprite;
        renderer.sortingLayerName = layer;
        renderer.sortingOrder = order;
        return renderer;
    }

    private static Vector3 CellCenter(int column, int row) => new Vector3(ColumnX[column], RowY[row], CellZ);
    private static Vector3 TotemWorld => CellCenter(TotemCell.x, TotemCell.y);
    private static bool InBoard(Vector2Int cell) => cell.x >= 0 && cell.x < Columns && cell.y >= 0 && cell.y < Rows;


    // GridCellView.ApplyPreview와 같은 방식: 공유 범위 머티리얼 + 칸별 줄무늬/채움 색.
    private void PaintRange(int step)
    {
        if (Gizmo is RangeRotateGizmo range)
        {
            var current = _shapes[_shape];
            bool armed = _phase == Phase.Armed;
            _rangeView.Present(range.Style, current.effect, current.disabled, current.tint, _facing, armed ? _candidate : -1, armed, Now);
            return;
        }
        ClearRange();
        var shape = _shapes[_shape];
        foreach (var offset in shape.effect) PaintCell(TotemCell + Rotate(offset, step), shape.tint);
        foreach (var offset in shape.disabled) PaintCell(TotemCell + Rotate(offset, step), DisabledTint);
    }

    private void PaintCell(Vector2Int cell, Color tint)
    {
        if (!InBoard(cell)) return;
        var renderer = _cells[cell.x, cell.y];
        if (Gizmo is CellRotateGizmo)
        {
            renderer.sharedMaterial = _cellDefaultMaterial;
            renderer.color = new Color(tint.r, tint.g, tint.b, 0.62f);
            return;
        }
        if (_rangeMaterial == null) { renderer.color = new Color(tint.r, tint.g, tint.b, 0.6f); return; }
        renderer.sharedMaterial = _rangeMaterial;
        renderer.color = Color.white;
        var stripe = tint; stripe.a *= _rangeMaterial.GetColor(StripeColorId).a;
        var fill = tint; fill.a *= _rangeMaterial.GetColor(FillColorId).a;
        renderer.GetPropertyBlock(_block);
        _block.SetColor(StripeColorId, stripe);
        _block.SetColor(FillColorId, fill);
        renderer.SetPropertyBlock(_block);
    }

    private void ClearRange()
    {
        _rangeView?.Clear();
        foreach (var renderer in _cells)
        {
            renderer.sharedMaterial = _cellDefaultMaterial;
            renderer.SetPropertyBlock(null);
            renderer.color = new Color(1f, 1f, 1f, 0f);
        }
    }

    // TotemBase.RotateOffset와 같은 90° 시계 방향 회전.
    private static Vector2Int Rotate(Vector2Int offset, int steps)
    {
        for (int i = 0; i < steps; i++) offset = new Vector2Int(offset.y, -offset.x);
        return offset;
    }

    // ───────────────────────────── 입력 ─────────────────────────────

    private void Update()
    {
        float now = Now, dt = Delta;
        Vector2 totemScreen = _camera.WorldToScreenPoint(TotemWorld);
        float scale = _canvas.scaleFactor;

        bool down; Vector2 pointer; bool overUi = false;
        if (_posing)
        {
            down = true;
            // 첫 프레임은 토템 위를 누르고, 다음 프레임부터 목표 방향으로 끈 위치를 유지한다.
            bool dragged = _pointerWasDown && _poseDirection >= 0;
            pointer = totemScreen + (dragged ? DirectionVectors[_poseDirection] * 250f : Vector2.zero) * scale;
        }
        else if (_demo) DemoPointer(now - _demoStart, totemScreen, scale, out down, out pointer);
        else ReadPointer(out down, out pointer, out overUi);

        if (down && !_pointerWasDown)
        {
            OnPointerDown(pointer, totemScreen, overUi, now);
            if (_posing) { _pressAt -= _holdSeconds + 0.05f; _pressScreen = pointer; }
        }
        if (down && !_ignoreGesture) OnPointerHeld(pointer, totemScreen, scale, now);
        if (!down && _pointerWasDown) OnPointerUp(now);
        _pointerWasDown = down;

        if (_phase == Phase.Idle && _keepRangeUntil > 0f && now >= _keepRangeUntil) { _keepRangeUntil = -1f; ClearRange(); }
        TickPresentation(now, dt, pointer, down && !_ignoreGesture, totemScreen, scale);
    }

    private void ReadPointer(out bool down, out Vector2 pointer, out bool overUi)
    {
        overUi = false;
        if (Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            down = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
            pointer = touch.position;
            if (EventSystem.current != null) overUi = EventSystem.current.IsPointerOverGameObject(touch.fingerId);
            return;
        }
        down = Input.GetMouseButton(0);
        pointer = Input.mousePosition;
        if (EventSystem.current != null) overUi = EventSystem.current.IsPointerOverGameObject();
    }

    private void OnPointerDown(Vector2 pointer, Vector2 totemScreen, bool overUi, float now)
    {
        float hitRadius = CellScreenSize() * 0.6f;
        _ignoreGesture = overUi || Vector2.Distance(pointer, totemScreen) > hitRadius;
        if (_ignoreGesture)
        {
            if (!overUi && !_demo) ShowToast("토템을 꾹 눌러보세요");
            return;
        }
        _phase = Phase.Holding;
        _pressAt = now;
        _armedAt = -1f;
        _pressScreen = pointer;
        _candidate = -1;
        _keepRangeUntil = -1f;
        _ring.ResetAppear();
        PaintRange(_facing); // 인게임 BeginPress와 같이 누르는 순간 현재 범위를 보여준다.
    }

    private void OnPointerHeld(Vector2 pointer, Vector2 totemScreen, float scale, float now)
    {
        if (_phase == Phase.Holding)
        {
            if (Vector2.Distance(pointer, _pressScreen) / scale > _moveCancelCanvas)
            {
                ResetGesture();
                ShowToast("게이지가 차기 전에 끌면 '이동'이에요");
                _ignoreGesture = true;
                return;
            }
            if (now - _pressAt >= _holdSeconds) Arm(now);
            return;
        }
        if (_phase != Phase.Armed) return;
        Vector2 delta = (pointer - totemScreen) / scale;
        Gizmo.SetPointer(delta);
        float dead = Gizmo.DeadZone > 0f ? Gizmo.DeadZone : _originalDeadZonePixels / scale;
        int candidate = TotemRotationUI.ResolveDirection(delta, dead);
        _outside = Gizmo.CancelBeyond > 0f && delta.magnitude > Gizmo.CancelBeyond;
        if (_outside) candidate = -1; // 명일방주식: 원 밖에서 놓으면 취소
        if (candidate == _candidate) return;
        _candidate = candidate;
        PaintRange(candidate < 0 ? _facing : candidate);
        Gizmo.SetCandidate(candidate);
    }

    private void Arm(float now)
    {
        _phase = Phase.Armed;
        _armedAt = now;
        _candidate = -1;
        Gizmo.Show();
        _burstTween?.Kill();
        _burst.rectTransform.localScale = Vector3.one * 0.4f;
        _burst.color = new Color(1f, 1f, 1f, 0.9f);
        _burstTween = DOTween.Sequence().SetUpdate(true)
            .Join(_burst.rectTransform.DOScale(2.2f, 0.42f).SetEase(Ease.OutCubic))
            .Join(_burst.DOFade(0f, 0.42f));
    }

    private void OnPointerUp(float now)
    {
        if (_ignoreGesture) { _ignoreGesture = false; return; }
        if (_phase == Phase.Armed)
        {
            if (_candidate >= 0)
            {
                _facing = _candidate;
                PaintRange(_facing);
                _keepRangeUntil = now + _keepRangeAfterCommitSeconds;
                _cameraRig.OnCommitted(_cameraMode);
                _totemPunch?.Kill();
                _totem.transform.localScale = Vector3.one * _totemScale;
                _totemPunch = _totem.transform.DOPunchScale(Vector3.one * _totemScale * 0.14f, 0.3f, 6, 0.6f).SetUpdate(true);
            }
            else ShowToast("회전 취소");
        }
        bool kept = _keepRangeUntil > 0f;
        ResetGesture();
        if (kept) { _keepRangeUntil = now + _keepRangeAfterCommitSeconds; PaintRange(_facing); }
    }

    private void ResetGesture()
    {
        _outside = false;
        _phase = Phase.Idle;
        _candidate = -1;
        _armedAt = -1f;
        _keepRangeUntil = -1f;
        if (_gizmos != null) Gizmo.Hide();
        ClearRange();
    }

    private float CellScreenSize()
    {
        Vector2 a = _camera.WorldToScreenPoint(TotemWorld);
        Vector2 b = _camera.WorldToScreenPoint(TotemWorld + Vector3.right * CellWorld);
        return Vector2.Distance(a, b);
    }

    // 데모: 누름 → 게이지 → 다음 방향으로 끌기 → 놓기. 다섯 번째마다 가운데로 돌아와 취소한다.
    private void DemoPointer(float time, Vector2 totemScreen, float scale, out bool down, out Vector2 pointer)
    {
        const float idle = 0.7f, drag = 0.38f, dwell = 0.75f, back = 0.32f, after = 0.95f;
        float press = _holdSeconds + 0.3f;
        float cycle = idle + press + drag + dwell + after;
        float cancelCycleLength = cycle + back + 0.4f;
        float block = cycle * 4f + cancelCycleLength;
        float t = Mathf.Repeat(time, block);
        int index = Mathf.Min(4, Mathf.FloorToInt(t / cycle));
        bool cancelCycle = index == 4;
        float local = cancelCycle ? t - cycle * 4f : t - index * cycle;
        var dir = DirectionVectors[(_facing + 1) % 4];
        var side = new Vector2(dir.y, -dir.x);
        const float reach = 250f;

        pointer = totemScreen;
        down = false;
        if (local < idle) return;
        local -= idle;
        down = true;
        if (local < press) return;
        local -= press;
        if (local < drag)
        {
            float e = 1f - Mathf.Pow(1f - local / drag, 3f);
            pointer = totemScreen + (dir * reach * e + side * Mathf.Sin(e * Mathf.PI) * 30f) * scale;
            return;
        }
        local -= drag;
        pointer = totemScreen + dir * reach * scale;
        if (local < dwell) return;
        local -= dwell;
        if (cancelCycle)
        {
            if (local < back) { pointer = totemScreen + dir * reach * (1f - local / back) * scale; return; }
            local -= back;
            pointer = totemScreen;
            if (local < 0.4f) return;
        }
        down = false;
    }

    // ───────────────────────────── 표시 ─────────────────────────────

    private void TickPresentation(float now, float dt, Vector2 pointer, bool pressing, Vector2 totemScreen, float scale)
    {
        bool holdingShown = _phase == Phase.Holding && now - _pressAt >= _feedbackDelaySeconds;
        bool slow = holdingShown || _phase == Phase.Armed;
        _labScale = Mathf.MoveTowards(_labScale, slow ? _holdSlowScale : 1f, dt / _slowRampSeconds);
        _labTime += dt * _labScale;

        bool armed = _phase == Phase.Armed;
        _cameraRig.Tick(_cameraMode, armed, TotemWorld, dt);
        foreach (var (t, basePosition, phase) in _standing)
        {
            if (t == null) continue;
            t.position = basePosition + Vector3.up * (Mathf.Sin(_labTime * 3.2f + phase) * 0.5f + 0.5f) * _bobHeight;
        }

        float progress = _phase == Phase.Holding ? Mathf.Clamp01((now - _pressAt - _feedbackDelaySeconds) / Mathf.Max(0.01f, _holdSeconds - _feedbackDelaySeconds)) : 1f;
        bool ringVisible = holdingShown || (armed && now - _armedAt < 0.25f);
        _ring.Tick(ringVisible, progress, armed ? now - _armedAt : -1f, _totem, _totem.transform, now, dt);

        // 카메라가 움직인 뒤의 토템 화면 위치로 UI를 맞춘다.
        totemScreen = _camera.WorldToScreenPoint(TotemWorld);
        Vector2 local = ScreenToLocal(totemScreen);
        Gizmo.Root.anchoredPosition = local;
        _burst.rectTransform.anchoredPosition = local;
        _spotlight.rectTransform.anchoredPosition = local;
        // 스포트라이트 스프라이트는 반지름 0.12까지 투명 → 구멍 반지름(칸 2.2개)이 0.12가 되도록 크기를 잡는다.
        float hole = CellScreenSize() / scale * SpotlightHoleCells;
        _spotlight.rectTransform.sizeDelta = Vector2.one * (hole * 2f / 0.12f);
        Gizmo.Tick(now);
        _rangeView.Tick(now);
        if (Gizmo is CellRotateGizmo cells)
        {
            _projectedRange.Clear();
            if (_candidate >= 0)
                foreach (var offset in _shapes[_shape].effect)
                {
                    var cell = TotemCell + Rotate(offset, _candidate);
                    if (InBoard(cell))
                        _projectedRange.Add(ScreenToLocal(_camera.WorldToScreenPoint(CellCenter(cell.x, cell.y))) - local);
                }
            float cellWidth = CellScreenSize() / scale;
            float cellHeight = Vector2.Distance(_camera.WorldToScreenPoint(TotemWorld),
                _camera.WorldToScreenPoint(TotemWorld + Vector3.up * CellWorld)) / scale;
            cells.SetCells(_projectedRange, new Vector2(cellWidth, cellHeight));
        }

        float spot = armed && _cameraMode == TotemRotateLabCamera.Spotlight ? 0.7f : 0f;
        float vignette = armed && _cameraMode == TotemRotateLabCamera.Focus ? 0.55f : 0f;
        Fade(_spotlight, spot, dt / 0.2f);
        Fade(_vignette, vignette, dt / 0.25f);
        Fade(_slowTint, slow ? 0.45f : 0f, dt / 0.15f);
        _slowChip.alpha = Mathf.MoveTowards(_slowChip.alpha, slow ? 1f : 0f, dt / 0.15f);
        _slowChip.text = "×" + _holdSlowScale.ToString("0.0#") + " SLOW";

        bool hint = armed && Gizmo.ShowsHint;
        _hint.gameObject.SetActive(hint);
        if (hint)
        {
            _hint.anchoredPosition = local + Vector2.up * 360f;
            _hintText.text = _candidate < 0 ? (_outside ? "지금 놓으면 <color=#FF7A6B>취소</color>" : Gizmo.IdleHint)
                : "놓으면 <color=#FFD24A>" + DirectionNames[_candidate] + "</color>쪽으로 회전";
        }

        _finger.gameObject.SetActive(_showFinger && pressing);
        if (_finger.gameObject.activeSelf)
        {
            _finger.rectTransform.anchoredPosition = ScreenToLocal(pointer);
            _finger.rectTransform.localScale = Vector3.one * (pressing ? 0.92f : 1f);
        }
        _status.text = $"확정 방향: <b>{DirectionNames[_facing]}</b>   ·   " +
            (_demo ? "데모 재생 중 (데모 끔 → 직접 조작)" : "토템을 " + _holdSeconds.ToString("0.##") + "초 눌러 게이지가 차면 끌기, 가운데로 돌아오면 취소");
    }

    private static void Fade(Graphic graphic, float target, float step)
    {
        var color = graphic.color;
        color.a = Mathf.MoveTowards(color.a, target, step);
        graphic.color = color;
    }

    private Vector2 ScreenToLocal(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_gizmoLayer, screen, null, out var local);
        return local;
    }

    private void ShowToast(string message)
    {
        _toast.text = message;
        _toastTween?.Kill();
        _toast.alpha = 1f;
        _toastTween = DOTween.Sequence().SetUpdate(true).AppendInterval(1.1f).Append(_toast.DOFade(0f, 0.25f));
    }

    private void RefreshDescription()
    {
        if (_description == null || _gizmos == null) return;
        _description.text = $"<b>{Gizmo.Title}</b>  {Gizmo.Description}\n<b>{CameraLabels[_cameraMode]}</b>  {CameraDescriptions[_cameraMode]}";
    }

    private void RefreshToggles()
    {
        if (_demoButton == null) return;
        SetLabel(_demoButton, _demo ? "데모 끄기" : "데모 보기", _demo);
        SetLabel(_fingerButton, _showFinger ? "손가락 표시 켬" : "손가락 표시 끔", _showFinger);
    }

    // ───────────────────────────── UI 구성 ─────────────────────────────

    private void BuildUi()
    {
        var canvasObject = new GameObject("TotemRotateLabCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        _canvas = canvasObject.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)canvasObject.transform;

        _gizmoLayer = Stretch(NewRect("Overlay", root));
        _spotlight = Centered("Spotlight", _gizmoLayer, _sprites.Spotlight, Vector2.one * 4000f, new Color(0.03f, 0.05f, 0.08f, 0f));
        _vignette = Stretch(NewRect("Vignette", _gizmoLayer)).gameObject.AddComponent<Image>();
        _vignette.sprite = _sprites.Vignette; _vignette.color = new Color(0.04f, 0.06f, 0.1f, 0f); _vignette.raycastTarget = false;
        _slowTint = Stretch(NewRect("SlowTint", _gizmoLayer)).gameObject.AddComponent<Image>();
        _slowTint.sprite = _sprites.Vignette; _slowTint.color = new Color(0.55f, 0.82f, 1f, 0f); _slowTint.raycastTarget = false;
        _burst = Centered("ArmedBurst", _gizmoLayer, _sprites.ThinRing, Vector2.one * 240f, new Color(1f, 1f, 1f, 0f));

        _gizmos = new TotemRotateLabGizmo[]
        {
            new DiamondRotateGizmo(_gizmoLayer, _sprites),
            new OrbitRotateGizmo(_gizmoLayer, _sprites),
            new CellRotateGizmo(_gizmoLayer, _sprites, _font),
            // 5차(10-15): N4 방향 확장 — 토템 주변 UI 없이 범위 칸만으로 방향을 읽게 하는 R1~R5. N1·N2·N3·N5는 버튼에서 뺌(소스 보존).
            new RangeRotateGizmo(_gizmoLayer, _sprites, TotemRotateLabRangeView.Style.OutlineStripes),
            new RangeRotateGizmo(_gizmoLayer, _sprites, TotemRotateLabRangeView.Style.Sweep),
            new RangeRotateGizmo(_gizmoLayer, _sprites, TotemRotateLabRangeView.Style.BeforeAfter),
            new RangeRotateGizmo(_gizmoLayer, _sprites, TotemRotateLabRangeView.Style.Flow),
            new RangeRotateGizmo(_gizmoLayer, _sprites, TotemRotateLabRangeView.Style.Wave)
        };

        _hint = NewRect("Hint", _gizmoLayer);
        _hint.anchorMin = _hint.anchorMax = _hint.pivot = Vector2.one * 0.5f;
        _hint.sizeDelta = new Vector2(720f, 84f);
        var hintBack = _hint.gameObject.AddComponent<Image>();
        hintBack.sprite = _sprites.RoundRect; hintBack.type = Image.Type.Sliced; hintBack.color = new Color(0.08f, 0.11f, 0.16f, 0.88f); hintBack.raycastTarget = false;
        _hintText = Text(_hint, "Label", "", 34f, TextAlignmentOptions.Center);
        _hint.gameObject.SetActive(false);

        _finger = Centered("Finger", _gizmoLayer, _sprites.Disc, Vector2.one * 140f, new Color(1f, 0.93f, 0.84f, 0.42f));
        Centered("Edge", _finger.transform, _sprites.ThinRing, Vector2.one * 140f, new Color(0.45f, 0.3f, 0.2f, 0.6f));
        _finger.gameObject.SetActive(false);

        var toastRect = NewRect("Toast", root);
        toastRect.anchorMin = new Vector2(0.1f, 0.5f); toastRect.anchorMax = new Vector2(0.9f, 0.55f);
        toastRect.offsetMin = toastRect.offsetMax = Vector2.zero;
        _toast = Text(toastRect, "Label", "", 38f, TextAlignmentOptions.Center);
        _toast.outlineWidth = 0.25f; _toast.outlineColor = Color.black; _toast.alpha = 0f;

        var chipRect = NewRect("SlowChip", root);
        chipRect.anchorMin = chipRect.anchorMax = chipRect.pivot = new Vector2(1f, 0.5f);
        chipRect.anchoredPosition = new Vector2(-40f, 0f); chipRect.sizeDelta = new Vector2(280f, 60f);
        _slowChip = Text(chipRect, "Label", "", 32f, TextAlignmentOptions.Right);
        _slowChip.color = new Color(0.74f, 0.9f, 1f, 1f); _slowChip.outlineWidth = 0.2f; _slowChip.outlineColor = Color.black; _slowChip.alpha = 0f;

        // 상단 조작 패널 (하늘·보스 영역 위)
        var panel = NewRect("Panel", root);
        panel.anchorMin = new Vector2(0.02f, 0.735f); panel.anchorMax = new Vector2(0.98f, 0.99f);
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        var panelImage = panel.gameObject.AddComponent<Image>(); panelImage.color = PanelColor; panelImage.raycastTarget = true;
        var title = Text(panel, "Title", "토템 회전 UI · 카메라 시안", 40f, TextAlignmentOptions.Left);
        Anchor(title.rectTransform, new Vector2(0.03f, 0.86f), new Vector2(0.97f, 0.98f));
        title.fontStyle = FontStyles.Bold;

        _styleButtons = Row(panel, StyleLabels, 0.70f, 0.84f, SelectStyle);
        _cameraButtons = Row(panel, CameraLabels, 0.54f, 0.68f, SelectCamera);
        _shapeButton = Button(panel, "Shape", "", new Vector2(0.02f, 0.38f), new Vector2(0.34f, 0.52f), NextShape);
        _demoButton = Button(panel, "Demo", "", new Vector2(0.35f, 0.38f), new Vector2(0.66f, 0.52f), ToggleDemo);
        _fingerButton = Button(panel, "Finger", "", new Vector2(0.67f, 0.38f), new Vector2(0.98f, 0.52f), ToggleFinger);
        _description = Text(panel, "Description", "", 26f, TextAlignmentOptions.TopLeft);
        Anchor(_description.rectTransform, new Vector2(0.03f, 0.11f), new Vector2(0.97f, 0.36f));
        _description.textWrappingMode = TextWrappingModes.Normal;
        _description.enableAutoSizing = true; _description.fontSizeMin = 18f; _description.fontSizeMax = 26f;
        _status = Text(panel, "Status", "", 24f, TextAlignmentOptions.Left);
        Anchor(_status.rectTransform, new Vector2(0.03f, 0.01f), new Vector2(0.97f, 0.1f));
        _status.color = new Color(0.75f, 0.83f, 0.8f, 1f);
        RefreshToggles();
    }

    private Button[] Row(RectTransform parent, string[] labels, float yMin, float yMax, UnityAction<int> onPick)
    {
        var buttons = new Button[labels.Length];
        float width = 0.96f / labels.Length;
        for (int i = 0; i < labels.Length; i++)
        {
            int choice = i;
            buttons[i] = Button(parent, labels[i], labels[i], new Vector2(0.02f + i * width, yMin), new Vector2(0.02f + (i + 1) * width - 0.008f, yMax), () => onPick(choice));
        }
        return buttons;
    }

    private Button Button(RectTransform parent, string name, string label, Vector2 min, Vector2 max, UnityAction action)
    {
        var rect = NewRect(name, parent);
        Anchor(rect, min, max);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = _sprites.RoundRect; image.type = Image.Type.Sliced; image.color = ButtonColor;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image; button.transition = Selectable.Transition.None;
        button.onClick.AddListener(action);
        var text = Text(rect, "Label", label, 28f, TextAlignmentOptions.Center);
        text.enableAutoSizing = true; text.fontSizeMin = 18f; text.fontSizeMax = 28f;
        text.rectTransform.offsetMin = new Vector2(6f, 0f); text.rectTransform.offsetMax = new Vector2(-6f, 0f);
        return button;
    }

    private TMP_Text Text(RectTransform parent, string name, string value, float size, TextAlignmentOptions alignment)
    {
        var rect = Stretch(NewRect(name, parent));
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _font; text.text = value; text.fontSize = size; text.color = TextColor;
        text.alignment = alignment; text.raycastTarget = false; text.richText = true;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private static Image Centered(string name, Transform parent, Sprite sprite, Vector2 size, Color color)
    {
        var rect = NewRect(name, parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        rect.sizeDelta = size;
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite; image.color = color; image.raycastTarget = false;
        return image;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static RectTransform Stretch(RectTransform rect)
    {
        Anchor(rect, Vector2.zero, Vector2.one);
        return rect;
    }

    private static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Highlight(Button[] buttons, int selected)
    {
        if (buttons == null) return;
        for (int i = 0; i < buttons.Length; i++) buttons[i].targetGraphic.color = i == selected ? SelectedColor : ButtonColor;
    }

    private static void SetLabel(Button button, string label, bool selected)
    {
        button.GetComponentInChildren<TMP_Text>().text = label;
        button.targetGraphic.color = selected ? SelectedColor : ButtonColor;
    }
}
