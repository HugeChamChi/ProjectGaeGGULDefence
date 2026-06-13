using UnityEngine;

/// <summary>
/// 인게임 진입 시 선택된 족장을 그리드 중앙에 자동 배치
///
/// 데이터 흐름:
///   GlobalData.SelectedParty.chieftainData (1순위) → 테스트 유닛(2순위) → 기존 레거시 ID(3순위)
/// </summary>
public class ChieftainSpawner : MonoBehaviour
{
    private GridManager _gridManager;
    private UnitFactory _unitFactory;
    private UnitSpawner _unitSpawner;

    [VContainer.Inject]
    public void Construct(GridManager gridManager, UnitFactory unitFactory, UnitSpawner unitSpawner)
    {
        _gridManager = gridManager;
        _unitFactory = unitFactory;
        _unitSpawner = unitSpawner;
    }

    [SerializeField] private ChieftainData[] chieftainDataList;

    [Header("테스트 소환 (아웃게임 미구현 시)")]
    [SerializeField] private bool     _useTestSpawn  = true;
    [Tooltip("테스트로 소환할 족장 UnitData SO — 비워두면 chieftainDataList 첫 번째 사용")]
    [SerializeField] private UnitData _testUnitData;

    /// <summary>현재 배치된 족장 유닛 — 족장 전용 버프 적용에 사용</summary>
    public UnitBase ChieftainUnit { get; private set; }

    public void Init()
    {
        // 1순위: 로비에서 선택한 파티 데이터에 족장 데이터가 있으면 스폰
        if (GlobalData.SelectedParty != null && GlobalData.SelectedParty.chieftainData != null)
        {
            SpawnChieftainByUnitData(GlobalData.SelectedParty.chieftainData);
            return;
        }

        // 2순위: 에디터 테스트용 할당 스폰
        if (_useTestSpawn && _testUnitData != null)
        {
            SpawnChieftainByUnitData(_testUnitData);
            return;
        }

        // 3순위: (레거시) Player.Chief.SelectedChiefId 기반 소환
        int selectedId = Player.Chief.SelectedChiefId;

        if (selectedId == 0 && _useTestSpawn)
            selectedId = GetTestChieftainId();

        if (selectedId == 0)
        {
            Debug.Log("ChieftainSpawner: 선택된 족장 없음 (SelectedChiefId = 0)");
            return;
        }

        SpawnChieftainById(selectedId);
    }

    private int GetTestChieftainId()
    {
        if (chieftainDataList != null && chieftainDataList.Length > 0 && chieftainDataList[0] != null)
            return chieftainDataList[0].chieftainId;
        return 0;
    }

    public void ChangeChieftain(int selectedId)
    {
        if (ChieftainUnit != null)
        {
            var cell = ChieftainUnit.currentCell;
            if (cell != null) cell.RemoveUnit();
            Destroy(ChieftainUnit.gameObject);
            ChieftainUnit = null;
        }
        SpawnChieftainById(selectedId);
    }

    private void SpawnChieftainById(int selectedId)
    {
        var data = System.Array.Find(
            chieftainDataList,
            d => d != null && d.chieftainId == selectedId);

        if (data == null)
        {
            Debug.LogWarning($"ChieftainSpawner: chieftainId={selectedId} 에 맞는 ChieftainData 없음");
            return;
        }

        PlaceChieftain(data);
    }

    private void PlaceChieftain(ChieftainData data)
    {
        SpawnToCenter(data.unitType);
    }

    private void SpawnChieftainByUnitData(UnitData unitData)
    {
        var cell = _gridManager.GetCenterCell();
        if (cell == null || !cell.IsAvailable)
        {
            Debug.LogWarning("ChieftainSpawner: 중앙 셀 배치 불가");
            return;
        }

        var unit = _unitFactory.CreateUnitFromData(unitData);
        if (unit == null) return;

        ChieftainUnit = unit;
        _unitSpawner.PlaceUnitWithEffect(unit, cell);
    }

    private void SpawnToCenter(int unitType)
    {
        var cell = _gridManager.GetCenterCell();
        if (cell == null || !cell.IsAvailable)
        {
            Debug.LogWarning("ChieftainSpawner: 중앙 셀 배치 불가");
            return;
        }

        var unit = _unitFactory.CreateUnit(unitType);
        if (unit == null) return;

        ChieftainUnit = unit;
        _unitSpawner.PlaceUnitWithEffect(unit, cell);
    }
}
