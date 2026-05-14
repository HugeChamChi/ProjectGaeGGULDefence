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

        if (!Manager.Currency.Spend(CurrentCost))
        {
            Debug.Log("식량이 부족합니다.");
            return;
        }

        var empty = Manager.Grid.GetEmptyCells();
        if (empty.Count == 0)
        {
            Debug.Log("빈 셀이 없습니다.");
            Manager.Currency.AddCurrency(CurrentCost);
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
            Manager.Currency.AddCurrency(CurrentCost);
            return;
        }

        var cell = empty[Random.Range(0, empty.Count)];
        cell.TryPlaceUnit(unit);
        unit.transform.SetParent(cell.transform, false);

        var rt = unit.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

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
