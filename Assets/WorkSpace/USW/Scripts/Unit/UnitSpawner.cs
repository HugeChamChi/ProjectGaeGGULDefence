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
    private PopulationManager _populationManager;
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

    public float CurrentCost { get; private set; }

    // UIManager가 구독해서 비용 텍스트 갱신
    public event System.Action<float> OnCostChanged;

    /// <summary>유닛 판매(삭제) 시 전역 알림 — TotemSellStack에서 구독</summary>
    public static event System.Action OnAnyUnitSold;

    public void Init()
    {
        _upgradeManager = _resolver.Resolve<UpgradeManager>();
        _gameDataManager = _resolver.Resolve<GameDataManager>();
        _gameManager = _resolver.Resolve<GameManager>();
        _populationManager = _resolver.Resolve<PopulationManager>();
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
            var row = _gameDataManager.GetCharacterRow(data.characterId);
            if (row != null)
            {
                data.ApplySheetData(row);
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

        if (_populationManager != null && !_populationManager.CanAdd(1))
        {
            Debug.Log("인구수가 부족합니다.");
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

        // 비동기 스폰 지연 전에 인구수 즉시 선점 (광클 초과 방지)
        if (_populationManager != null)
        {
            _populationManager.Add(unit.unitData?.populationCost ?? 1);
            unit.IsPopulationReserved = true;
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
        if (unit.unitData?.unitTier == Tier.Chieftain) return;

        var cell = FindCellByUnit(unit);
        if (cell == null) return;

        // 강화 레벨 조회 (UpgradeManager 기준)
        int charId  = unit.unitData != null ? unit.unitData.characterId : -1;
        string jobType = unit.unitData != null
            ? _upgradeManager?.GetJobType(charId) ?? string.Empty
            : string.Empty;
        int upgradeLevel = _upgradeManager != null && !string.IsNullOrEmpty(jobType)
            ? _upgradeManager.GetJobLevel(jobType)
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

        // 판매 시 족장 공격력 증가 (원맨쇼 레벨업 효과)
        if (lu != null && lu.HasChieftainGainOnSell)
        {
            lu.AddChieftainAttackBonus(lu.ChieftainSellAtkGain);
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
