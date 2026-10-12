using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;
using VContainer;

/// <summary>Recommended combo: charge blocks, impact glitch, boss RGB distortion, then data shards arriving at the timer.</summary>
public sealed class DisigmanGlitchLinkPresentation : MonoBehaviour
{
    private const float ChargeBlockStart = .35f;
    private const float ShardStagger = .012f;
    private const float ShardLifePad = .2f;
    private const float RingLife = .25f;
    [SerializeField] private DisigmanGlitchLinkSettings _settings;
    [SerializeField] private TimerBonusLab _hack;
    [SerializeField] private RectTransform _stage;
    [SerializeField] private RectTransform _shardLayer;
    [SerializeField] private Camera _worldCamera;
    [Inject] private TimerController _timer;
    [Inject] private BossManager _bosses;
    [Inject] private WaveManager _waves;
    private WorldGlitchBlocks _blocks;
    private TimerBonusSprites _sprites;
    private TimerBonusParticles _particles;
    private BossBase _visualBoss;
    private BossGlitchFx _spriteGlitch;
    private DisigmanSpineGlitchFx _spineGlitch;
    private readonly List<Shot> _shots = new List<Shot>();
    private int _generation;

    private sealed class Shot
    {
        public RailgunProjectile Owner;
        public Vector3 From;
        public BossBase Boss;
        public float Seconds;
        public float Charge;
        public float Elapsed;
        public bool Charged;
    }

    /// <summary>Whether this scene has the authored effect and both world/HUD endpoints.</summary>
    public bool IsConfigured => isActiveAndEnabled && _settings != null && _settings.ShardCount > 0
        && _settings.LinkPalette != null && _settings.LinkPalette.Length > 0 && _hack != null && _hack.IsRuntime
        && _stage != null && _shardLayer != null && _worldCamera != null;

    private void Start()
    {
        EnsureResources();
        if (_waves != null) { _waves.OnRunStopped += Cancel; _waves.OnWaveChanged += OnWaveChanged; }
    }

    private void EnsureResources()
    {
        if (!IsConfigured || _blocks != null) return;
        _blocks = new WorldGlitchBlocks(transform, _settings.BlockSortingOrder);
        _sprites = new TimerBonusSprites();
        _particles = new TimerBonusParticles(_shardLayer, _sprites.SoftDot, _sprites.Ring);
    }

    /// <summary>Tracks the actual skill projectile; basic shots never call this path.</summary>
    public void Begin(RailgunProjectile owner, Vector3 from, float charge, float seconds)
    {
        if (!IsConfigured || owner == null || seconds <= 0f) return;
        EnsureResources();
        _shots.Add(new Shot { Owner = owner, From = from, Boss = _bosses != null ? _bosses.CurrentBoss : null, Seconds = seconds, Charge = charge });
    }

    /// <summary>Called by the real projectile at impact. The authoritative time was already added by TryCast.</summary>
    public void Impact(RailgunProjectile owner, Vector3 target)
    {
        for (int i = _shots.Count - 1; i >= 0; i--)
        {
            var shot = _shots[i];
            if (shot.Owner != owner) continue;
            _shots.RemoveAt(i);
            if (!IsConfigured || shot.Boss == null || shot.Boss.IsDead || shot.Boss != _bosses?.CurrentBoss) return;
            var random = Random.state;
            try
            {
                BeamGlitch(shot.From, target);
                ImpactBlocks(target);
                GlitchBoss(shot.Boss);
                Shards(target, shot.Seconds, _generation);
            }
            finally { Random.state = random; }
            return;
        }
    }

    private void GlitchBoss(BossBase boss)
    {
        if (_visualBoss != boss)
        {
            ReleaseBoss();
            _visualBoss = boss;
            var spine = boss.GetComponentInChildren<SkeletonRenderer>();
            if (spine != null) _spineGlitch = new DisigmanSpineGlitchFx(spine, _settings);
            else
            {
                var sprite = boss.GetComponentInChildren<SpriteRenderer>();
                if (sprite != null) _spriteGlitch = new BossGlitchFx(sprite, _settings.Cyan, _settings.Hot);
            }
        }
        _spineGlitch?.Trigger();
        _spriteGlitch?.Trigger(_settings.BossGlitchSeconds, _settings.BossJitter, _settings.BossGhostOffset, _settings.BossGlitchAmount);
    }

    private void Shards(Vector3 world, float seconds, int generation)
    {
        float px = _hack.Px;
        Vector2 from = ToStage(world), to = _hack.TimerPosition;
        _particles.Emit(new TimerBonusParticles.Particle
        {
            Kind = TimerBonusParticles.Kind.Ring, Position = from, RadiusFrom = 8f * px, RadiusTo = 60f * px,
            Color = Color.white, MaxLife = RingLife,
        });
        for (int i = 0; i < _settings.ShardCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float delay = _settings.ShardScatterSeconds + i * ShardStagger;
            var particle = new TimerBonusParticles.Particle
            {
                Kind = TimerBonusParticles.Kind.Home, Position = from,
                Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(_settings.ShardSpeed.x, _settings.ShardSpeed.y) * px,
                Drag = .12f, Size = Random.Range(_settings.ShardSize.x, _settings.ShardSize.y) * px,
                HomeDelay = delay, HomeDuration = _settings.ShardTravel, Target = to,
                Curve = Random.Range(-_settings.ShardCurve, _settings.ShardCurve) * px,
                MaxLife = delay + _settings.ShardTravel + ShardLifePad,
                Color = _settings.LinkPalette[i % _settings.LinkPalette.Length],
            };
            if (i == 0) particle.OnArrive = _ =>
            {
                if (generation == _generation && IsConfigured && _timer != null)
                    _hack.PresentHack(_timer, seconds, _hack.TimerPosition);
            };
            _particles.Emit(particle);
        }
    }

    private void BeamGlitch(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        Vector3 n = new Vector3(-d.y, d.x, 0f).normalized;
        for (int i = 0; i < _settings.BeamBlocks; i++)
        {
            Vector3 pos = from + d * Random.value + n * Random.Range(-.14f, .14f);
            _blocks.Spawn(pos, new Vector2(Random.Range(.25f, .7f), Random.Range(.05f, .11f)), Pick(), Random.Range(.12f, .24f), .14f);
        }
    }

    private void ImpactBlocks(Vector3 at)
    {
        for (int i = 0; i < _settings.ImpactBlocks; i++)
        {
            float size = Random.Range(.1f, .24f);
            _blocks.Spawn(at + (Vector3)(Random.insideUnitCircle * .6f), new Vector2(size, size), Pick(), Random.Range(.18f, .35f), .08f);
        }
        for (int i = 0; i < _settings.TearLines; i++)
            _blocks.Spawn(at + new Vector3(0f, Random.Range(-.5f, .5f), 0f), new Vector2(Random.Range(1.2f, 2.2f), .025f),
                i % 2 == 0 ? _settings.Hot : Color.white, .14f, .25f);
    }

    private void ChargeBlocks(Vector3 at, float life)
    {
        for (int i = 0; i < _settings.ChargeBlocks; i++)
        {
            float size = Random.Range(.05f, .12f);
            _blocks.Spawn(at + (Vector3)(Random.insideUnitCircle * .35f), new Vector2(size, size), Pick(), life, .05f);
        }
    }

    private Color Pick() => _settings.LinkPalette[Random.Range(0, _settings.LinkPalette.Length)];
    private Vector2 ToStage(Vector3 world) => DamageStyleLabUtil.ScreenToLocal(_stage, RectTransformUtility.WorldToScreenPoint(_worldCamera, world));

    private void Update()
    {
        float dt = Time.deltaTime;
        if (!IsConfigured || dt <= 0f) return;
        for (int i = _shots.Count - 1; i >= 0; i--)
        {
            var shot = _shots[i];
            if (shot.Owner == null || !shot.Owner.isActiveAndEnabled || shot.Boss == null || shot.Boss.IsDead || shot.Boss != _bosses?.CurrentBoss)
            { _shots.RemoveAt(i); continue; }
            shot.Elapsed += dt;
            if (!shot.Charged && shot.Elapsed >= shot.Charge * ChargeBlockStart)
            {
                shot.Charged = true;
                var random = Random.state;
                try { ChargeBlocks(shot.From, Mathf.Max(0f, shot.Charge - shot.Elapsed)); }
                finally { Random.state = random; }
            }
        }
        _blocks?.Tick(dt);
        _particles?.Tick(dt);
    }

    private void LateUpdate()
    {
        if (_visualBoss != null && _visualBoss != _bosses?.CurrentBoss) ReleaseBoss();
        _spriteGlitch?.Tick(Time.deltaTime);
        _spineGlitch?.Tick(Time.deltaTime);
    }

    private void ReleaseBoss()
    {
        _spriteGlitch?.Dispose(); _spriteGlitch = null;
        _spineGlitch?.Dispose(); _spineGlitch = null;
        _visualBoss = null;
    }
    private void OnWaveChanged(int _) => Cancel();

    /// <summary>Cancels stale shard callbacks and returns all visible particles on scene/run exit.</summary>
    public void Cancel()
    {
        _generation++;
        _shots.Clear();
        _blocks?.Clear();
        _particles?.Clear();
        if (_hack != null && _hack.IsPresentingHack) _hack.CancelPresentation();
        ReleaseBoss();
    }
    private void OnDisable() => Cancel();
    private void OnDestroy()
    {
        if (_waves != null) { _waves.OnRunStopped -= Cancel; _waves.OnWaveChanged -= OnWaveChanged; }
        Cancel();
        _blocks?.Dispose();
        _sprites?.Dispose();
    }
}
