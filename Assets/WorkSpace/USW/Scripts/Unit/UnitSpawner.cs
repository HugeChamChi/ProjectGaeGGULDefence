using UnityEngine;
using VContainer;
using Cysharp.Threading.Tasks;

// ════════════════════════════════════════════════════════
// UnitSpawner — InGameSingleton 교체 + Manager 접근 통일
// ════════════════════════════════════════════════════════
public class UnitSpawner : MonoBehaviour
{
    [Inject] private IObjectResolver _resolver;

    private UpgradeManager _upgradeManager;
    private GameDataManager _gameDataManager;
    private GameManager _gameManager;
    private LevelUpManager _levelUpManager;
    private CurrencyManager _currencyManager;
    private GridManager _gridManager;
    private UnitFactory _unitFactory;
    private BossManager _bossManager;
    private TotemBuffManager _totemBuffManager;

    [Header("Spawn Effects")]
    [SerializeField] private SpawnLine spawnLinePrefab;
    [SerializeField] private GameObject spawnEffectPrefab;
    [SerializeField] private Transform defaultSpawnOrigin;
    [SerializeField] private Transform effectParent;

    [Header("Unit Settings")]
    [SerializeField] private Vector3 spawnScale = new Vector3(0.5f, 0.5f, 0.5f);

    public float CurrentCost { get; private set; }

    /// <summary>이번 씬에서 당첨되었지만 아직 배치하지 못한 노말 유닛 보상 수.</summary>
    public int PendingSupportCount { get; private set; }
    private bool _processingSupport;

    /// <summary>성공한 합성마다 한 번 추첨한다. 당첨 보상은 만석이어도 소실되지 않는다.</summary>
    public void RequestMergeSupport()
    {
        float chance = _levelUpManager?.DroneSelections.Get(DroneSelectionKind.MergeSupport)?.Value ?? 0f;
        if (chance > 0f && Random.value < Mathf.Clamp01(chance)) PendingSupportCount++;
    }

    // 합성/판매/드래그의 셀 변경이 끝난 프레임에 처리하여 합성 결과 자리를 선점하지 않는다.
    private void LateUpdate() => ProcessPendingSupport();

    /// <summary>전투 중 빈칸에 대기 보상을 배치한다. 효과 연출 전에 셀을 점유한다.</summary>
    public void ProcessPendingSupport()
    {
        if (_processingSupport || PendingSupportCount <= 0 || _gridManager == null
            || (_gameManager != null && _gameManager.CurrentState != GameManager.GameState.Playing)) return;
        _processingSupport = true;
        try
        {
            while (PendingSupportCount > 0)
            {
                var cells = _gridManager.GetEmptyCells();
                if (cells.Count == 0) break;
                var cell = cells[Random.Range(0, cells.Count)];
                if (!TryPlaceSupportUnit(cell)) break;
                PendingSupportCount--;
            }
        }
        finally { _processingSupport = false; }
    }

    /// <summary>현재 파티의 노말 랜덤 유닛을 기존 생성/배치 경로로 지급한다.</summary>
    protected virtual bool TryPlaceSupportUnit(GridCell cell)
    {
        if (_unitFactory == null || !cell.IsAvailable) return false;
        var unit = _unitFactory.CreateRandomNormalUnit();
        if (unit == null) return false;
        PlaceUnitWithEffect(unit, cell);
        return true;
    }

    // UIManager가 구독해서 비용 텍스트 갱신
    public event System.Action<float> OnCostChanged;

    /// <summary>유닛 판매(삭제) 시 전역 알림 — TotemSellStack에서 구독</summary>
    public static event System.Action OnAnyUnitSold;

    public void Init()
    {
        _upgradeManager = _resolver.Resolve<UpgradeManager>();
        _gameDataManager = _resolver.Resolve<GameDataManager>();
        _gameManager = _resolver.Resolve<GameManager>();
        _levelUpManager = _resolver.Resolve<LevelUpManager>();
        _currencyManager = _resolver.Resolve<CurrencyManager>();
        _gridManager = _resolver.Resolve<GridManager>();
        _unitFactory = _resolver.Resolve<UnitFactory>();
        _bossManager = _resolver.Resolve<BossManager>();
        _totemBuffManager = _resolver.Resolve<TotemBuffManager>();

        // GameDataManager 로드 전에는 시트 기본값(20)으로 시작, 로드 후 동기화
        CurrentCost = 20f;
        if (_gameDataManager != null)
        {
            if (_gameDataManager.IsLoaded)
            {
                SyncInitialCost();
                SyncAllUnitData();
            }
            else
            {
                _gameDataManager.OnLoaded += SyncInitialCost;
                _gameDataManager.OnLoaded += SyncAllUnitData;
            }
        }
    }

    private void SyncInitialCost()
    {
        CurrentCost = _gameDataManager.SummonInitialCost;
        OnCostChanged?.Invoke(CurrentCost);
    }

    private void SyncAllUnitData()
    {
        if (_unitFactory == null || _unitFactory.UnitDataList == null || _gameDataManager == null) return;

        foreach (var data in _unitFactory.UnitDataList)
        {
            if (data == null) continue;
            foreach (var tier in TierUtil.All)
            {
                var row = _gameDataManager.GetCharacterRow(data.characterId.Get(tier));
                if (row != null)
                {
                    data.ApplySheetData(tier, row);
                }
            }
        }
        Debug.Log($"[UnitSpawner] 모든 UnitData SO에 시트 스탯 동기화 완료 ({_unitFactory.UnitDataList.Length}종)");
    }

    public void OnSpawnButtonPressed()
    {
        if (_gameManager.CurrentState != GameManager.GameState.Playing)
        {
            Debug.Log("게임 시작 후 배치 가능합니다.");
            return;
        }

        // 소환 할인 (할인 티켓 레벨업 효과 등)
        float discountRate  = _levelUpManager?.SummonDiscountRate ?? 0f;
        float fixedDiscount = _levelUpManager?.SummonFixedDiscountAmount ?? 0f;
        float effectiveCost = (CurrentCost - fixedDiscount) * Mathf.Max(0f, 1f - discountRate);
        effectiveCost = Mathf.Max(0f, effectiveCost);

        if (!_currencyManager.Spend(effectiveCost))
        {
            Debug.Log("식량이 부족합니다.");
            return;
        }

        var empty = _gridManager.GetEmptyCells();
        if (empty.Count == 0)
        {
            Debug.Log("빈 셀이 없습니다.");
            _currencyManager.AddCurrency(effectiveCost);
            return;
        }

        // 소환 확률 시트 기반 랜덤 — 미로드 시 Normal 랜덤 폴백
        UnitBase unit;
        if (_gameDataManager != null && _gameDataManager.IsLoaded)
        {
            int charId = _gameDataManager.GetRandomSpawnCharacterId();
            unit = charId >= 0
                ? _unitFactory.CreateUnitByCharacterId(charId)
                : _unitFactory.CreateRandomNormalUnit();
        }
        else
        {
            unit = _unitFactory.CreateRandomNormalUnit();
        }
        if (unit == null)
        {
            Debug.LogError("UnitSpawner: 유닛 생성 실패");
            _currencyManager.AddCurrency(effectiveCost);
            return;
        }

        // 환급 처리(소환 실패 시)는 effectiveCost 기준
        var cell = empty[Random.Range(0, empty.Count)];
        
        PlaceUnitWithEffect(unit, cell);

        // 소환 성공 시 비용 증가 (시트 값 우선, 폴백 20)
        float increment = _gameDataManager != null && _gameDataManager.IsLoaded
            ? _gameDataManager.SummonCostIncrease
            : 20f;
        CurrentCost += increment;
        OnCostChanged?.Invoke(CurrentCost);
    }

    /// <summary>
    /// 지정된 셀에 유닛을 배치하며 스폰 이펙트(SpawnLine, ParticleImage)를 재생합니다.
    /// 외부 요인(레벨업 보상 등)에 의한 스폰 시에도 동일하게 사용합니다.
    /// </summary>
    public void PlaceUnitWithEffect(UnitBase unit, GridCell cell, Vector3? originWorldPos = null)
    {
        // 1. 점유 상태 설정 (다른 스폰과 겹치지 않도록 미리 점유)
        cell.TryPlaceUnit(unit);
        unit.transform.SetParent(cell.transform, false);
        
        _unitFactory.InitUnitTransform(unit);
        unit.transform.localScale = spawnScale;
        
        var drag = unit.GetComponent<DragHandler>();
        if (drag != null) drag.SetOriginCell(cell);

        // 2. 비동기 이펙트 재생 (끝나면 OnPlaced 호출 및 활성화)
        SpawnProcessAsync(unit, cell, originWorldPos).Forget();
    }

    private async UniTaskVoid SpawnProcessAsync(UnitBase unit, GridCell cell, Vector3? originWorldPos)
    {
        if (unit == null || cell == null) return;
        
        // 이펙트 재생 동안 유닛을 숨김
        unit.gameObject.SetActive(false);
        
        Transform lineParent = effectParent != null ? effectParent : transform;

        // 1. Line Effect (World Space)
        if (spawnLinePrefab != null)
        {
            var lineObj = RM.Instantiate(spawnLinePrefab.gameObject, lineParent, true);
            var line = lineObj.GetComponent<SpawnLine>();
            if (line != null)
            {
                var tr = line.transform;
                tr.position = Vector3.zero;
                tr.localScale = Vector3.one;
                tr.localRotation = Quaternion.identity;

                Vector3 startPos;
                if (originWorldPos.HasValue)
                {
                    startPos = originWorldPos.Value;
                }
                else if (defaultSpawnOrigin != null)
                {
                    // 만약 할당된 위치가 UI(RectTransform)라면 화면 좌표를 2D 월드 좌표로 변환합니다.
                    var rect = defaultSpawnOrigin.GetComponent<RectTransform>();
                    if (rect != null && Camera.main != null)
                    {
                        Vector3 screenPos = rect.position;
                        // 2D 카메라와의 거리(z)를 맞춰주어 정확한 월드 좌표를 얻습니다.
                        screenPos.z = Mathf.Abs(Camera.main.transform.position.z);
                        startPos = Camera.main.ScreenToWorldPoint(screenPos);
                        startPos.z = 0f; // 2D 평면에 맞게 Z축 보정
                    }
                    else
                    {
                        startPos = defaultSpawnOrigin.position;
                    }
                }
                else
                {
                    startPos = lineParent.position;
                }

                Vector3 endPos = cell.transform.position;

                line.Fire(startPos, endPos);
                await UniTask.Delay(System.TimeSpan.FromSeconds(line.duration), ignoreTimeScale: false);
            }
        }

        // 2. Particle Effect (World Space)
        if (spawnEffectPrefab != null)
        {
            Transform pParent = effectParent != null ? effectParent : cell.transform;
            var particleObj = RM.Instantiate(spawnEffectPrefab, cell.transform.position, spawnEffectPrefab.transform.rotation, pParent, true);
            var particle = particleObj.GetComponent<ParticleSystem>();
            if (particle != null) particle.Play();
            RM.Destroy(particleObj, 2f);
        }

        if (unit != null && unit.gameObject != null)
        {
            unit.gameObject.SetActive(true);
            var dragHandler = unit.GetComponent<DragHandler>();
            if (dragHandler != null) dragHandler.UpdateDepthSorting();
            
            unit.OnPlaced(_currencyManager, _bossManager?.CurrentBoss, cell);

            var lu = _levelUpManager;
            if (lu != null && lu.HasSummonDealsDamage && unit.unitData != null)
            {
                int dmg = DamageCalculator.ApplyRounding(unit.GetAttackDamage() * lu.SummonDamagePct);
                _bossManager?.CurrentBoss?.TakeDamage(dmg);
            }
        }
    }


    /// <summary>특정 유닛을 판매합니다. SellButtonUI에서 호출</summary>
    public void SellUnit(UnitBase unit)
    {
        if (unit == null) return;
        if (unit.OriginalTier == Tier.Chieftain) return;

        var cell = FindCellByUnit(unit);
        if (cell == null) return;

        unit.RestoreOriginalTier();

        // 강화 레벨 조회 (UpgradeManager 기준)
        int charId  = unit.OriginalData != null ? unit.OriginalData.characterId.Get(unit.OriginalTier) : -1;
        string jobType = unit.OriginalData != null
            ? _upgradeManager?.GetJobType(charId) ?? string.Empty
            : string.Empty;
        int upgradeLevel = _upgradeManager != null && !string.IsNullOrEmpty(jobType)
            ? _upgradeManager.GetJobLevel(jobType) + 1 // 판매 테이블은 기존 1 기반 레벨 계약 유지
            : 1;

        float refund = _gameDataManager != null && _gameDataManager.IsLoaded && charId >= 0
            ? _gameDataManager.GetSellPrice(charId, upgradeLevel)
            : 5f;

        // 판매 시 보스 피해 (자폭병 레벨업 효과) - 유닛 제거 전에 데미지를 미리 계산해야 토템/위치 효과가 적용됨
        var lu = _levelUpManager;
        int sellDmg = 0;
        if (lu != null && lu.HasSellDealsDamage && unit.unitData != null)
        {
            sellDmg = DamageCalculator.ApplyRounding(unit.GetAttackDamage() * lu.SellDamagePct);
        }

        cell.RemoveUnit();
        unit.OnRemoved();
        Destroy(unit.gameObject);

        if (refund > 0f)
            _currencyManager.AddCurrency(refund);

        // 판매 보너스 식량 (서비스 레벨업 효과)
        float bonusFood = _levelUpManager?.SellBonusFoodAmount ?? 0f;
        if (bonusFood > 0f)
            _currencyManager.AddCurrency(bonusFood);

        if (sellDmg > 0)
        {
            _bossManager?.CurrentBoss?.TakeDamage(sellDmg);
        }

        // 판매 시 확률로 노말 기물 획득 (비상 탈출 레벨업 효과)
        if (lu != null && lu.HasSellGivesRandomUnit)
        {
            if (Random.value < lu.SellGivesUnitChance)
            {
                // 다음 프레임이나 약간 지연 후 스폰하여 그리드 충돌 회피
                SpawnNormalUnitAfterSellAsync().Forget();
            }
        }

        OnAnyUnitSold?.Invoke();
    }

    private GridCell FindCellByUnit(UnitBase unit)
    {
        foreach (var cell in _gridManager.AllCells())
        {
            if (cell.OccupyingUnit == unit) return cell;
        }
        return null;
    }

    private async Cysharp.Threading.Tasks.UniTaskVoid SpawnNormalUnitAfterSellAsync()
    {
        await Cysharp.Threading.Tasks.UniTask.Yield();
        var empty = _gridManager.GetEmptyCells();
        if (empty.Count == 0) return;

        var cell = empty[Random.Range(0, empty.Count)];
        UnitBase newUnit = _unitFactory.CreateRandomNormalUnit();
        if (newUnit != null)
        {
            PlaceUnitWithEffect(newUnit, cell);
        }
    }
}
