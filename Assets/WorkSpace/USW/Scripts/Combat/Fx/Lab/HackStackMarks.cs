using System;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// FxLab_HackBloom 전용 — 델탕 드론의 해킹 표식(빛점)을 악어 보스 Spine 뼈에 붙이고, 감망 신호에 맞춰 일부를 연쇄로 터뜨린다.
/// 표식은 맞은 자리의 뼈에 고정되어 보스 애니메이션을 따라 움직인다(린처럼 몸에 박혀 있는 느낌).
/// 표식 칸은 재사용한다: 터진 칸은 비워지고, 드론이 다시 맞히면 새 표식이 붙는다. 이미 표식이 찬 만큼이면 기존 표식에 맞아 번쩍인다.
/// 시각 전용: 전투 수치·시간 배율을 바꾸지 않는다. 모든 모양은 Time.time 기준으로 매 프레임 다시 계산한다.
/// </summary>
public sealed class HackStackMarks : MonoBehaviour
{
    /// <summary>보스 몸에 동시에 보이는 최대 표식 수.</summary>
    public const int Capacity = 12;
    private const int RaysPerPop = 6;
    private const int BoltPool = 6;
    private const float GoldenAngle = 137.508f;

    private sealed class Chip
    {
        public Renderer Glow, Core, Ring, Flash;
        public readonly LineRenderer[] Rays = new LineRenderer[RaysPerPop];
        public float AttachedAt = -1f, PopAt = -1f, IgniteAt = -1f, PingAt = -1f, Seed;
        public bool Reserved;
        public Vector3 PopPosition;
        public Bone Bone;
        public Vector2 Local;
        public bool Anchored;
    }

    private sealed class Bolt
    {
        public LineRenderer Line;
        public Renderer Head;
        public Vector3 From;
        public int Chip = -1;
        public float StartAt, Flight;
        public bool Active, NewMark;
    }

    [SerializeField] private SkeletonAnimation _boss;
    [Tooltip("표식이 붙을 수 있는 뼈 — 맞은 자리에서 가장 가까운 뼈에 고정된다")]
    [SerializeField] private string[] _anchorBones = { "body1", "body2", "head", "nose", "armL", "armR", "soulderL", "soulderR", "guntletL", "guntletR", "handL", "handR" };
    [Tooltip("표식 배치 중심 뼈")]
    [SerializeField] private string _centerBone = "body2";
    [Tooltip("중심 뼈 기준 보정 (월드). z는 보스 앞으로 당기는 값")]
    [SerializeField] private Vector3 _centerOffset = new Vector3(0f, .2f, -.65f);
    [Tooltip("표식이 맞는 자리를 고르는 타원 반경 (중심 기준, 월드)")]
    [SerializeField] private Vector2 _bodyRadius = new Vector2(1.55f, .95f);
    [SerializeField] private Material _lineMaterial;
    [SerializeField] private Material _glowMaterial;
    [SerializeField] private Material _ringMaterial;
    [Tooltip("해킹 표식·탄 기본색 (연한 하늘색)")]
    [SerializeField] private Color _hackColor = new Color(.38f, .86f, 1f);
    [Tooltip("기폭 직전 달아오른 색")]
    [SerializeField] private Color _hotColor = new Color(.9f, .98f, 1f);
    [Header("빛점 크기 (월드)")]
    [SerializeField] private float _glowSize = .75f;
    [SerializeField] private float _coreSize = .22f;
    [Header("시간 (초)")]
    [SerializeField] private float _attachPop = .12f;
    [SerializeField] private float _igniteTime = .1f;
    [SerializeField] private float _popTime = .26f;

    private readonly Chip[] _chips = new Chip[Capacity];
    private readonly Bolt[] _bolts = new Bolt[BoltPool];
    private MaterialPropertyBlock _block;
    private Bone _center;
    private bool _ready;

    /// <summary>탄이 보스에 닿은 순간 (표식 칸 번호). 새 표식이든 기존 표식 재명중이든 매번 불린다.</summary>
    public event Action<int> BoltHit;

    /// <summary>붙어 있고 아직 터지지 않은 표식 수.</summary>
    public int AttachedCount
    {
        get
        {
            int n = 0;
            foreach (var c in _chips) if (c != null && c.AttachedAt >= 0f && c.PopAt < 0f) n++;
            return n;
        }
    }

    /// <summary>날아가는 중인 새 표식 탄 수 (자리를 예약한 칸).</summary>
    public int ReservedCount
    {
        get
        {
            int n = 0;
            foreach (var c in _chips) if (c != null && c.Reserved) n++;
            return n;
        }
    }

    /// <summary>보스 몸 중심 (앞쪽, 애니메이션을 따라 움직인다).</summary>
    public Vector3 Center
    {
        get
        {
            var bone = CenterBone();
            Vector3 p = bone != null ? bone.GetWorldPosition(_boss.transform) : _boss.transform.position;
            return new Vector3(p.x, p.y, _boss.transform.position.z) + _centerOffset;
        }
    }

    private void Awake() => EnsurePool();
    private void OnDisable() => Clear();

    /// <summary>표식·탄을 모두 지우고 처음 상태로 되돌린다.</summary>
    public void Clear()
    {
        EnsurePool();
        foreach (var c in _chips) { Free(c); c.Anchored = false; }
        foreach (var b in _bolts) b.Active = false;
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
    }

    /// <summary>표식이 붙어 있고 아직 터지지 않았는지.</summary>
    public bool IsAttached(int chip) { var c = _chips[Mathf.Clamp(chip, 0, Capacity - 1)]; return c.AttachedAt >= 0f && c.PopAt < 0f; }

    /// <summary>표식 번호의 현재 월드 위치 (뼈를 따라 움직인 위치).</summary>
    public Vector3 ChipPosition(int chip) => Rest(Mathf.Clamp(chip, 0, Capacity - 1));

    /// <summary>빈 표식 칸 하나를 예약한다 (새 표식 탄의 목표). 없으면 -1.</summary>
    public int ReserveFreeSlot()
    {
        EnsurePool();
        for (int i = 0; i < Capacity; i++)
        {
            var c = _chips[i];
            if (c.Reserved || c.AttachedAt >= 0f) continue;
            c.Reserved = true;
            return i;
        }
        return -1;
    }

    /// <summary>붙어 있는 표식 중 하나 (pick으로 고름). 없으면 -1.</summary>
    public int PickAttached(int pick)
    {
        int count = AttachedCount;
        if (count == 0) return -1;
        pick = Mathf.Abs(pick) % count;
        for (int i = 0; i < Capacity; i++)
            if (IsAttached(i) && pick-- == 0) return i;
        return -1;
    }

    /// <summary>델탕 드론 해킹 탄 발사. newMark면 도착 때 그 칸에 새 표식이 붙고, 아니면 기존 표식이 번쩍인다. 도착 때 <see cref="BoltHit"/>.</summary>
    public void LaunchBolt(Vector3 from, int chip, float flight, bool newMark)
    {
        EnsurePool();
        chip = Mathf.Clamp(chip, 0, Capacity - 1);
        Anchor(chip);
        Bolt slot = null;
        foreach (var b in _bolts) if (!b.Active) { slot = b; break; }
        if (slot == null) slot = _bolts[0];
        slot.Active = true; slot.From = from; slot.Chip = chip; slot.NewMark = newMark;
        slot.StartAt = Time.time; slot.Flight = Mathf.Max(.02f, flight);
    }

    /// <summary>이미 쌓여 있던 표식을 탄 없이 바로 붙인다. <see cref="BoltHit"/>는 부르지 않는다.</summary>
    public void AttachInstant(int chip)
    {
        EnsurePool();
        chip = Mathf.Clamp(chip, 0, Capacity - 1);
        Anchor(chip);
        var c = _chips[chip];
        c.AttachedAt = Time.time - 1f; c.PopAt = -1f; c.Reserved = false;
    }

    /// <summary>터질 표식이 하얗게 달아오른다 (기폭 예고).</summary>
    public void Ignite(int chip)
    {
        var c = _chips[Mathf.Clamp(chip, 0, Capacity - 1)];
        if (c.AttachedAt >= 0f) c.IgniteAt = Time.time;
    }

    /// <summary>표식 하나를 그 자리에서 터뜨린다.</summary>
    public void Pop(int chip)
    {
        chip = Mathf.Clamp(chip, 0, Capacity - 1);
        var c = _chips[chip];
        if (c.AttachedAt < 0f || c.PopAt >= 0f) return;
        c.PopPosition = Rest(chip);
        c.PopAt = Time.time;
    }

    private void LateUpdate()
    {
        if (!_ready) return;
        float now = Time.time;
        UpdateBolts(now);
        for (int i = 0; i < Capacity; i++) DrawChip(i, now);
    }

    // ── 뼈 고정 ───────────────────────────────────────────────

    // 맞을 자리를 몸 타원 안에 고르게 고르고, 가장 가까운 뼈의 로컬 좌표로 저장한다.
    private void Anchor(int i)
    {
        var c = _chips[i];
        if (c.Anchored) return;
        var skeleton = _boss.Skeleton;
        // 7은 12와 서로소 — 맞는 순서를 섞어 표식이 몇 개뿐이어도 몸 전체에 흩어진다.
        int k = i * 7 % Capacity;
        float r = Mathf.Sqrt((k + .5f) / Capacity);
        Vector3 d = Direction(k * GoldenAngle + 20f);
        Vector3 world = Center + new Vector3(d.x * _bodyRadius.x * r, d.y * _bodyRadius.y * r, 0f);
        Vector3 skel = _boss.transform.InverseTransformPoint(world);
        Bone best = null;
        float bestDistance = float.MaxValue;
        foreach (var name in _anchorBones)
        {
            var bone = skeleton?.FindBone(name);
            if (bone == null) continue;
            var pose = bone.AppliedPose;
            float distance = (new Vector2(pose.WorldX, pose.WorldY) - (Vector2)skel).sqrMagnitude;
            if (distance < bestDistance) { bestDistance = distance; best = bone; }
        }
        c.Bone = best;
        if (best != null) { best.AppliedPose.WorldToLocal(skel.x, skel.y, out float lx, out float ly); c.Local = new Vector2(lx, ly); }
        else c.Local = skel;
        c.Anchored = true;
    }

    private Vector3 Rest(int i)
    {
        var c = _chips[i];
        if (!c.Anchored) Anchor(i);
        float x = c.Local.x, y = c.Local.y;
        if (c.Bone != null) c.Bone.AppliedPose.LocalToWorld(c.Local.x, c.Local.y, out x, out y);
        Vector3 p = _boss.transform.TransformPoint(new Vector3(x, y, 0f));
        return new Vector3(p.x, p.y, Center.z);
    }

    private Bone CenterBone()
    {
        if (_center == null && _boss != null && _boss.Skeleton != null) _center = _boss.Skeleton.FindBone(_centerBone);
        return _center;
    }

    // ── 탄 ────────────────────────────────────────────────────

    private void UpdateBolts(float now)
    {
        foreach (var b in _bolts)
        {
            if (!b.Active) { b.Line.enabled = b.Head.enabled = false; continue; }
            float p = (now - b.StartAt) / b.Flight;
            Vector3 to = Rest(b.Chip);
            if (p >= 1f)
            {
                b.Active = false;
                b.Line.enabled = b.Head.enabled = false;
                var chip = _chips[b.Chip];
                if (b.NewMark && chip.AttachedAt < 0f) { chip.AttachedAt = now; chip.PopAt = -1f; }
                else if (chip.AttachedAt >= 0f && chip.PopAt < 0f) chip.PingAt = now;
                chip.Reserved = false;
                BoltHit?.Invoke(b.Chip);
                continue;
            }
            // 직선으로 빠르게 꽂히는 탄: 머리는 선명, 꼬리는 투명하게 끌린다.
            float eased = p * p;
            Vector3 head = Vector3.Lerp(b.From, to, eased);
            Vector3 tail = Vector3.Lerp(b.From, to, Mathf.Max(0f, eased - .35f));
            b.Line.enabled = true;
            b.Line.positionCount = 2;
            b.Line.SetPosition(0, tail);
            b.Line.SetPosition(1, head);
            b.Line.startColor = WithAlpha(_hackColor, 0f);
            b.Line.endColor = Color.white;
            b.Line.startWidth = .015f; b.Line.endWidth = .075f;
            Glow(b.Head, head, .4f, _hackColor, 1f);
        }
    }

    // ── 표식 ──────────────────────────────────────────────────

    private void DrawChip(int i, float now)
    {
        var c = _chips[i];
        if (c.AttachedAt < 0f) { HideChip(c); return; }
        if (c.PopAt >= 0f && now >= c.PopAt) { DrawPop(c, now); return; }

        // 빛점: 린 GIF처럼 몸에 맺힌 빛 덩어리. 테두리 없이 숨쉬듯 밝아졌다 어두워진다.
        Vector3 pos = Rest(i);
        float age = now - c.AttachedAt;
        float born = Mathf.Clamp01(age / _attachPop);
        float ping = c.PingAt < 0f ? 0f : 1f - Mathf.Clamp01((now - c.PingAt) / .2f);
        float popScale = 1f + .7f * (1f - born) * (1f - born) + .35f * ping * ping;
        float ignite = c.IgniteAt < 0f ? 0f : Mathf.Clamp01((now - c.IgniteAt) / _igniteTime);
        Color color = Color.Lerp(_hackColor, _hotColor, ignite);
        float pulse = .5f + .5f * Mathf.Sin(now * 5f + c.Seed * 6.283f);
        Glow(c.Glow, pos, (_glowSize + .15f * pulse + .35f * ignite) * popScale, color, .75f + .2f * pulse + .05f * ignite);
        Glow(c.Core, pos, (_coreSize + .05f * pulse + .14f * ignite) * popScale, Color.white * 1.2f, .95f);
        float ringAge = Mathf.Min(age, c.PingAt < 0f ? age : now - c.PingAt);
        if (ringAge < .2f)
        {
            float q = ringAge / .2f;
            Glow(c.Ring, pos, Mathf.Lerp(.12f, .55f, 1f - (1f - q) * (1f - q)), _hackColor, 1f - q);
        }
        else c.Ring.enabled = false;
        c.Flash.enabled = false;
        foreach (var r in c.Rays) r.enabled = false;
    }

    private void DrawPop(Chip c, float now)
    {
        float q = Mathf.Clamp01((now - c.PopAt) / _popTime);
        c.Glow.enabled = c.Core.enabled = false;
        if (q >= 1f) { Free(c); HideChip(c); return; }
        float eased = 1f - Mathf.Pow(1f - q, 3f);
        float fade = 1f - q;
        Vector3 pos = c.PopPosition;
        Glow(c.Ring, pos, Mathf.Lerp(.15f, 1.05f, eased), _hackColor, fade);
        Glow(c.Flash, pos, Mathf.Lerp(.85f, .1f, q), Color.white * 1.3f, fade * fade);
        Color ray = Color.Lerp(Color.white, _hackColor, q);
        for (int j = 0; j < RaysPerPop; j++)
        {
            Vector3 d = Direction(j * 60f + c.Seed * 40f);
            Path(c.Rays[j], ray, fade, .04f * fade, pos + d * (.12f + .25f * eased), pos + d * (.25f + .6f * eased));
        }
    }

    // ── 풀·그리기 도우미 ─────────────────────────────────────

    private void EnsurePool()
    {
        if (_ready) return;
        _ready = true;
        _block = new MaterialPropertyBlock();
        for (int i = 0; i < Capacity; i++)
        {
            var c = new Chip
            {
                Glow = Quad("ChipGlow_" + i, _glowMaterial, 54), Core = Quad("ChipCore_" + i, _glowMaterial, 55),
                Ring = Quad("ChipRing_" + i, _ringMaterial, 59), Flash = Quad("ChipFlash_" + i, _glowMaterial, 60),
                Seed = Hash(i * 13 + 7)
            };
            for (int j = 0; j < RaysPerPop; j++) c.Rays[j] = Line("ChipShard_" + i + "_" + j, 60);
            _chips[i] = c;
        }
        for (int i = 0; i < BoltPool; i++)
            _bolts[i] = new Bolt { Line = Line("HackBolt_" + i, 61), Head = Quad("HackBoltHead_" + i, _glowMaterial, 62) };
    }

    // 터진 칸을 비워 다음 표식이 같은 자리에 붙을 수 있게 한다.
    private static void Free(Chip c)
    {
        c.AttachedAt = c.PopAt = c.IgniteAt = c.PingAt = -1f;
        c.Reserved = false;
    }

    private static void HideChip(Chip c)
    {
        c.Glow.enabled = c.Core.enabled = c.Ring.enabled = c.Flash.enabled = false;
        foreach (var r in c.Rays) r.enabled = false;
    }

    private LineRenderer Line(string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var r = go.AddComponent<LineRenderer>();
        r.sharedMaterial = _lineMaterial; r.useWorldSpace = true; r.numCapVertices = 2;
        r.sortingLayerName = "FX"; r.sortingOrder = order;
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        r.enabled = false;
        return r;
    }

    private Renderer Quad(string name, Material material, int order)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = material; r.sortingLayerName = "FX"; r.sortingOrder = order;
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        r.enabled = false;
        return r;
    }

    private void Glow(Renderer r, Vector3 pos, float size, Color color, float alpha)
    {
        r.enabled = alpha > .005f;
        if (!r.enabled) return;
        r.transform.position = pos;
        r.transform.localScale = new Vector3(size, size, 1f);
        color.a = alpha;
        _block.Clear(); _block.SetColor("_Color", color); r.SetPropertyBlock(_block);
    }

    private static void Path(LineRenderer line, Color color, float alpha, float width, Vector3 from, Vector3 to)
    {
        line.enabled = alpha > .005f && width > .0005f;
        if (!line.enabled) return;
        color.a = alpha;
        line.startColor = line.endColor = color;
        line.startWidth = line.endWidth = width;
        line.positionCount = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
    }

    private static Vector3 Direction(float degrees)
        => new Vector3(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad), 0f);

    private static Color WithAlpha(Color c, float a) { c.a = a; return c; }

    // 0~1 결정적 난수 (프레임마다 같은 값 → 캡처 재현 가능)
    private static float Hash(int n)
    {
        unchecked
        {
            uint x = (uint)n * 747796405u + 2891336453u;
            x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
            return ((x >> 22) ^ x) / (float)uint.MaxValue;
        }
    }
}
