using System.Collections.Generic;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 출격 실험실(FxLab_Sortie) — "고정 그리드인데 판이 너무 정적" 해결 시안.
/// 원칙: 칸은 판정·점유의 자리(진지)로 그대로 두고, 보이는 몸만 칸을 떠났다 돌아온다. 토템은 움직이지 않는 유일한 존재로 남고
/// 토템 버프 색이 투사체·트레일·연결선으로 출격한 유닛을 따라간다. 실제 6×4 칸 좌표와 실제 유닛·드론·토템 외형(스크립트 제거본)을 쓴다.
/// 전투·DI·피해 없음, 실험실 전용.
/// </summary>
public sealed class SortieLab : MonoBehaviour
{
    private const int Columns = 6;
    private const int Rows = 4;
    private const string UnitSortingLayer = "Unit";
    private const string FxSortingLayer = "FX";
    private const int PaintOrder = -3000;
    private const int HomeMarkOrder = -2500;
    private const int LeapOrder = 4000;
    private const int SortieDroneOrder = 4500;
    private const float CellSize = 1.04f;
    private const float AwayThreshold = 0.1f;
    private const float BodyHeight = 0.45f;
    private const float OrbitRadiusX = 0.5f;
    private const float OrbitRadiusY = 0.16f;
    private const float OrbitHeight = 0.25f;
    private const float OrbitPeriod = 6f;
    private static readonly Color ButtonIdle = new Color(1f, 1f, 1f, 0.15f);
    private static readonly Color ButtonSelected = new Color(0.35f, 0.8f, 0.45f, 0.85f);
    private static readonly Color BossHitTint = new Color(1f, 0.55f, 0.55f, 1f);
    private static readonly float[] SpeedSteps = { 1f, 0.5f, 0.25f };
    private static readonly string[] BuffModeNames = { "끔", "투사체", "투사체+트레일", "투사체+트레일+연결선" };

    private const string AttackAnimation = "attack";
    private const string AttackStartAnimation = "attack_start";
    private const string AttackLoopAnimation = "attack_loop";
    private const string AttackEndAnimation = "attack_end";
    private const string IdleAnimation = "idle";

    // 판 구성(위 줄부터): B=베탕 G=감망 D=델탕 Z=젤탕 S=디시그망 1·2=토템 .=빈칸
    private static readonly string[] Board = { "B.G.DB", "ZB1BSG", "BDB2BZ", "G.BSDB" };
    private const string UnitCodes = "BGDZS";
    private const string TotemCodes = "12";
    // 유닛 코드 순서별 드론 템플릿(-1 = 없음)과 드론 수
    private static readonly int[] DroneTemplateByUnit = { 0, 1, 2, -1, -1 };
    private static readonly int[] DroneCountByUnit = { 4, 1, 1, 0, 0 };
    // 토템 범위(칸 오프셋): 1 = 십자(상하좌우), 2 = 같은 줄 좌우 2칸
    private static readonly Vector2Int[][] TotemRanges =
    {
        new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right },
        new[] { new Vector2Int(-2, 0), new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) },
    };

    [Header("외형 템플릿 (비활성, 스크립트 제거본)")]
    [Tooltip("순서: 베탕, 감망, 델탕, 젤탕, 디시그망")]
    [SerializeField] private GameObject[] _unitTemplates = new GameObject[5];
    [Tooltip("순서: 일반(베탕), 버프(감망), 디버프(델탕)")]
    [SerializeField] private GameObject[] _droneTemplates = new GameObject[3];
    [Tooltip("판 구성의 토템 1, 2")]
    [SerializeField] private GameObject[] _totemTemplates = new GameObject[2];

    [Header("칸 좌표 (IngameScene GridCell_0_0 기준)")]
    [SerializeField] private Vector2 _cellOrigin = new Vector2(-2.72f, -3.03f);
    [SerializeField] private float _cellStep = 1.088f;
    [Tooltip("UnitData.spawnOffsetY와 같은 칸 기준 유닛 높이 보정")]
    [SerializeField] private float _unitOffsetY = -0.2f;

    [Header("보스·카메라")]
    [SerializeField] private Transform _boss;
    [SerializeField] private Vector2 _bossCenter = new Vector2(0f, 3.1f);
    [SerializeField] private Vector2 _bossExtents = new Vector2(0.7f, 0.5f);
    [Tooltip("도약이 내려찍는 높이: 보스 중심에서 아래로")]
    [SerializeField] private float _slamBelowBoss = 0.95f;

    [Header("시안")]
    [SerializeField] private SortieLabVariant[] _variants = System.Array.Empty<SortieLabVariant>();
    [SerializeField] private int _variant;

    [Header("타이밍 (초, 실험실 시계 = 실제 시간 × 속도)")]
    [SerializeField, Min(0.2f)] private float _attackInterval = 1.6f;
    [SerializeField, Min(0.3f)] private float _skillInterval = 2.4f;
    [SerializeField] private float _lungeDistance = 0.26f;
    [SerializeField, Min(0.1f)] private float _lungeSeconds = 0.42f;
    [SerializeField, Min(0.3f)] private float _leapSeconds = 1.15f;
    [SerializeField] private float _leapHeight = 0.9f;
    [SerializeField] private float _leapScale = 0.15f;
    [SerializeField, Min(0.3f)] private float _droneSortieSeconds = 0.95f;
    [SerializeField] private float _pulseAmplitude = 0.07f;
    [SerializeField, Min(0.2f)] private float _pulseBeat = 1.2f;
    [SerializeField] private float _recoil = 0.05f;

    [Header("토템 (실험실 색 — TD1008 효과 그룹 색)")]
    [SerializeField] private Color[] _totemColors = { new Color(1f, 0.3f, 0.1f, 1f), new Color(0.15f, 0.7f, 1f, 1f) };
    [SerializeField] private string[] _totemLabels = { "공격력", "공격 속도" };
    [SerializeField, Range(0f, 1f)] private float _paintAlpha = 0.2f;

    [Header("표시 토글")]
    [SerializeField] private bool _totems = true;
    [Tooltip("0 끔, 1 투사체, 2 +트레일, 3 +연결선·칸 번쩍임")]
    [SerializeField, Range(0, 3)] private int _buffMode = 3;
    [SerializeField] private bool _homeMarks = true;
    [SerializeField] private bool _bossReact = true;
    [SerializeField] private bool _cameraPunch = true;
    [SerializeField] private int _speedStep;

    [Header("사격 (게임 DroneSkyBolt 값 복사)")]
    [SerializeField] private Material _boltBeamMaterial;
    [SerializeField] private Material _boltGlowMaterial;
    [Tooltip("순서: 일반(민트), 버프(노랑), 디버프(빨강)")]
    [SerializeField] private Color[] _boltTints = { new Color(0.25f, 1f, 0.82f), new Color(1f, 0.86f, 0.2f), new Color(1f, 0.3f, 0.28f) };
    [SerializeField] private float _boltWidth = 0.16f;
    [SerializeField] private float _boltSegment = 0.65f;
    [SerializeField] private float _boltDuration = 0.2f;

    [Header("선·트레일")]
    [SerializeField] private Material _lineMaterial;
    [SerializeField] private Color _neutralTrail = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private float _unitTrailWidth = 0.18f;
    [SerializeField] private float _droneTrailWidth = 0.12f;

    [Header("UI")]
    [SerializeField] private TMP_Text _status;
    [SerializeField] private Image[] _variantButtons = System.Array.Empty<Image>();

    private enum Motion { None, Lunge, Leap, Recoil }

    private sealed class Drone
    {
        public Transform Body;
        public Renderer[] Renderers;
        public Vector3 BaseScale;
        public SkeletonAnimation Skeleton;
        public TrailRenderer Trail;
        public int Template;
        public float SortieStart = -1f;
        public bool SortieFired;
        public Vector3 SortieFrom;
        public Vector3 SortieTarget;
        public float Side;
        public float Facing = 1f;
        public Vector3 LastPosition;
    }

    private sealed class Actor
    {
        public int Code;
        public int Column;
        public int Row;
        public Transform Unit;
        public Renderer[] Renderers;
        public SkeletonAnimation Skeleton;
        public Vector3 Home;
        public Vector3 BaseScale;
        public int Face;
        public float Phase;
        public float NextAttack;
        public int AttackCount;
        public Motion Motion;
        public float MotionStart;
        public bool MotionFired;
        public Vector3 SlamPoint;
        public readonly List<Drone> Drones = new();
        public readonly List<int> Buffs = new();
        public TrailRenderer Trail;
        public LineRenderer[] Tethers;
        public SpriteRenderer HomeMark;
    }

    private sealed class Bolt
    {
        public DroneBeamVisual Visual;
        public Vector3 From;
        public Vector3 To;
        public float Elapsed;
        public Color Tint;
        public bool Active;
    }

    private sealed class Ring
    {
        public SpriteRenderer Renderer;
        public float Start;
        public Color Tint;
    }

    private readonly List<Actor> _actors = new();
    private readonly List<Bolt> _bolts = new();
    private readonly List<Ring> _rings = new();
    private readonly Dictionary<Vector2Int, List<SpriteRenderer>> _paint = new();
    private readonly Dictionary<Vector2Int, float> _paintFlash = new();
    private readonly List<Transform> _totemBodies = new();
    private readonly Dictionary<int, Vector3> _totemTops = new();
    private Transform _boardRoot;
    private Sprite _squareSprite;
    private Sprite _discSprite;
    private Sprite _ringSprite;
    private float _clock;
    private float _nextSkill;
    private float _bossFlinch;
    private float _shake;
    private Vector3 _bossBasePosition;
    private Vector3 _bossBaseScale;
    private SpriteRenderer _bossRenderer;
    private Camera _camera;
    private Vector3 _cameraBasePosition;
    private float _cameraBaseFov;

    private SortieLabVariant Current => _variants.Length == 0 ? null : _variants[Mathf.Clamp(_variant, 0, _variants.Length - 1)];
    private SortieLabMotionKind Kind => Current?.Kind ?? SortieLabMotionKind.Static;

    private void Start()
    {
        foreach (var t in _unitTemplates) if (t != null) t.SetActive(false);
        foreach (var t in _droneTemplates) if (t != null) t.SetActive(false);
        foreach (var t in _totemTemplates) if (t != null) t.SetActive(false);
        _squareSprite = MakeSprite(Square, "LabSquare");
        _discSprite = MakeSprite(Disc, "LabDisc");
        _ringSprite = MakeSprite(RingShape, "LabRing");
        _camera = Camera.main;
        if (_camera != null) { _cameraBasePosition = _camera.transform.position; _cameraBaseFov = _camera.fieldOfView; }
        if (_boss != null)
        {
            _bossBasePosition = _boss.position;
            _bossBaseScale = _boss.localScale;
            _bossRenderer = _boss.GetComponent<SpriteRenderer>();
        }
        BuildBoard();
        Refresh();
    }

    private void Update()
    {
        float dt = Time.deltaTime * SpeedSteps[_speedStep];
        _clock += dt;
        UpdateAttacks();
        UpdateSkill();
        foreach (var a in _actors) UpdateActor(a);
        UpdateBolts(dt);
        UpdateRings();
        UpdatePaint(dt);
        UpdateBoss(dt);
        UpdateCamera(dt);
        Refresh();
    }

    private void OnDisable()
    {
        if (_camera != null) { _camera.transform.position = _cameraBasePosition; _camera.fieldOfView = _cameraBaseFov; }
    }

    // ── 버튼 ─────────────────────────────────────────────────────────

    /// <summary>시안 선택. 진행 중인 움직임은 모두 칸으로 돌려보낸다.</summary>
    public void SelectVariant(int index)
    {
        if (_variants.Length == 0) return;
        _variant = Mathf.Clamp(index, 0, _variants.Length - 1);
        foreach (var a in _actors)
        {
            a.Motion = Motion.None;
            foreach (var d in a.Drones) d.SortieStart = -1f;
        }
        _nextSkill = _clock + _skillInterval * 0.5f;
        Refresh();
    }

    /// <summary>토템 표시 켬/끔(버프 칸 칠하기 포함).</summary>
    public void ToggleTotems()
    {
        _totems = !_totems;
        foreach (var t in _totemBodies) if (t != null) t.gameObject.SetActive(_totems);
        foreach (var list in _paint.Values) foreach (var r in list) r.enabled = _totems;
        Refresh();
    }

    /// <summary>버프 색 단계 순환: 끔 → 투사체 → +트레일 → +연결선·칸 번쩍임.</summary>
    public void NextBuffMode()
    {
        _buffMode = (_buffMode + 1) % BuffModeNames.Length;
        Refresh();
    }

    /// <summary>칸 발판(유닛이 나가 있을 때 진지 표시) 켬/끔.</summary>
    public void ToggleHomeMarks() { _homeMarks = !_homeMarks; Refresh(); }

    /// <summary>보스 피격 반응(움찔·붉은 번쩍임) 켬/끔.</summary>
    public void ToggleBossReact() { _bossReact = !_bossReact; Refresh(); }

    /// <summary>도약 착지 카메라 흔들림·줌 켬/끔.</summary>
    public void ToggleCameraPunch() { _cameraPunch = !_cameraPunch; Refresh(); }

    /// <summary>재생 속도 순환 ×1 → ×0.5 → ×0.25.</summary>
    public void NextSpeed() { _speedStep = (_speedStep + 1) % SpeedSteps.Length; Refresh(); }

    /// <summary>지금 바로 한 기를 도약시킨다(시안과 무관하게 확인용).</summary>
    public void TriggerLeap()
    {
        var a = PickIdleActor();
        if (a != null) StartLeap(a);
    }

    // ── 캡처 ─────────────────────────────────────────────────────────

    /// <summary>마지막 캡처 결과(진행 중이면 null). 에디터 캡처 메뉴가 읽는다.</summary>
    public string CaptureResult { get; private set; } = string.Empty;

    /// <summary>시안마다 몇 초 돌린 뒤 일정 간격으로 연속 캡처한다.</summary>
    public void CaptureAll(string folder, float warmup, int frames, float interval, int width, int height)
    {
        CaptureResult = null;
        StartCoroutine(CaptureRoutine(folder, warmup, frames, interval, width, height));
    }

    private System.Collections.IEnumerator CaptureRoutine(string folder, float warmup, int frames, float interval, int width, int height)
    {
        System.IO.Directory.CreateDirectory(folder);
        int start = _variant;
        int count = 0;
        for (int v = 0; v < _variants.Length; v++)
        {
            SelectVariant(v);
            yield return new WaitForSeconds(warmup);
            for (int f = 0; f < frames; f++)
            {
                yield return new WaitForEndOfFrame();
                Shot(_camera, System.IO.Path.Combine(folder, $"v{v}_{f:00}.png"), width, height);
                count++;
                yield return new WaitForSeconds(interval);
            }
        }
        SelectVariant(start);
        CaptureResult = $"{folder} ({count})";
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
        _actors.Clear();
        _paint.Clear();
        _paintFlash.Clear();
        _totemBodies.Clear();
        _totemTops.Clear();

        var totemCells = new Vector2Int?[TotemCodes.Length];
        for (int line = 0; line < Board.Length; line++)
        {
            int row = Rows - 1 - line;
            for (int col = 0; col < Columns && col < Board[line].Length; col++)
            {
                char c = Board[line][col];
                int unit = UnitCodes.IndexOf(c);
                int totem = TotemCodes.IndexOf(c);
                if (unit >= 0 && _unitTemplates[unit] != null) _actors.Add(Spawn(unit, col, row));
                else if (totem >= 0) totemCells[totem] = new Vector2Int(col, row);
            }
        }

        for (int i = 0; i < totemCells.Length; i++)
        {
            if (totemCells[i] is not Vector2Int cell) continue;
            SpawnTotem(i, cell);
            foreach (var off in TotemRanges[i])
            {
                var target = cell + off;
                if (target.x < 0 || target.x >= Columns || target.y < 0 || target.y >= Rows) continue;
                Paint(target, _totemColors[i]);
                var actor = _actors.Find(a => a.Column == target.x && a.Row == target.y);
                if (actor != null && !actor.Buffs.Contains(i)) actor.Buffs.Add(i);
            }
        }

        foreach (var a in _actors) BuildActorFx(a);
        _nextSkill = _skillInterval * 0.5f;
    }

    private Actor Spawn(int code, int col, int row)
    {
        var cell = CellCenter(col, row);
        var unit = Instantiate(_unitTemplates[code], _boardRoot);
        unit.name = $"{_unitTemplates[code].name}_{col}_{row}";
        unit.SetActive(true);
        var a = new Actor
        {
            Code = code, Column = col, Row = row, Unit = unit.transform,
            Renderers = unit.GetComponentsInChildren<Renderer>(true),
            Skeleton = unit.GetComponentInChildren<SkeletonAnimation>(true),
            Home = new Vector3(cell.x, cell.y + _unitOffsetY, 0f),
            BaseScale = _unitTemplates[code].transform.localScale,
            Face = col < Columns / 2 ? 1 : -1,
            Phase = (col * 0.37f + row * 0.61f) % 1f,
        };
        a.NextAttack = a.Phase * _attackInterval;

        int template = DroneTemplateByUnit[code];
        if (template >= 0 && _droneTemplates[template] != null)
        {
            for (int i = 0; i < DroneCountByUnit[code]; i++)
            {
                var go = Instantiate(_droneTemplates[template], _boardRoot);
                go.name = $"{unit.name}_Drone{i}";
                go.SetActive(true);
                var d = new Drone
                {
                    Body = go.transform,
                    Renderers = go.GetComponentsInChildren<Renderer>(true),
                    BaseScale = _droneTemplates[template].transform.localScale,
                    Skeleton = go.GetComponentInChildren<SkeletonAnimation>(true),
                    Template = template,
                    Side = i % 2 == 0 ? -1f : 1f,
                };
                d.Trail = NewTrail(go.transform, _droneTrailWidth, 0.18f);
                a.Drones.Add(d);
            }
        }
        return a;
    }

    private void SpawnTotem(int index, Vector2Int cell)
    {
        var center = CellCenter(cell.x, cell.y);
        var template = index < _totemTemplates.Length ? _totemTemplates[index] : null;
        Transform body;
        if (template != null)
        {
            var go = Instantiate(template, _boardRoot);
            go.name = $"Totem{index + 1}_{template.name}";
            go.SetActive(true);
            body = go.transform;
        }
        else
        {
            body = new GameObject($"Totem{index + 1}").transform;
            body.SetParent(_boardRoot, false);
            var sr = body.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = _discSprite;
            sr.color = _totemColors[index];
            body.localScale = new Vector3(0.6f, 0.9f, 1f);
        }
        body.position = new Vector3(center.x, center.y + _unitOffsetY, 0f);
        SetSorting(body.GetComponentsInChildren<Renderer>(true), UnitSortingLayer, Mathf.RoundToInt(-body.position.y * 100f));
        _totemBodies.Add(body);
        _totemTops[index] = body.position + new Vector3(0f, BodyHeight + 0.15f, 0f);
        Paint(cell, _totemColors[index]);
    }

    private void Paint(Vector2Int cell, Color color)
    {
        var center = CellCenter(cell.x, cell.y);
        var go = new GameObject($"Paint_{cell.x}_{cell.y}", typeof(SpriteRenderer));
        go.transform.SetParent(_boardRoot, false);
        go.transform.position = new Vector3(center.x, center.y, 0f);
        go.transform.localScale = Vector3.one * CellSize;
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = _squareSprite;
        sr.color = new Color(color.r, color.g, color.b, _paintAlpha);
        sr.sortingLayerName = UnitSortingLayer;
        sr.sortingOrder = PaintOrder;
        sr.enabled = _totems;
        if (!_paint.TryGetValue(cell, out var list)) _paint[cell] = list = new List<SpriteRenderer>();
        list.Add(sr);
    }

    private void BuildActorFx(Actor a)
    {
        a.Trail = NewTrail(a.Unit, _unitTrailWidth, 0.22f, BodyHeight);
        a.Tethers = new LineRenderer[a.Buffs.Count];
        for (int i = 0; i < a.Buffs.Count; i++)
        {
            var go = new GameObject($"{a.Unit.name}_Tether{i}", typeof(LineRenderer));
            go.transform.SetParent(_boardRoot, false);
            var lr = go.GetComponent<LineRenderer>();
            lr.sharedMaterial = _lineMaterial;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.05f;
            lr.numCapVertices = 3;
            lr.sortingLayerName = FxSortingLayer;
            lr.sortingOrder = 5;
            lr.enabled = false;
            a.Tethers[i] = lr;
        }
        var mark = new GameObject($"{a.Unit.name}_Home", typeof(SpriteRenderer));
        mark.transform.SetParent(_boardRoot, false);
        mark.transform.position = a.Home + new Vector3(0f, 0.02f, 0f);
        mark.transform.localScale = new Vector3(0.75f, 0.3f, 1f);
        a.HomeMark = mark.GetComponent<SpriteRenderer>();
        a.HomeMark.sprite = _discSprite;
        a.HomeMark.sortingLayerName = UnitSortingLayer;
        a.HomeMark.sortingOrder = HomeMarkOrder;
        a.HomeMark.enabled = false;
    }

    private TrailRenderer NewTrail(Transform parent, float width, float time, float height = 0f)
    {
        var go = new GameObject("Trail", typeof(TrailRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.position = parent.position + new Vector3(0f, height, 0f);
        var tr = go.GetComponent<TrailRenderer>();
        tr.sharedMaterial = _lineMaterial;
        tr.time = time;
        tr.minVertexDistance = 0.03f;
        tr.widthMultiplier = width;
        tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        tr.numCapVertices = 4;
        tr.sortingLayerName = FxSortingLayer;
        tr.sortingOrder = 0;
        tr.emitting = false;
        return tr;
    }

    private Vector2 CellCenter(int col, int row) => _cellOrigin + new Vector2(col * _cellStep, row * _cellStep);

    // ── 공격·스킬 일정 ──────────────────────────────────────────────

    private void UpdateAttacks()
    {
        var kind = Kind;
        foreach (var a in _actors)
        {
            if (_clock < a.NextAttack) continue;
            a.NextAttack += _attackInterval;
            if (a.Motion == Motion.Leap) continue;
            a.AttackCount++;
            PlayAttack(a.Skeleton);

            switch (kind)
            {
                case SortieLabMotionKind.Lunge:
                    StartMotion(a, Motion.Lunge);
                    break;
                case SortieLabMotionKind.DroneSortie:
                    if (a.Drones.Count > 0) { LaunchDrones(a); StartMotion(a, Motion.Recoil); }
                    else FireFrom(a);
                    break;
                case SortieLabMotionKind.Pulse:
                    StartMotion(a, Motion.Recoil);
                    FireFrom(a);
                    break;
                case SortieLabMotionKind.Mixed:
                    if (a.Code == 0 && a.AttackCount % 3 == 0) { LaunchDrones(a); StartMotion(a, Motion.Recoil); }
                    else StartMotion(a, Motion.Lunge);
                    break;
                default:
                    FireFrom(a);
                    break;
            }
        }
    }

    private void UpdateSkill()
    {
        var kind = Kind;
        if (kind != SortieLabMotionKind.SkillLeap && kind != SortieLabMotionKind.Mixed) return;
        if (_clock < _nextSkill) return;
        _nextSkill = _clock + _skillInterval;
        var a = PickIdleActor();
        if (a != null) StartLeap(a);
    }

    private Actor PickIdleActor()
    {
        var idle = _actors.FindAll(x => x.Motion == Motion.None || x.Motion == Motion.Recoil);
        return idle.Count == 0 ? null : idle[Random.Range(0, idle.Count)];
    }

    private void StartLeap(Actor a)
    {
        StartMotion(a, Motion.Leap);
        float x = Mathf.Lerp(a.Home.x, _bossCenter.x, 0.6f);
        a.SlamPoint = new Vector3(x, _bossCenter.y - _slamBelowBoss, 0f);
        foreach (var d in a.Drones) d.SortieStart = -1f;
    }

    private void StartMotion(Actor a, Motion motion)
    {
        a.Motion = motion;
        a.MotionStart = _clock;
        a.MotionFired = false;
    }

    private void LaunchDrones(Actor a)
    {
        for (int i = 0; i < a.Drones.Count; i++)
        {
            var d = a.Drones[i];
            d.SortieStart = _clock + i * 0.07f;
            d.SortieFired = false;
            d.SortieFrom = d.Body.position;
            float spread = a.Drones.Count == 1 ? 0f : Mathf.Lerp(-0.6f, 0.6f, i / (float)(a.Drones.Count - 1));
            d.SortieTarget = new Vector3(_bossCenter.x + spread + (a.Home.x - _bossCenter.x) * 0.25f, _bossCenter.y - 0.85f, 0f);
        }
    }

    // ── 움직임 ───────────────────────────────────────────────────────

    private void UpdateActor(Actor a)
    {
        Vector3 offset = Vector3.zero;
        float scaleMul = 1f;
        float squashY = 1f;
        int face = a.Face;
        bool front = false;
        float u = 0f;

        switch (a.Motion)
        {
            case Motion.Lunge:
            {
                u = (_clock - a.MotionStart) / _lungeSeconds;
                const float Peak = 0.25f;
                float k = u < Peak ? EaseOut(u / Peak) : 1f - EaseInOut((u - Peak) / (1f - Peak));
                offset = new Vector3(0f, _lungeDistance * k, 0f);
                squashY = 1f + 0.06f * k;
                if (!a.MotionFired && u >= Peak) { a.MotionFired = true; FireFrom(a, offset); }
                break;
            }
            case Motion.Recoil:
            {
                u = (_clock - a.MotionStart) / 0.25f;
                offset = new Vector3(0f, -_recoil * Mathf.Sin(Mathf.PI * Mathf.Clamp01(u)), 0f);
                break;
            }
            case Motion.Leap:
            {
                u = (_clock - a.MotionStart) / _leapSeconds;
                front = true;
                Vector3 slamOffset = a.SlamPoint - a.Home;
                if (Mathf.Abs(slamOffset.x) > 0.05f) face = slamOffset.x > 0f ? 1 : -1;
                if (u < 0.12f)
                {
                    squashY = 1f - 0.18f * Mathf.Sin(Mathf.PI * u / 0.12f);
                    front = false;
                }
                else if (u < 0.48f)
                {
                    float p = EaseInOut((u - 0.12f) / 0.36f);
                    offset = Bezier(Vector3.zero, slamOffset * 0.5f + new Vector3(0f, _leapHeight, 0f), slamOffset, p);
                    scaleMul = 1f + _leapScale * p;
                    squashY = 1f + 0.12f * Mathf.Sin(Mathf.PI * p);
                }
                else if (u < 0.62f)
                {
                    offset = slamOffset;
                    scaleMul = 1f + _leapScale;
                    if (!a.MotionFired) { a.MotionFired = true; Slam(a); }
                    squashY = 1f - 0.15f * Mathf.Sin(Mathf.PI * (u - 0.48f) / 0.14f);
                }
                else
                {
                    float p = EaseInOut((u - 0.62f) / 0.38f);
                    offset = Bezier(slamOffset, slamOffset * 0.5f + new Vector3(0f, _leapHeight * 0.6f, 0f), Vector3.zero, p);
                    scaleMul = 1f + _leapScale * (1f - p);
                    face = -face;
                }
                break;
            }
        }
        if (a.Motion != Motion.None && u >= 1f) a.Motion = Motion.None;

        if (Kind == SortieLabMotionKind.Pulse && a.Motion != Motion.Leap)
        {
            float wave = Mathf.Max(0f, Mathf.Sin(_clock / _pulseBeat * Mathf.PI * 2f - a.Row * 0.9f));
            offset.y += _pulseAmplitude * wave * wave;
            squashY *= 1f + 0.05f * wave * wave;
        }

        var pos = a.Home + offset;
        a.Unit.position = pos;
        float sx = Mathf.Abs(a.BaseScale.x) * scaleMul / Mathf.Sqrt(squashY);
        a.Unit.localScale = new Vector3(face > 0 ? -sx : sx, a.BaseScale.y * scaleMul * squashY, a.BaseScale.z);
        int order = front ? LeapOrder : Mathf.RoundToInt(-a.Home.y * 100f);
        SetSorting(a.Renderers, UnitSortingLayer, order);

        float away = offset.magnitude;
        bool moving = away > AwayThreshold;
        ApplyTrail(a.Trail, a.Buffs, moving, _buffMode >= 2);
        UpdateTethers(a, pos, away);
        if (a.HomeMark != null)
        {
            float alpha = Mathf.Clamp01((away - AwayThreshold) / 0.3f) * 0.55f;
            a.HomeMark.enabled = _homeMarks && alpha > 0.01f;
            var c = _buffMode >= 2 && a.Buffs.Count > 0 && _totems ? _totemColors[a.Buffs[0]] : Color.white;
            a.HomeMark.color = new Color(c.r, c.g, c.b, alpha);
        }

        for (int i = 0; i < a.Drones.Count; i++) UpdateDrone(a, a.Drones[i], i, pos, order);
    }

    private void UpdateDrone(Actor a, Drone d, int i, Vector3 unitPos, int unitOrder)
    {
        int n = a.Drones.Count;
        float angle = (_clock / OrbitPeriod + i / (float)Mathf.Max(1, n)) * Mathf.PI * 2f;
        float sin = Mathf.Sin(angle);
        var orbit = unitPos + new Vector3(Mathf.Cos(angle) * OrbitRadiusX, OrbitHeight + sin * OrbitRadiusY, 0f);
        int order = unitOrder + (sin > 0f ? -1 : 1);
        float targetFacing = -sin > 0f ? -1f : 1f;
        Vector3 pos = orbit;
        bool sortie = false;

        if (d.SortieStart >= 0f && _clock >= d.SortieStart)
        {
            float u = (_clock - d.SortieStart) / _droneSortieSeconds;
            if (u >= 1f) d.SortieStart = -1f;
            else
            {
                sortie = true;
                var bulge = new Vector3(d.Side * 0.8f, 0f, 0f);
                if (u < 0.4f)
                {
                    float p = EaseInOut(u / 0.4f);
                    pos = Bezier(d.SortieFrom, (d.SortieFrom + d.SortieTarget) * 0.5f + bulge, d.SortieTarget, p);
                }
                else if (u < 0.5f)
                {
                    pos = d.SortieTarget + new Vector3(0f, 0.04f * Mathf.Sin((u - 0.4f) * 60f), 0f);
                    if (!d.SortieFired)
                    {
                        d.SortieFired = true;
                        PlayAttack(d.Skeleton);
                        Fire(a, pos, BoltTint(a, d.Template));
                    }
                }
                else
                {
                    float p = EaseInOut((u - 0.5f) / 0.5f);
                    pos = Bezier(d.SortieTarget, (d.SortieTarget + orbit) * 0.5f - bulge, orbit, p);
                }
            }
        }

        if (sortie)
        {
            float dx = pos.x - d.LastPosition.x;
            if (Mathf.Abs(dx) > 0.001f) targetFacing = dx > 0f ? -1f : 1f;
            order = SortieDroneOrder + i;
        }
        d.Facing = Mathf.MoveTowards(d.Facing, targetFacing, 2f * Time.deltaTime / 0.15f);
        d.LastPosition = pos;
        d.Body.position = pos;
        d.Body.localScale = new Vector3(d.Facing * Mathf.Abs(d.BaseScale.x), d.BaseScale.y, d.BaseScale.z);
        SetSorting(d.Renderers, UnitSortingLayer, order);
        ApplyTrail(d.Trail, a.Buffs, sortie, _buffMode >= 2);
    }

    private void UpdateTethers(Actor a, Vector3 pos, float away)
    {
        bool show = _buffMode >= 3 && _totems && away > AwayThreshold;
        for (int i = 0; i < a.Tethers.Length; i++)
        {
            var lr = a.Tethers[i];
            lr.enabled = show;
            if (!show) continue;
            var color = _totemColors[a.Buffs[i]];
            float alpha = Mathf.Clamp01((away - AwayThreshold) / 0.25f) * 0.75f;
            lr.SetPosition(0, _totemTops.TryGetValue(a.Buffs[i], out var top) ? top : a.Home);
            lr.SetPosition(1, pos + new Vector3(0f, BodyHeight, 0f));
            lr.startColor = new Color(color.r, color.g, color.b, alpha);
            lr.endColor = new Color(color.r, color.g, color.b, alpha * 0.35f);
        }
    }

    private void ApplyTrail(TrailRenderer trail, List<int> buffs, bool emitting, bool colored)
    {
        if (trail == null) return;
        trail.emitting = emitting;
        if (!emitting) return;
        Color a = _neutralTrail, b = _neutralTrail;
        if (colored && _totems && buffs.Count > 0)
        {
            a = _totemColors[buffs[0]];
            b = _totemColors[buffs[buffs.Count - 1]];
        }
        trail.startColor = new Color(a.r, a.g, a.b, colored && buffs.Count > 0 ? 0.6f : a.a);
        trail.endColor = new Color(b.r, b.g, b.b, 0f);
    }

    // ── 사격·충격 ────────────────────────────────────────────────────

    private void FireFrom(Actor a, Vector3 offset = default)
    {
        if (a.Drones.Count == 0)
        {
            Fire(a, a.Home + offset + new Vector3(0f, BodyHeight + 0.1f, 0f), BoltTint(a, 0));
            return;
        }
        foreach (var d in a.Drones)
        {
            if (d.SortieStart >= 0f) continue;
            PlayAttack(d.Skeleton);
            Fire(a, d.Body.position, BoltTint(a, d.Template));
        }
    }

    private Color BoltTint(Actor a, int template)
    {
        if (_buffMode >= 1 && _totems && a.Buffs.Count > 0)
            return _totemColors[a.Buffs[a.AttackCount % a.Buffs.Count]];
        return _boltTints[Mathf.Clamp(template, 0, _boltTints.Length - 1)];
    }

    private void Fire(Actor a, Vector3 origin, Color tint)
    {
        var target = (Vector3)(_bossCenter + new Vector2(Random.Range(-_bossExtents.x, _bossExtents.x), Random.Range(-_bossExtents.y, _bossExtents.y)));
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
        bolt.Tint = tint;
        bolt.Active = true;
        if (_buffMode >= 3 && _totems && a.Buffs.Count > 0) _paintFlash[new Vector2Int(a.Column, a.Row)] = 1f;
    }

    private void Slam(Actor a)
    {
        if (_bossReact) _bossFlinch = 1f;
        if (_cameraPunch) _shake = 1f;
        var tint = _buffMode >= 1 && _totems && a.Buffs.Count > 0 ? _totemColors[a.Buffs[0]] : Color.white;
        var ring = _rings.Find(r => r.Renderer != null && !r.Renderer.enabled);
        if (ring == null)
        {
            var go = new GameObject($"Shockwave_{_rings.Count}", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            ring = new Ring { Renderer = go.GetComponent<SpriteRenderer>() };
            ring.Renderer.sprite = _ringSprite;
            ring.Renderer.sortingLayerName = FxSortingLayer;
            ring.Renderer.sortingOrder = 20;
            _rings.Add(ring);
        }
        ring.Start = _clock;
        ring.Tint = tint;
        ring.Renderer.transform.position = a.SlamPoint + new Vector3(0f, 0.15f, 0f);
        ring.Renderer.enabled = true;
        if (_buffMode >= 3 && _totems && a.Buffs.Count > 0) _paintFlash[new Vector2Int(a.Column, a.Row)] = 1f;
    }

    private void UpdateBolts(float dt)
    {
        foreach (var b in _bolts)
        {
            if (!b.Active) continue;
            b.Elapsed += dt;
            float k = Mathf.Clamp01(b.Elapsed / Mathf.Max(0.01f, _boltDuration));
            Vector3 tip = Vector3.Lerp(b.From, b.To, k);
            Vector3 tail = tip - Vector3.ClampMagnitude(tip - b.From, _boltSegment);
            b.Visual.Draw(tail, tip, b.Tint, _boltWidth, 1f, 0f, 0.7f);
            if (k < 1f) continue;
            b.Active = false;
            b.Visual.Clear();
            if (_bossReact) _bossFlinch = Mathf.Max(_bossFlinch, 0.3f);
        }
    }

    private void UpdateRings()
    {
        const float Life = 0.4f;
        foreach (var r in _rings)
        {
            if (!r.Renderer.enabled) continue;
            float u = (_clock - r.Start) / Life;
            if (u >= 1f) { r.Renderer.enabled = false; continue; }
            float s = Mathf.Lerp(0.3f, 2.2f, EaseOut(u));
            r.Renderer.transform.localScale = new Vector3(s, s * 0.45f, 1f);
            r.Renderer.color = new Color(r.Tint.r, r.Tint.g, r.Tint.b, 1f - u);
        }
    }

    private void UpdatePaint(float dt)
    {
        foreach (var kv in _paint)
        {
            _paintFlash.TryGetValue(kv.Key, out float flash);
            foreach (var sr in kv.Value)
            {
                var c = sr.color;
                sr.color = new Color(c.r, c.g, c.b, _paintAlpha + flash * 0.4f);
            }
            if (flash > 0f) _paintFlash[kv.Key] = Mathf.Max(0f, flash - dt * 4f);
        }
    }

    private void UpdateBoss(float dt)
    {
        if (_boss == null) return;
        _bossFlinch = Mathf.Max(0f, _bossFlinch - dt * 5f);
        float f = _bossReact ? _bossFlinch : 0f;
        _boss.position = _bossBasePosition + new Vector3(0f, 0.06f * f, 0f);
        _boss.localScale = _bossBaseScale * (1f + 0.04f * f);
        if (_bossRenderer != null) _bossRenderer.color = Color.Lerp(Color.white, BossHitTint, f);
    }

    private void UpdateCamera(float dt)
    {
        if (_camera == null) return;
        _shake = Mathf.Max(0f, _shake - dt * 4f);
        float s = _cameraPunch ? _shake * _shake : 0f;
        var jitter = (Vector3)(Random.insideUnitCircle * 0.09f * s);
        _camera.transform.position = _cameraBasePosition + jitter;
        _camera.fieldOfView = _cameraBaseFov - 1.8f * s;
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

    // ── 상태 줄 ──────────────────────────────────────────────────────

    private void Refresh()
    {
        var v = Current;
        if (_status != null && v != null)
        {
            string totem = _totems ? $"<color=#{ColorUtility.ToHtmlStringRGB(_totemColors[0])}>■{_totemLabels[0]}(십자)</color> <color=#{ColorUtility.ToHtmlStringRGB(_totemColors[1])}>■{_totemLabels[1]}(좌우2)</color>" : "끔";
            _status.text = $"<b>{v.Label}</b>  {v.Description}"
                + $"\n토템: {totem} · 버프색: {BuffModeNames[_buffMode]} · 발판 {OnOff(_homeMarks)} · 보스 반응 {OnOff(_bossReact)} · 카메라 {OnOff(_cameraPunch)} · 속도 ×{SpeedSteps[_speedStep]:0.##}";
        }
        for (int i = 0; i < _variantButtons.Length; i++)
            if (_variantButtons[i] != null) _variantButtons[i].color = i == _variant ? ButtonSelected : ButtonIdle;
    }

    private static string OnOff(bool on) => on ? "켬" : "끔";

    // ── 도우미 ───────────────────────────────────────────────────────

    private static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }

    private static float EaseInOut(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    private static Vector3 Bezier(Vector3 a, Vector3 c, Vector3 b, float t)
    {
        float m = 1f - t;
        return m * m * a + 2f * m * t * c + t * t * b;
    }

    private static void SetSorting(Renderer[] renderers, string layer, int order)
    {
        int id = SortingLayer.NameToID(layer);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.sortingLayerID = id;
            r.sortingOrder = order;
        }
    }

    private const int SpriteSize = 64;

    private static float Square(float x, float y) => Mathf.Clamp01(Mathf.Min(1f - Mathf.Abs(x), 1f - Mathf.Abs(y)) * 12f);

    private static float Disc(float x, float y) => Mathf.Clamp01((1f - Mathf.Sqrt(x * x + y * y)) * 2.5f);

    private static float RingShape(float x, float y)
    {
        float r = Mathf.Sqrt(x * x + y * y);
        return Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) * 7f);
    }

    private static Sprite MakeSprite(System.Func<float, float, float> alpha, string name)
    {
        var tex = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[SpriteSize * SpriteSize];
        for (int y = 0; y < SpriteSize; y++)
            for (int x = 0; x < SpriteSize; x++)
            {
                float nx = (x + 0.5f) / SpriteSize * 2f - 1f;
                float ny = (y + 0.5f) / SpriteSize * 2f - 1f;
                pixels[y * SpriteSize + x] = new Color32(255, 255, 255, (byte)(alpha(nx, ny) * 255f));
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, SpriteSize, SpriteSize), new Vector2(0.5f, 0.5f), SpriteSize);
    }
}
