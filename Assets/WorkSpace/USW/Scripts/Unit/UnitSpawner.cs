using UnityEngine;

// ════════════════════════════════════════════════════════
// UnitSpawner — InGameSingleton 교체 + Manager 접근 통일
// ════════════════════════════════════════════════════════
public class UnitSpawner : InGameSingleton<UnitSpawner>
{
    public float CurrentCost { get; private set; }

    // UIManager가 구독해서 비용 텍스트 갱신
    public event System.Action<float> OnCostChanged;

    /// <summary>유닛 판매(삭제) 시 전역 알림 — TotemSellStack에서 구독</summary>
    public static event System.Action OnAnyUnitSold;

    protected override void Awake()
    {
        base.Awake();
        // GameDataManager 로드 전에는 시트 기본값(20)으로 시작, 로드 후 동기화
        CurrentCost = 20f;
        if (Manager.GameData != null)
            Manager.GameData.OnLoaded += SyncInitialCost;
    }

    private void SyncInitialCost()
    {
        CurrentCost = Manager.GameData.SummonInitialCost;
        OnCostChanged?.Invoke(CurrentCost);
    }

    public void OnSpawnButtonPressed()
    {
        if (Manager.Game.CurrentState != GameManager.GameState.Playing)
        {
            Debug.Log("게임 시작 후 배치 가능합니다.");
            return;
        }

        if (Manager.Population != null && !Manager.Population.CanAdd(1))
        {
            Debug.Log("인구수가 부족합니다.");
            return;
        }

        // 소환 할인 (할인 티켓 레벨업 효과)
        float discountRate  = Manager.LevelUp?.SummonDiscountRate ?? 0f;
        float effectiveCost = CurrentCost * Mathf.Max(0f, 1f - discountRate);

        if (!Manager.Currency.Spend(effectiveCost))
        {
            Debug.Log("식량이 부족합니다.");
            return;
        }

        var empty = Manager.Grid.GetEmptyCells();
        if (empty.Count == 0)
        {
            Debug.Log("빈 셀이 없습니다.");
            Manager.Currency.AddCurrency(effectiveCost);
            return;
        }

        // 소환 확률 시트 기반 랜덤 — 미로드 시 Normal 랜덤 폴백
        UnitBase unit;
        if (Manager.GameData != null && Manager.GameData.IsLoaded)
        {
            int charId = Manager.GameData.GetRandomSpawnCharacterId();
            unit = charId >= 0
                ? Manager.UnitFactory.CreateUnitByCharacterId(charId)
                : Manager.UnitFactory.CreateRandomNormalUnit();
        }
        else
        {
            unit = Manager.UnitFactory.CreateRandomNormalUnit();
        }
        if (unit == null)
        {
            Debug.LogError("UnitSpawner: 유닛 생성 실패");
            Manager.Currency.AddCurrency(effectiveCost);
            return;
        }

        // 환급 처리(소환 실패 시)는 effectiveCost 기준
        var cell = empty[Random.Range(0, empty.Count)];
        cell.TryPlaceUnit(unit);
        unit.transform.SetParent(cell.transform, false);

        Manager.UnitFactory.InitUnitRectTransform(unit);

        var drag = unit.GetComponent<DragHandler>();
        if (drag != null) drag.SetOriginCell(cell);

        unit.OnPlaced(Manager.Currency, Manager.Boss.CurrentBoss, cell);

        // 소환 성공 시 비용 증가 (시트 값 우선, 폴백 20)
        float increment = Manager.GameData != null && Manager.GameData.IsLoaded
            ? Manager.GameData.SummonCostIncrease
            : 20f;
        CurrentCost += increment;
        OnCostChanged?.Invoke(CurrentCost);
    }

    /// <summary>특정 유닛을 판매합니다. SellButtonUI에서 호출</summary>
    public void SellUnit(UnitBase unit)
    {
        if (unit == null) return;

        var cell = FindCellByUnit(unit);
        if (cell == null) return;

        // 강화 레벨 조회 (UpgradeManager 기준)
        int charId  = unit.unitData != null ? unit.unitData.characterId : -1;
        string jobType = unit.unitData != null
            ? Manager.Upgrade?.GetJobType(charId) ?? string.Empty
            : string.Empty;
        int upgradeLevel = Manager.Upgrade != null && !string.IsNullOrEmpty(jobType)
            ? Manager.Upgrade.GetJobLevel(jobType)
            : 1;

        float refund = Manager.GameData != null && Manager.GameData.IsLoaded && charId >= 0
            ? Manager.GameData.GetSellPrice(charId, upgradeLevel)
            : 5f;

        cell.RemoveUnit();
        unit.OnRemoved();
        Destroy(unit.gameObject);

        if (refund > 0f)
            Manager.Currency.AddCurrency(refund);

        // 판매 보너스 식량 (서비스 레벨업 효과)
        float bonusFood = Manager.LevelUp?.SellBonusFoodAmount ?? 0f;
        if (bonusFood > 0f)
            Manager.Currency.AddCurrency(bonusFood);

        // 판매 시 보스 피해 (자폭병 레벨업 효과)
        var lu = Manager.LevelUp;
        if (lu != null && lu.HasSellDealsDamage && unit.unitData != null)
        {
            int dmg = Mathf.RoundToInt(unit.GetAttackDamage() * lu.SellDamagePct);
            Manager.Boss?.CurrentBoss?.TakeDamage(dmg);
        }

        // 판매 시 족장 공격력 증가 (원맨쇼 레벨업 효과)
        if (lu != null && lu.HasChieftainGainOnSell)
        {
            Manager.Buff.AddLevelUpAttackBuff(lu.ChieftainSellAtkGain);
            int   excessPop     = (Manager.Population?.Current ?? 0) - 2;
            float popPenalty    = excessPop > 0 ? excessPop * lu.ChieftainSellPopPenalty : 0f;
            if (popPenalty > 0f)
                Manager.Buff.AddLevelUpAttackBuff(-popPenalty);
        }

        OnAnyUnitSold?.Invoke();
    }

    private GridCell FindCellByUnit(UnitBase unit)
    {
        foreach (var cell in Manager.Grid.AllCells())
        {
            if (cell.OccupyingUnit == unit) return cell;
        }
        return null;
    }
}
