using UnityEngine;

/// <summary>
/// 인게임 진입 시 선택된 족장을 그리드 중앙에 자동 배치
///
/// 데이터 흐름:
///   Player.Chief.SelectedChiefId (HSD) → chieftainDataList 룩업 → UnitFactory 생성 → 중앙 셀 배치
///
/// Inspector: chieftainDataList — 등록된 모든 ChieftainData 할당
/// </summary>
public class ChieftainSpawner : MonoBehaviour
{
    [VContainer.Inject] private GridManager _gridManager;
    [VContainer.Inject] private UnitFactory _unitFactory;
    [VContainer.Inject] private UnitSpawner _unitSpawner;

    [SerializeField] private ChieftainData[] chieftainDataList;

    [Header("테스트 소환 (아웃게임 미구현 시)")]
    [SerializeField] private bool     _useTestSpawn  = true;
    [Tooltip("테스트로 소환할 족장 UnitData SO — 비워두면 chieftainDataList 첫 번째 사용")]
    [SerializeField] private UnitData _testUnitData;

    /// <summary>현재 배치된 족장 유닛 — 족장 전용 버프 적용에 사용</summary>
    public UnitBase ChieftainUnit { get; private set; }

    private void Start()
    {
        if (_useTestSpawn && _testUnitData != null)
        {
            SpawnChieftainByUnitData(_testUnitData);
            return;
        }

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
