using UnityEngine;
using VContainer;
using VContainer.Unity;

// ════════════════════════════════════════════════════════
// UnitFactory — InGameSingleton 교체
// ════════════════════════════════════════════════════════
public class UnitFactory : MonoBehaviour
{
    [Inject] private IObjectResolver _resolver;
    [SerializeField] private UnitData[] unitDataList;

    public UnitData[] UnitDataList => unitDataList;

    public void Init()
    {
        if (GlobalData.SelectedParty != null && GlobalData.SelectedParty.unitDataList != null)
        {
            unitDataList = GlobalData.SelectedParty.unitDataList.ToArray();
        }
        ValidateUnitDataList();
    }

    public UnitBase CreateUnit(int type)
    {
        if (unitDataList == null || unitDataList.Length == 0)
        {
            Debug.LogError("UnitFactory: unitDataList가 비어있습니다.");
            return null;
        }

        var data = System.Array.Find(unitDataList, d => d != null && d.unitType == type);
        if (data == null)
        {
            Debug.LogError($"UnitFactory: unitType={type}인 UnitData를 찾지 못했습니다.");
            return null;
        }

        return InstantiateFromData(data);
    }

    /// <summary>Normal 티어 유닛만 랜덤 생성 (소환 버튼용)</summary>
    public UnitBase CreateRandomNormalUnit() => CreateRandomUnitOfTier(Tier.Normal);

    /// <summary>characterId 기준 유닛 생성 (소환 확률 시트 연동용). 없으면 Normal 랜덤 폴백.</summary>
    public UnitBase CreateUnitByCharacterId(int characterId)
    {
        var data = System.Array.Find(unitDataList, d => d != null && d.characterId == characterId);
        if (data == null)
        {
            Debug.LogWarning($"UnitFactory: characterId={characterId} 미등록 — Normal 랜덤 폴백");
            return CreateRandomNormalUnit();
        }
        return InstantiateFromData(data);
    }

    /// <summary>전체 풀에서 랜덤 생성</summary>
    public UnitBase CreateRandomUnit()
    {
        if (unitDataList == null || unitDataList.Length == 0)
        {
            Debug.LogError("UnitFactory: unitDataList가 비어있습니다.");
            return null;
        }

        var data = unitDataList[Random.Range(0, unitDataList.Length)];
        if (data == null) return null;

        return InstantiateFromData(data);
    }

    /// <summary>지정 티어에서 랜덤 유닛 생성 (머지 결과물 스폰에 사용)</summary>
    public UnitBase CreateRandomUnitOfTier(Tier tier)
    {
        if (unitDataList == null || unitDataList.Length == 0)
        {
            Debug.LogError("UnitFactory: unitDataList가 비어있습니다.");
            return null;
        }

        var pool = System.Array.FindAll(unitDataList, d => d != null && d.unitTier == tier);
        if (pool.Length == 0)
        {
            Debug.LogError($"UnitFactory: unitTier={tier} 인 유닛 데이터가 없습니다.");
            return null;
        }

        // unitType 중복과 무관하게 data 객체를 직접 사용
        return InstantiateFromData(pool[Random.Range(0, pool.Length)]);
    }

    /// <summary>부족 + 티어로 랜덤 유닛 생성 (GainUnit 레벨업 효과용)</summary>
    public UnitBase CreateRandomUnitByTribeAndTier(UnitTribe tribe, Tier tier)
    {
        if (unitDataList == null || unitDataList.Length == 0)
        {
            Debug.LogError("UnitFactory: unitDataList가 비어있습니다.");
            return null;
        }

        var pool = System.Array.FindAll(unitDataList,
            d => d != null && d.unitTribe == tribe && d.unitTier == tier);

        if (pool.Length == 0)
        {
            Debug.LogWarning($"UnitFactory: tribe={tribe} tier={tier} 유닛 없음 — tier 폴백");
            return CreateRandomUnitOfTier(tier);
        }

        return InstantiateFromData(pool[Random.Range(0, pool.Length)]);
    }

    /// <summary>UnitData SO를 직접 넘겨 생성 — unitDataList 등록 없이도 동작 (테스트 소환 등)</summary>
    public UnitBase CreateUnitFromData(UnitData data) => InstantiateFromData(data);

    // ── 공통 인스턴스화 ────────────────────────────────────────

    private UnitBase InstantiateFromData(UnitData data)
    {
        if (data.prefab == null)
        {
            Debug.LogError($"UnitFactory: [{data.unitName}] prefab 미연결");
            return null;
        }

        var go   = _resolver.Instantiate(data.prefab);
        var unit = go.GetComponent<UnitBase>();

        if (unit == null)
        {
            Debug.LogError($"UnitFactory: [{data.unitName}] 프리팹에 UnitBase 없음");
            Destroy(go);
            return null;
        }

        unit.unitData = data;
        unit.animator?.Initialize(unit);

        InitUnitRectTransform(unit);

        return unit;
    }

    [Header("Spawn Settings")]
    [SerializeField] private float defaultSpawnPivotY = 0.5f;
    [SerializeField] private float defaultSpawnOffsetY = 0f;

    /// <summary>유닛의 RectTransform을 그리드 셀 배치에 최적화된 기본값으로 초기화합니다.</summary>
    public void InitUnitRectTransform(UnitBase unit)
    {
        var rt = unit.GetComponent<RectTransform>();
        if (rt == null) return;

        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, defaultSpawnPivotY); // 기본값 0.5 (인스펙터에서 수정 가능)
        rt.anchoredPosition = new Vector2(0f, defaultSpawnOffsetY); // y에 offset 더함
        rt.localScale       = Vector3.one;
    }

    private void ValidateUnitDataList()
    {
        if (unitDataList == null || unitDataList.Length == 0)
        {
            Debug.LogError("UnitFactory: unitDataList 비어있음");
            return;
        }

        for (int i = 0; i < unitDataList.Length; i++)
        {
            var data = unitDataList[i];
            if (data == null) { Debug.LogError($"UnitFactory: unitDataList[{i}] null"); continue; }
            if (data.prefab == null) Debug.LogError($"UnitFactory: [{data.unitName}] prefab 미연결");
            if (string.IsNullOrEmpty(data.unitName)) Debug.LogWarning($"UnitFactory: [{i}] unitName 없음");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"UnitFactory: 유닛 {unitDataList.Length}종 로드 완료");
#endif
    }
}
