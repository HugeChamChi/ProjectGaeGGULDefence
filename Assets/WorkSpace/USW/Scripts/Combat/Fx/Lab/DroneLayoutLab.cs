using System.Collections.Generic;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 드론 배치/크기 실험실(FxLab_DroneLayout). 실제 6×4 칸 좌표에 실제 유닛·드론 외형(스크립트 제거본)을 깔고
/// 드론 배치 시안을 바꿔 가며 유닛·드론끼리 가리는 정도를 본다. 전투·DI 없음, 실험실 전용.
/// 정렬 규칙은 게임과 같다: 유닛 = -y×100, 드론 = 오너 순서 + 시안별 앞(+1)/뒤(-1).
/// </summary>
public sealed class DroneLayoutLab : MonoBehaviour
{
    private const int Columns = 6;
    private const int Rows = 4;
    private const string UnitSortingLayer = "Unit";
    private const int SkySortingOrder = 9000;
    private const int BadgeSortingOrder = 9500;
    private const int MetricsWarmupFrames = 3;
    private const float FallbackHalfWidth = 0.4f;
    private const float FallbackTop = 0.55f;
    private const float CellHalf = 0.54f;
    private static readonly Color ButtonIdle = new Color(1f, 1f, 1f, 0.15f);
    private static readonly Color ButtonSelected = new Color(0.35f, 0.8f, 0.45f, 0.85f);
    private static readonly float[] DroneScaleSteps = { 1f, 1.15f, 1.3f, 0.85f };
    // 사격 테스트: 쏘는 유닛 수(0 = 끔, -1 = 판 전체)
    private static readonly int[] FireUnitCounts = { 0, 2, 6, -1 };
    private const string MuzzleBone = "muzzle";
    // SpineActorVisual.PlayAttack과 같은 순서: attack 있으면 그것, 없으면 attack_start → loop → end, 끝나면 idle.
    private const string AttackAnimation = "attack";
    private const string AttackStartAnimation = "attack_start";
    private const string AttackLoopAnimation = "attack_loop";
    private const string AttackEndAnimation = "attack_end";
    private const string IdleAnimation = "idle";

    // 판 구성: 위 줄부터. B=베탕 G=감망 D=델탕 Z=젤탕 S=디시그망 .=빈칸
    private static readonly string[][] Boards =
    {
        new[] { "BBBBBB", "BBBBBB", "BBBBBB", "BBBBBB" },
        new[] { "BGBBDB", "ZBSBBG", "BDBZBS", "GBBSDB" },
        new[] { "..B.G.", ".B..B.", "B.D.Z.", "..BS.B" },
    };
    private static readonly string[] BoardNames = { "베탕 24칸(최악)", "혼합 덱 24칸", "중반 12칸" };
    private const string UnitCodes = "BGDZS";
    // 유닛 코드 순서별 드론 템플릿(-1 = 드론 없음)과 기본 드론 수(-1 = 베탕 수 사용)
    private static readonly int[] DroneTemplateByUnit = { 0, 1, 2, -1, -1 };
    private static readonly int[] DroneCountByUnit = { -1, 1, 1, 0, 0 };

    [Header("외형 템플릿 (비활성, 스크립트 제거본)")]
    [Tooltip("순서: 베탕, 감망, 델탕, 젤탕, 디시그망")]
    [SerializeField] private GameObject[] _unitTemplates = new GameObject[5];
    [Tooltip("순서: 일반(베탕), 버프(감망), 디버프(델탕)")]
    [SerializeField] private GameObject[] _droneTemplates = new GameObject[3];

    [Header("칸 좌표 (IngameScene GridCell_0_0 기준)")]
    [SerializeField] private Vector2 _cellOrigin = new Vector2(-2.72f, -3.03f);
    [SerializeField] private float _cellStep = 1.088f;
    [Tooltip("UnitData.spawnOffsetY와 같은 칸 기준 유닛 높이 보정")]
    [SerializeField] private float _unitOffsetY = -0.2f;

    [Header("현재 게임 슬롯 (BetanFormationSettings 복사)")]
    [SerializeField] private Vector2[] _currentSlots = System.Array.Empty<Vector2>();

    [Header("시안")]
    [SerializeField] private DroneLayoutVariant[] _variants = System.Array.Empty<DroneLayoutVariant>();
    [SerializeField] private int _variant;
    [SerializeField] private int _board;
    [SerializeField, Range(1, 8)] private int _betanDrones = 4;
    [SerializeField] private int _droneScaleStep;
    [SerializeField] private bool _bob = true;
    [SerializeField] private float _bobAmplitude = 0.06f;
    [SerializeField] private float _bobPeriod = 3.6f;
    [SerializeField] private float _orbitPeriod = 6f;

    [Header("사격 테스트 (게임 DroneSkyBolt 프리팹·ProjectileData_DroneLaser 값 복사)")]
    [SerializeField] private Material _boltBeamMaterial;
    [SerializeField] private Material _boltGlowMaterial;
    [Tooltip("순서: 일반(민트), 버프(노랑), 디버프(빨강)")]
    [SerializeField] private Color[] _boltTints = { new Color(0.25f, 1f, 0.82f), new Color(1f, 0.86f, 0.2f), new Color(1f, 0.3f, 0.28f) };
    [SerializeField] private float _boltWidth = 0.16f;
    [SerializeField] private float _boltSegment = 0.65f;
    [SerializeField] private float _boltDuration = 0.2f;
    [Tooltip("UnitData_Betan 전설 attackSpeed(초당 횟수)")]
    [SerializeField] private float _attacksPerSecond = 1.65f;
    [SerializeField] private Vector2 _bossCenter = new Vector2(0f, 3.1f);
    [SerializeField] private Vector2 _bossExtents = new Vector2(0.7f, 0.5f);
    [SerializeField] private int _fireMode = 1;

    [Header("궤도 회전 좌우")]
    [Tooltip("켜면 궤도(H) 드론이 움직이는 방향을 보도록 좌우를 돌린다. 끄면 게임 규칙(판 가운데 보기) 고정.")]
    [SerializeField] private bool _orbitFaceMotion = true;
    [Tooltip("좌우 전환에 걸리는 시간(초). 0이면 즉시 뒤집기.")]
    [SerializeField, Min(0f)] private float _turnSeconds = 0.15f;

    [Header("UI")]
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private TMP_Text _status;
    [SerializeField] private Image[] _variantButtons = System.Array.Empty<Image>();

    private sealed class Placed
    {
        public int Code;
        public int Column;
        public int Row;
        public Transform Unit;
        public Renderer[] UnitRenderers;
        public Vector3 BasePosition;
        public Vector3 BaseScale;
        public readonly List<Transform> Drones = new();
        public readonly List<Renderer[]> DroneRenderers = new();
        public readonly List<Vector3> DroneBaseScales = new();
        public readonly List<SkeletonAnimation> DroneSkeletons = new();
        public readonly List<float> NextFire = new();
        public readonly List<float> FacingSign = new();
        public int DroneTemplate;
        public bool Firing;
        public TextMeshPro Badge;
        public float Phase;
    }

    private readonly List<Placed> _placed = new();
    private readonly Vector2[] _halfWidth = new Vector2[5]; // x = 반폭, y = 머리 높이(피벗 기준)
    private readonly bool[] _measured = new bool[5];
    private Transform _boardRoot;
    private int _startFrame;

    private sealed class Bolt
    {
        public DroneBeamVisual Visual;
        public Vector3 From;
        public Vector3 To;
        public float Elapsed;
        public Color Tint;
        public bool Active;
    }

    private readonly List<Bolt> _bolts = new();
    private int _shotsLastSecond;
    private int _shotsThisSecond;
    private float _secondStart;

    /// <summary>현재 시안 수.</summary>
    public int VariantCount => _variants.Length;

    private void Start()
    {
        foreach (var t in _unitTemplates) if (t != null) t.SetActive(false);
        foreach (var t in _droneTemplates) if (t != null) t.SetActive(false);
        BuildBoard();
        ApplyNow();
    }

    private void Update()
    {
        ApplyNow();
        UpdateFiring();
        UpdateBolts();
    }

    // ── 버튼 ─────────────────────────────────────────────────────────

    /// <summary>시안 선택.</summary>
    public void SelectVariant(int index)
    {
        if (_variants.Length == 0) return;
        _variant = Mathf.Clamp(index, 0, _variants.Length - 1);
        ApplyNow();
    }

    /// <summary>판 구성 순환(베탕 가득 → 혼합 → 중반).</summary>
    public void NextBoard()
    {
        _board = (_board + 1) % Boards.Length;
        BuildBoard();
        ApplyNow();
    }

    /// <summary>판 구성 직접 지정(캡처용).</summary>
    public void SetBoard(int index)
    {
        _board = Mathf.Clamp(index, 0, Boards.Length - 1);
        BuildBoard();
        ApplyNow();
    }

    /// <summary>베탕 드론 수 4 ↔ 5(선택지 추가 드론).</summary>
    public void ToggleBetanDrones()
    {
        _betanDrones = _betanDrones == 4 ? 5 : 4;
        BuildBoard();
        ApplyNow();
    }

    /// <summary>시안 위에 곱하는 드론 크기 배율 순환.</summary>
    public void NextDroneScale()
    {
        _droneScaleStep = (_droneScaleStep + 1) % DroneScaleSteps.Length;
        ApplyNow();
    }

    /// <summary>사격 테스트 순환: 끔 → 유닛 2기 → 6기 → 판 전체.</summary>
    public void NextFireMode()
    {
        _fireMode = (_fireMode + 1) % FireUnitCounts.Length;
        PickShooters();
        ApplyNow();
    }

    /// <summary>궤도 드론 좌우: 이동 방향 보기 ↔ 판 가운데 고정.</summary>
    public void ToggleOrbitFacing()
    {
        _orbitFaceMotion = !_orbitFaceMotion;
        ApplyNow();
    }

    /// <summary>부유 연출 켬/끔.</summary>
    public void ToggleBob()
    {
        _bob = !_bob;
        ApplyNow();
    }

    // ── 캡처 ─────────────────────────────────────────────────────────

    /// <summary>마지막 캡처 결과(진행 중이면 null). 에디터 캡처 메뉴가 읽는다.</summary>
    public string CaptureResult { get; private set; } = string.Empty;

    /// <summary>판 구성 × 시안 전체를 PNG로 저장한다. 렌더러 위치는 프레임을 넘겨야 반영되므로 코루틴으로 찍는다.</summary>
    public void CaptureAll(string folder, int boards, int width, int height)
    {
        CaptureResult = null;
        StartCoroutine(CaptureRoutine(folder, Mathf.Clamp(boards, 1, Boards.Length), width, height));
    }

    private System.Collections.IEnumerator CaptureRoutine(string folder, int boards, int width, int height)
    {
        System.IO.Directory.CreateDirectory(folder);
        var cam = Camera.main;
        int count = 0;
        int startVariant = _variant;
        for (int board = 0; board < boards; board++)
        {
            SetBoard(board);
            for (int f = 0; f <= MetricsWarmupFrames + 1; f++) yield return null;
            for (int v = 0; v < _variants.Length; v++)
            {
                SelectVariant(v);
                yield return null;
                yield return new WaitForEndOfFrame();
                Shot(cam, System.IO.Path.Combine(folder, $"board{board}_v{v:00}.png"), width, height);
                count++;
            }
        }
        SetBoard(0);
        SelectVariant(startVariant);
        CaptureResult = $"{folder} ({count})";
    }

    /// <summary>현재 상태 그대로 일정 간격 연속 캡처(사격·궤도 움직임 확인용).</summary>
    public void CaptureSequence(string folder, int frames, float interval, int width, int height)
    {
        CaptureResult = null;
        StartCoroutine(SequenceRoutine(folder, frames, interval, width, height));
    }

    private System.Collections.IEnumerator SequenceRoutine(string folder, int frames, float interval, int width, int height)
    {
        System.IO.Directory.CreateDirectory(folder);
        var cam = Camera.main;
        for (int f = 0; f < frames; f++)
        {
            yield return new WaitForEndOfFrame();
            Shot(cam, System.IO.Path.Combine(folder, $"seq_{f:000}.png"), width, height);
            yield return new WaitForSeconds(interval);
        }
        CaptureResult = $"{folder} ({frames})";
    }

    private static void Shot(Camera cam, string path, int width, int height)
    {
        Canvas.ForceUpdateCanvases();
        var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;
        var active = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = active;
        RenderTexture.ReleaseTemporary(rt);
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Destroy(tex);
    }

    // ── 판 구성 ───────────────────────────────────────────────────────

    private void BuildBoard()
    {
        if (_boardRoot != null) Destroy(_boardRoot.gameObject);
        _boardRoot = new GameObject("Board").transform;
        _boardRoot.SetParent(transform, false);
        _placed.Clear();
        _startFrame = Time.frameCount;
        for (int i = 0; i < _measured.Length; i++) _measured[i] = false;

        var rows = Boards[_board];
        for (int line = 0; line < rows.Length; line++)
        {
            int row = Rows - 1 - line;
            for (int col = 0; col < Columns && col < rows[line].Length; col++)
            {
                int code = UnitCodes.IndexOf(rows[line][col]);
                if (code < 0 || _unitTemplates[code] == null) continue;
                _placed.Add(Spawn(code, col, row));
            }
        }
        PickShooters();
    }

    /// <summary>쏘는 유닛을 판 전체에 고르게 뽑는다(드론이 있는 유닛만).</summary>
    private void PickShooters()
    {
        var armed = _placed.FindAll(x => x.Drones.Count > 0);
        foreach (var p in _placed) p.Firing = false;
        int want = FireUnitCounts[_fireMode];
        if (want < 0) want = armed.Count;
        want = Mathf.Min(want, armed.Count);
        for (int k = 0; k < want; k++)
            armed[Mathf.FloorToInt((k + 0.5f) * armed.Count / want)].Firing = true;
    }

    private Placed Spawn(int code, int col, int row)
    {
        var cell = CellCenter(col, row);
        var unit = Instantiate(_unitTemplates[code], _boardRoot);
        unit.name = $"{_unitTemplates[code].name}_{col}_{row}";
        unit.SetActive(true);
        var p = new Placed
        {
            Code = code, Column = col, Row = row, Unit = unit.transform,
            UnitRenderers = unit.GetComponentsInChildren<Renderer>(true),
            BasePosition = new Vector3(cell.x, cell.y + _unitOffsetY, 0f),
            BaseScale = _unitTemplates[code].transform.localScale,
            Phase = (col * 0.37f + row * 0.61f) % 1f,
            DroneTemplate = DroneTemplateByUnit[code],
        };

        int droneTemplate = DroneTemplateByUnit[code];
        int count = DroneCountByUnit[code] < 0 ? _betanDrones : DroneCountByUnit[code];
        if (droneTemplate >= 0 && _droneTemplates[droneTemplate] != null)
        {
            for (int i = 0; i < count; i++)
            {
                var drone = Instantiate(_droneTemplates[droneTemplate], _boardRoot);
                drone.name = $"{p.Unit.name}_Drone{i}";
                drone.SetActive(true);
                p.Drones.Add(drone.transform);
                p.DroneRenderers.Add(drone.GetComponentsInChildren<Renderer>(true));
                p.DroneBaseScales.Add(_droneTemplates[droneTemplate].transform.localScale);
                p.DroneSkeletons.Add(drone.GetComponentInChildren<SkeletonAnimation>(true));
                p.NextFire.Add(Time.time + (p.Phase + i * 0.29f) % 1f / Mathf.Max(0.01f, _attacksPerSecond));
                p.FacingSign.Add(p.Column < Columns / 2 ? -1f : 1f);
            }
        }

        if (p.Drones.Count > 1 && _font != null)
        {
            var badge = new GameObject($"{p.Unit.name}_Badge", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            badge.transform.SetParent(_boardRoot, false);
            badge.font = _font;
            badge.fontSize = 2.4f;
            badge.alignment = TextAlignmentOptions.Center;
            badge.text = $"×{p.Drones.Count}";
            badge.color = Color.white;
            badge.outlineWidth = 0.25f;
            badge.outlineColor = new Color32(20, 20, 30, 255);
            badge.rectTransform.sizeDelta = new Vector2(1f, 0.5f);
            badge.sortingLayerID = SortingLayer.NameToID(UnitSortingLayer);
            badge.sortingOrder = BadgeSortingOrder;
            p.Badge = badge;
        }
        return p;
    }

    private Vector2 CellCenter(int col, int row) => _cellOrigin + new Vector2(col * _cellStep, row * _cellStep);

    // ── 배치 ─────────────────────────────────────────────────────────

    /// <summary>현재 시안을 즉시 적용한다(캡처 직전에도 호출).</summary>
    public void ApplyNow()
    {
        if (_variants.Length == 0) return;
        var v = _variants[Mathf.Clamp(_variant, 0, _variants.Length - 1)];
        float droneMul = v.DroneScale * DroneScaleSteps[_droneScaleStep];
        float t = Time.time;

        foreach (var p in _placed)
        {
            if (p.Unit == null) continue;
            int face = p.Column < Columns / 2 ? 1 : -1; // 판 가운데를 보는 방향(+1 = 오른쪽)
            p.Unit.position = p.BasePosition;
            float sx = Mathf.Abs(p.BaseScale.x) * v.UnitScale;
            p.Unit.localScale = new Vector3(face > 0 ? -sx : sx, p.BaseScale.y * v.UnitScale, p.BaseScale.z);
            int unitOrder = Mathf.RoundToInt(-p.BasePosition.y * 100f);
            SetSorting(p.UnitRenderers, unitOrder);

            var metric = Metric(p) * v.UnitScale;
            int n = p.Drones.Count;
            for (int i = 0; i < n; i++)
            {
                var drone = p.Drones[i];
                bool visible = v.Kind != DroneLayoutKind.SingleWithCount || i == 0;
                drone.gameObject.SetActive(visible);
                if (!visible) continue;

                Slot(v, p, i, n, metric, face, t, out var pos, out int sortDelta, out bool sky);
                float bob = _bob ? Mathf.Sin((t / _bobPeriod + p.Phase + i * 0.23f) * Mathf.PI * 2f) * _bobAmplitude * droneMul : 0f;
                drone.position = new Vector3(pos.x, pos.y + bob, 0f);
                var bs = p.DroneBaseScales[i];
                float dx = Mathf.Abs(bs.x) * droneMul;
                // 그림 기본은 왼쪽을 본다: 오른쪽을 보려면 x 음수. 궤도에선 이동 방향(x 속도 부호)을 보게 할 수 있다.
                float targetSign = face > 0 ? -1f : 1f;
                if (v.Kind == DroneLayoutKind.Orbit && _orbitFaceMotion)
                {
                    float a = (t / _orbitPeriod + i / (float)Mathf.Max(1, n)) * Mathf.PI * 2f;
                    targetSign = -Mathf.Sin(a) > 0f ? -1f : 1f;
                }
                float sign = _turnSeconds <= 0f ? targetSign
                    : Mathf.MoveTowards(p.FacingSign[i], targetSign, 2f * Time.deltaTime / _turnSeconds);
                p.FacingSign[i] = sign;
                drone.localScale = new Vector3(sign * dx, bs.y * droneMul, bs.z);
                SetSorting(p.DroneRenderers[i], sky ? SkySortingOrder + p.Row * 10 + i : unitOrder + sortDelta);
            }

            if (p.Badge != null)
            {
                bool show = v.Kind == DroneLayoutKind.SingleWithCount && n > 1;
                p.Badge.gameObject.SetActive(show);
                if (show && n > 0)
                {
                    var first = p.Drones[0].position;
                    p.Badge.transform.position = first + new Vector3(face * 0.26f, -0.12f, 0f);
                }
            }
        }

        if (_status != null)
            _status.text = $"<b>{v.Label}</b>  {v.Description}\n판: {BoardNames[_board]} · 베탕 드론 {_betanDrones}기 · 드론 크기 ×{droneMul:0.##} (프리팹 0.9 기준) · 부유 {(_bob ? "켬" : "끔")}"
                + $"\n사격: {FireLabel()} · 초당 발사 {_shotsLastSecond} · 화면 레이저 {ActiveBolts()} · {Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.smoothDeltaTime))}fps · 궤도 좌우 {(_orbitFaceMotion ? "이동 방향" : "고정")}";
        for (int i = 0; i < _variantButtons.Length; i++)
            if (_variantButtons[i] != null) _variantButtons[i].color = i == _variant ? ButtonSelected : ButtonIdle;
    }

    // ── 사격 테스트 ──────────────────────────────────────────────────

    private string FireLabel()
    {
        int want = FireUnitCounts[_fireMode];
        return want == 0 ? "끔" : want < 0 ? "판 전체" : $"유닛 {want}기";
    }

    private int ActiveBolts()
    {
        int c = 0;
        foreach (var b in _bolts) if (b.Active) c++;
        return c;
    }

    /// <summary>게임 DroneUnit처럼 드론마다 공격 간격마다 총구(muzzle 본)에서 보스 영역 임의 지점으로 쏜다. 피해 없음.</summary>
    private void UpdateFiring()
    {
        float now = Time.time;
        if (now - _secondStart >= 1f) { _shotsLastSecond = _shotsThisSecond; _shotsThisSecond = 0; _secondStart = now; }
        if (FireUnitCounts[_fireMode] == 0) return;
        float interval = 1f / Mathf.Max(0.01f, _attacksPerSecond);
        foreach (var p in _placed)
        {
            if (!p.Firing) continue;
            for (int i = 0; i < p.Drones.Count; i++)
            {
                if (now < p.NextFire[i]) continue;
                p.NextFire[i] = now + interval;
                if (!p.Drones[i].gameObject.activeInHierarchy) continue;
                Fire(p, i);
            }
        }
    }

    private void Fire(Placed p, int i)
    {
        var skeleton = p.DroneSkeletons[i];
        Vector3 origin = p.Drones[i].position;
        var bone = skeleton != null ? skeleton.Skeleton?.FindBone(MuzzleBone) : null;
        if (bone != null) origin = bone.GetWorldPosition(skeleton.transform);
        PlayAttack(skeleton);
        var target = _bossCenter + new Vector2(Random.Range(-_bossExtents.x, _bossExtents.x), Random.Range(-_bossExtents.y, _bossExtents.y));
        var bolt = _bolts.Find(b => !b.Active);
        if (bolt == null)
        {
            var go = new GameObject($"Bolt_{_bolts.Count}");
            go.transform.SetParent(transform, false);
            bolt = new Bolt { Visual = go.AddComponent<DroneBeamVisual>() };
            bolt.Visual.Initialize(_boltBeamMaterial, _boltGlowMaterial);
            _bolts.Add(bolt);
        }
        bolt.From = origin;
        bolt.To = target;
        bolt.Elapsed = 0f;
        bolt.Tint = _boltTints[Mathf.Clamp(p.DroneTemplate, 0, _boltTints.Length - 1)];
        bolt.Active = true;
        _shotsThisSecond++;
    }

    private static void PlayAttack(SkeletonAnimation skeleton)
    {
        if (skeleton == null || skeleton.AnimationState == null) return;
        var data = skeleton.Skeleton.Data;
        var state = skeleton.AnimationState;
        if (data.FindAnimation(AttackAnimation) != null) state.SetAnimation(0, AttackAnimation, false);
        else if (data.FindAnimation(AttackStartAnimation) != null)
        {
            state.SetAnimation(0, AttackStartAnimation, false);
            if (data.FindAnimation(AttackLoopAnimation) != null) state.AddAnimation(0, AttackLoopAnimation, false, 0f);
            if (data.FindAnimation(AttackEndAnimation) != null) state.AddAnimation(0, AttackEndAnimation, false, 0f);
        }
        else return;
        if (data.FindAnimation(IdleAnimation) != null) state.AddAnimation(0, IdleAnimation, true, 0f);
    }

    /// <summary>DroneLaserProjectile(구간 길이 > 0)와 같은 그리기: 직선 이동하는 끝점에서 구간 길이만큼 뒤까지.</summary>
    private void UpdateBolts()
    {
        foreach (var b in _bolts)
        {
            if (!b.Active) continue;
            b.Elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(b.Elapsed / Mathf.Max(0.01f, _boltDuration));
            Vector3 tip = Vector3.Lerp(b.From, b.To, k);
            Vector3 tail = tip - Vector3.ClampMagnitude(tip - b.From, _boltSegment);
            b.Visual.Draw(tail, tip, b.Tint, _boltWidth, 1f, 0f, 0.7f);
            if (k >= 1f) { b.Active = false; b.Visual.Clear(); }
        }
    }

    /// <summary>시안별 드론 한 기의 월드 위치와 정렬 보정.</summary>
    private void Slot(DroneLayoutVariant v, Placed p, int i, int n, Vector2 m, int face, float t,
        out Vector2 pos, out int sortDelta, out bool sky)
    {
        Vector2 root = p.BasePosition;
        Vector2 cell = CellCenter(p.Column, p.Row);
        float s = v.Spread;
        float halfW = m.x;
        float top = m.y;
        float centered = i - (n - 1) * 0.5f;
        sortDelta = 1;
        sky = false;

        switch (v.Kind)
        {
            case DroneLayoutKind.Current:
            case DroneLayoutKind.Compact:
            case DroneLayoutKind.ShrinkUnit:
            {
                var slot = _currentSlots.Length == 0 ? Vector2.zero : _currentSlots[i < _currentSlots.Length ? i : 0];
                pos = root + slot * s;
                break;
            }
            case DroneLayoutKind.HeadRow:
                pos = root + new Vector2(centered * 0.21f * s, top + 0.06f);
                break;
            case DroneLayoutKind.HeadArc:
            {
                float a = n == 1 ? 90f : Mathf.Lerp(155f, 25f, i / (float)(n - 1));
                a *= Mathf.Deg2Rad;
                pos = root + new Vector2(Mathf.Cos(a) * 0.44f * s, top - 0.18f + Mathf.Sin(a) * 0.3f * s);
                break;
            }
            case DroneLayoutKind.FootRow:
                pos = new Vector2(root.x + centered * 0.2f * s, cell.y - CellHalf + 0.14f);
                break;
            case DroneLayoutKind.BehindFan:
                sortDelta = -1;
                pos = root + new Vector2(centered * 0.26f * s, top - 0.12f - Mathf.Abs(centered) * 0.08f);
                break;
            case DroneLayoutKind.CellCorners:
            {
                float inset = (CellHalf - 0.16f) * s;
                Vector2[] corners =
                {
                    new Vector2(-inset, inset), new Vector2(inset, inset),
                    new Vector2(-inset, -inset), new Vector2(inset, -inset), new Vector2(0f, inset + 0.04f),
                };
                pos = cell + corners[i % corners.Length];
                break;
            }
            case DroneLayoutKind.BackColumn:
                pos = new Vector2(root.x - face * (halfW + 0.02f) * s, cell.y + 0.34f - i * 0.17f * s);
                break;
            case DroneLayoutKind.Orbit:
            {
                float a = (t / _orbitPeriod + i / (float)Mathf.Max(1, n)) * Mathf.PI * 2f;
                float sin = Mathf.Sin(a);
                pos = root + new Vector2(Mathf.Cos(a) * (halfW + 0.1f) * s, top * 0.45f + sin * 0.16f * s);
                sortDelta = sin > 0f ? -1 : 1;
                break;
            }
            case DroneLayoutKind.SingleWithCount:
                pos = root + new Vector2(-face * halfW * 0.55f * s, top - 0.05f);
                break;
            case DroneLayoutKind.SkyHangar:
            {
                float gridTop = CellCenter(0, Rows - 1).y + CellHalf;
                sky = true;
                pos = new Vector2(cell.x + centered * 0.19f * s, gridTop + 0.3f + (Rows - 1 - p.Row) * 0.26f * s);
                break;
            }
            case DroneLayoutKind.ShoulderRide:
                pos = root + new Vector2(centered * 0.16f * s, top - 0.28f);
                break;
            default:
                pos = root;
                break;
        }
    }

    /// <summary>유닛 종류별 외형 반폭·머리 높이. Spine 메시가 생긴 뒤 한 번 재고 고정한다(대기 애니로 드론이 흔들리지 않게).</summary>
    private Vector2 Metric(Placed p)
    {
        if (_measured[p.Code]) return _halfWidth[p.Code];
        var fallback = new Vector2(FallbackHalfWidth, FallbackTop);
        if (Time.frameCount - _startFrame < MetricsWarmupFrames) return fallback;

        bool any = false;
        var b = new Bounds();
        foreach (var r in p.UnitRenderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r.bounds.size.sqrMagnitude < 0.0001f) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!any) return fallback;
        // 유닛 배율 1 기준으로 저장해 두고, 사용할 때 현재 시안의 유닛 배율을 곱한다.
        float unitScale = Mathf.Max(0.01f, Mathf.Abs(p.Unit.localScale.y / p.BaseScale.y));
        _halfWidth[p.Code] = new Vector2(b.extents.x, b.max.y - p.BasePosition.y) / unitScale;
        _measured[p.Code] = true;
        return _halfWidth[p.Code];
    }

    private static void SetSorting(Renderer[] renderers, int order)
    {
        int layer = SortingLayer.NameToID(UnitSortingLayer);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.sortingLayerID = layer;
            r.sortingOrder = order;
        }
    }
}
