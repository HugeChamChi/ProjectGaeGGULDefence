using System.Collections.Generic;
using AssetKits.ParticleImage;
using AssetKits.ParticleImage.Enumerations;
using UnityEngine;
using VContainer;

/// <summary>보스 피해 경험치를 무작위 좌/우 한 경로로 운반하고 도착 시 한 번 지급한다.</summary>
[DefaultExecutionOrder(-100)]
public class ExpEffectController : MonoBehaviour
{
    private const int SampleCount = 96;
    private const float ParticleEndPadding = 0.05f;
    private const string TargetName = "ExpRouteFollower";
    [Inject] private BossManager _bossManager;
    [Inject] private ExpManager _expManager;
    [SerializeField] private ParticleImage particleImage;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform attractorTarget;
    [SerializeField] private RectTransform _routeCanvas;
    [SerializeField, Min(0.1f)] private float _travelDuration = 1.15f;
    [SerializeField, Min(0.02f)] private float _effectCooldown = 0.15f;
    [SerializeField] private bool _mirrorRight = true;
    [SerializeField] private ExpEffectRoute _leftRoute = new ExpEffectRoute();
    [SerializeField] private ExpEffectRoute _rightRoute = new ExpEffectRoute();
    [Header("테스트 (Play Mode에서만 경험치 지급)")]
    [SerializeField] private float testExpAmount = 20f;

    private readonly List<Flight> _flights = new List<Flight>();
    private readonly Stack<Flight> _recycledFlights = new Stack<Flight>();
    private BossBase _subscribedBoss;
    private Canvas _canvas;
    private Camera _worldCamera;
    private float _accumulatedExp;
    private float _lastEffectTime;
    private bool _started;

    /// <summary>경로를 배치하는 Canvas 영역.</summary>
    public RectTransform RouteCanvas => _routeCanvas;
    /// <summary>편집용 왼쪽 경로.</summary>
    public ExpEffectRoute LeftRoute => _leftRoute;
    /// <summary>편집용 오른쪽 독립 경로.</summary>
    public ExpEffectRoute RightRoute => _rightRoute;
    /// <summary>오른쪽이 왼쪽 경로를 대칭으로 따르는지 여부.</summary>
    public bool MirrorRight => _mirrorRight;
    /// <summary>전체 이동 시간.</summary>
    public float TravelDuration => Mathf.Max(0.1f, _travelDuration);
    /// <summary>씬 뷰의 시작 기준점.</summary>
    public Transform SpawnPoint => spawnPoint;
    /// <summary>경험치가 흡수되는 UI 기준점.</summary>
    public Transform ArrivalTarget => attractorTarget;

    private void Awake()
    {
        ResolveCanvas();
        _worldCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (_started) SubscribeManager();
    }

    private void Start()
    {
        _started = true;
        SubscribeManager();
    }

    private void SubscribeManager()
    {
        if (_bossManager == null) return;
        _bossManager.OnBossEntryed += SubscribeBoss;
        SubscribeBoss(null, null);
    }

    private void OnDisable()
    {
        if (_bossManager != null) _bossManager.OnBossEntryed -= SubscribeBoss;
        UnsubscribeBoss();
        float pending = _accumulatedExp;
        _accumulatedExp = 0f;
        foreach (Flight flight in _flights)
        {
            pending += flight.Reward;
            Release(flight);
        }
        _flights.Clear();
        if (pending > 0f) _expManager?.AddExp(pending);
    }

    private void SubscribeBoss(BossEntry _, BossEntry __)
    {
        UnsubscribeBoss();
        _subscribedBoss = _bossManager.CurrentBoss;
        if (_subscribedBoss != null) _subscribedBoss.OnDamaged += OnBossDamaged;
    }

    private void UnsubscribeBoss()
    {
        if (_subscribedBoss != null) _subscribedBoss.OnDamaged -= OnBossDamaged;
        _subscribedBoss = null;
    }

    private void OnBossDamaged(decimal damage, Vector3? hitPos)
    {
        if (_expManager == null) return;
        float amount = _expManager.CalculateExpFromDamage((float)damage);
        if (amount > 0f) _accumulatedExp += amount;
    }

    private void Update()
    {
        if (_accumulatedExp > 0f && Time.unscaledTime - _lastEffectTime >= _effectCooldown)
        {
            float amount = _accumulatedExp;
            _accumulatedExp = 0f;
            _lastEffectTime = Time.unscaledTime;
            SpawnParticleAndApplyExp(amount);
        }
        AdvanceFlights(Time.deltaTime);
    }

    private void AdvanceFlights(float deltaTime)
    {
        for (int i = _flights.Count - 1; i >= 0; i--)
        {
            Flight flight = _flights[i];
            flight.Elapsed += deltaTime;
            float progress = Mathf.Clamp01(flight.Elapsed / flight.Duration);
            MoveFollower(flight.Target, flight.Samples, flight.Distances, progress);
            if (progress >= 1f && flight.Reward > 0f)
            {
                float reward = flight.Reward;
                flight.Reward = 0f;
                _expManager?.AddExp(reward);
                if (!isActiveAndEnabled) return;
            }
            if (flight.Elapsed < flight.Duration + flight.TailDuration) continue;
            _flights.RemoveAt(i);
            Release(flight);
        }
    }

    /// <summary>실행 중 테스트 경험치를 지급한다. 편집 모드에서는 상태를 바꾸지 않는다.</summary>
    [ContextMenu("테스트: 경험치 이펙트 발동")]
    public void TestFireEffect()
    {
        if (Application.isPlaying && isActiveAndEnabled) SpawnParticleAndApplyExp(testExpAmount);
    }

    /// <summary>실행 중 경험치를 지급하지 않고 경로 연출만 재생한다.</summary>
    public void PreviewEffect()
    {
        if (Application.isPlaying && isActiveAndEnabled) SpawnParticleAndApplyExp(0f);
    }

    /// <summary>Canvas 참조가 없는 기존 씬도 소속 루트 Canvas를 사용한다.</summary>
    public void ResolveCanvas()
    {
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null) _canvas = _canvas.rootCanvas;
        if (_routeCanvas == null && _canvas != null) _routeCanvas = _canvas.transform as RectTransform;
    }

    /// <summary>씬 뷰 및 미리보기용 시작/도착 좌표를 가져온다.</summary>
    public bool TryGetEndpoints(out Vector2 start, out Vector2 end)
    {
        start = end = Vector2.zero;
        if (_routeCanvas == null || spawnPoint == null || attractorTarget == null) return false;
        if (_routeCanvas.rect.width <= 0f || _routeCanvas.rect.height <= 0f) return false;
        start = WorldToNormalized(spawnPoint.position);
        end = WorldToNormalized(attractorTarget.position);
        return true;
    }

    /// <summary>월드 UI 좌표를 화면 비율 좌표로 변환한다.</summary>
    public Vector2 WorldToNormalized(Vector3 position)
    {
        Vector2 local = _routeCanvas.InverseTransformPoint(position);
        Rect rect = _routeCanvas.rect;
        return new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
    }

    /// <summary>화면 비율 좌표를 월드 UI 좌표로 변환한다.</summary>
    public Vector3 NormalizedToWorld(Vector2 position)
    {
        Rect rect = _routeCanvas.rect;
        return _routeCanvas.TransformPoint(new Vector3(rect.xMin + position.x * rect.width,
            rect.yMin + position.y * rect.height, 0f));
    }

    private void SpawnParticleAndApplyExp(float amount)
    {
        if (Time.timeScale <= 0f || particleImage == null || particleImage.gameObject == gameObject
            || !TryGetEndpoints(out Vector2 start, out Vector2 end)
            || !_leftRoute.IsValid || (!_mirrorRight && !_rightRoute.IsValid))
        {
            if (amount > 0f) _expManager?.AddExp(amount);
            return;
        }
        // 실제 보스의 화면 위치에서 출발. 보스 없는 테스트에서는 Spawn Point 사용.
        if (_subscribedBoss != null && _worldCamera != null && _canvas != null)
        {
            Vector3 screen = _worldCamera.WorldToScreenPoint(_subscribedBoss.transform.position);
            Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (screen.z > 0f && RectTransformUtility.ScreenPointToWorldPointInRectangle(_routeCanvas, screen, uiCamera, out Vector3 world))
                start = WorldToNormalized(world);
        }
        Flight flight = _recycledFlights.Count > 0 ? _recycledFlights.Pop() : new Flight();
        flight.Elapsed = 0f;
        flight.Reward = amount;
        flight.Duration = TravelDuration;
        BakeRandomRoute(flight, start, end);
        flight.Particle = CreateParticle(start, flight.Duration, out flight.Target);
        flight.TailDuration = flight.Particle.trailLifetime + ParticleEndPadding;
        _flights.Add(flight);
    }

    private void BakeRandomRoute(Flight flight, Vector2 start, Vector2 end)
    {
        bool right = Random.Range(0, 2) == 1;
        Bake(right && !_mirrorRight ? _rightRoute : _leftRoute, right && _mirrorRight,
            start, end, flight.Samples, flight.Distances);
    }

    private ParticleImage CreateParticle(Vector2 start, float duration, out RectTransform target)
    {
        ParticleImage particle = RM.Instantiate(particleImage, NormalizedToWorld(start), _routeCanvas.rotation, _routeCanvas, true);
        return PrepareParticle(particle, start, duration, out target);
    }

    private ParticleImage PrepareParticle(ParticleImage particle, Vector2 start, float duration, out RectTransform target)
    {
        particle.Stop(true);
        particle.rectTransform.position = NormalizedToWorld(start);
        particle.rectTransform.localScale = Vector3.one;
        particle.onAnyParticleFinished.RemoveAllListeners();
        target = particle.transform.Find(TargetName) as RectTransform;
        if (target == null)
        {
            target = new GameObject(TargetName, typeof(RectTransform)).transform as RectTransform;
            target.SetParent(particle.transform, false);
        }
        target.position = NormalizedToWorld(start);
        particle.attractorTarget = target;
        particle.attractorEnabled = true;
        particle.attractorType = AttractorType.Pivot;
        particle.attractorLerp = new ParticleSystem.MinMaxCurve(1f);
        particle.startSpeed = new ParticleSystem.MinMaxCurve(0f);
        particle.gravityEnabled = particle.velocityEnabled = particle.noiseEnabled = particle.vortexEnabled = false;
        particle.shape = EmitterShape.Point;
        particle.space = Simulation.Local;
        particle.timeScale = TimeScale.Normal;
        particle.loop = false;
        particle.duration = ParticleEndPadding;
        particle.lifetime = new ParticleSystem.MinMaxCurve(duration + ParticleEndPadding);
        particle.rateOverTime = particle.rateOverLifetime = particle.rateOverDistance = 0f;
        // 프리팹의 단발 Burst 하나를 재사용한다.
        particle.SetBurst(0, 0f, 1);
        particle.raycastTarget = false;
        particle.maskable = false;
        if (particle.particleTrailRenderer != null)
        {
            particle.particleTrailRenderer.raycastTarget = false;
            particle.particleTrailRenderer.maskable = false;
        }
        particle.Play();
        return particle;
    }

    private void Bake(ExpEffectRoute route, bool mirror, Vector2 start, Vector2 end, Vector2[] samples, float[] distances)
    {
        Vector2 size = _routeCanvas.rect.size;
        distances[0] = 0f;
        for (int i = 0; i <= SampleCount; i++)
        {
            samples[i] = route.Evaluate((float)i / SampleCount, start, end, mirror);
            if (i > 0) distances[i] = distances[i - 1] + Vector2.Scale(samples[i] - samples[i - 1], size).magnitude;
        }
    }

    private void MoveFollower(RectTransform target, Vector2[] samples, float[] distances, float progress)
    {
        if (target == null || _routeCanvas == null) return;
        float distance = distances[SampleCount] * progress;
        int low = 0, high = SampleCount;
        while (high - low > 1)
        {
            int middle = (low + high) / 2;
            if (distances[middle] < distance) low = middle;
            else high = middle;
        }
        float t = Mathf.InverseLerp(distances[low], distances[high], distance);
        target.position = NormalizedToWorld(Vector2.Lerp(samples[low], samples[high], t));
    }

    private void Release(Flight flight)
    {
        if (flight.Particle != null) { flight.Particle.Stop(true); RM.Destroy(flight.Particle); }
        flight.Particle = null;
        flight.Target = null;
        flight.Reward = 0f;
        _recycledFlights.Push(flight);
    }

    private sealed class Flight
    {
        public ParticleImage Particle;
        public RectTransform Target;
        public float Elapsed, Duration, TailDuration, Reward;
        public readonly Vector2[] Samples = new Vector2[SampleCount + 1];
        public readonly float[] Distances = new float[SampleCount + 1];
    }
}
