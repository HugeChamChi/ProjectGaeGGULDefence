using UnityEngine;
using VContainer;
/// <summary>
/// 인구수 추적 — 현재 배치 인구 / 최대 인구 관리
///
/// 유닛: UnitBase.OnPlaced/OnRemoved 에서 자동 호출
/// 토템: TotemBase.OnPlaced/OnRemoved 에서 동일하게 Add/Remove 호출하면 확장 가능
/// </summary>
public class PopulationManager : MonoBehaviour
{
    [Inject] private IObjectResolver _resolver;

    private GridManager _gridManager;
    private TotemSpawner _totemSpawner;
    private UnitSpawner _unitSpawner;
    private UnitFactory _unitFactory;
    
    private void Start()
    {
        _gridManager = _resolver.Resolve<GridManager>();
        _totemSpawner = _resolver.Resolve<TotemSpawner>();
        _unitSpawner = _resolver.Resolve<UnitSpawner>();
        _unitFactory = _resolver.Resolve<UnitFactory>();
    }
    
    public void Init()
    {
    }

    public struct SpawnQueueItem {
        public bool isTotem;
        public TotemData totemData;
        public UnitTribe? unitTribe;
        public Tier minTier;
        public Tier maxTier;
    }

    private System.Collections.Generic.Queue<SpawnQueueItem> _spawnQueue = new System.Collections.Generic.Queue<SpawnQueueItem>();

    public void EnqueueTotem(TotemData data) {
        _spawnQueue.Enqueue(new SpawnQueueItem { isTotem = true, totemData = data });
    }

    public void EnqueueUnit(UnitTribe? tribe, Tier minTier, Tier maxTier) {
        _spawnQueue.Enqueue(new SpawnQueueItem { isTotem = false, unitTribe = tribe, minTier = minTier, maxTier = maxTier });
    }

    private void Update() {
        if (_spawnQueue.Count == 0) return;
        
        var emptyCells = _gridManager?.GetEmptyCells();
        if (emptyCells == null || emptyCells.Count == 0) return;

        var peek = _spawnQueue.Peek();
        if (peek.isTotem) {
            if (!CanAdd(1)) return;
            var item = _spawnQueue.Dequeue();
            _totemSpawner.SpawnTotemByData(item.totemData);
        } else {
            var item = _spawnQueue.Dequeue();
            var cell = emptyCells[Random.Range(0, emptyCells.Count)];
            Tier tier = (Tier)Random.Range((int)item.minTier, (int)item.maxTier + 1);
            UnitBase unit = item.unitTribe.HasValue
                ? _unitFactory.CreateRandomUnitByTribeAndTier(item.unitTribe.Value, tier)
                : _unitFactory.CreateRandomUnitOfTier(tier);

            if (unit != null) {
                _unitSpawner.PlaceUnitWithEffect(unit, cell);
            }
        }
    }

    [SerializeField] private GameConfig _config;

    public int Current    { get; private set; }
    public int MaxBonus   { get; private set; }
    public int Max        => (_config != null ? _config.maxPopulation : 21) + MaxBonus;

    /// <summary>레벨업 선택지(풍요로운 영토 등)가 최대 인구수를 증가시킬 때 호출</summary>
    public void AddMaxBonus(int amount)
    {
        MaxBonus += amount;
        OnPopulationChanged?.Invoke(Current, Max);
    }

    public event System.Action<int, int> OnPopulationChanged;

    public bool CanAdd(int cost = 1) => Current + cost <= Max;

    public void Add(int cost)
    {
        Current += cost;
        OnPopulationChanged?.Invoke(Current, Max);
    }

    public void Remove(int cost)
    {
        Current = Mathf.Max(0, Current - cost);
        OnPopulationChanged?.Invoke(Current, Max);
    }
}
