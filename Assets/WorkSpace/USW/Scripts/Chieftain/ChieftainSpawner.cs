using UnityEngine;

/// <summary>
/// 인게임 진입 시 선택된 족장을 그리드 중앙에 자동 배치
///
/// 데이터 흐름:
///   Player.Chief.SelectedChiefId (HSD) → chieftainDataList 룩업 → UnitFactory 생성 → 중앙 셀 배치
///
/// Inspector: chieftainDataList — 등록된 모든 ChieftainData 할당
/// </summary>
public class ChieftainSpawner : InGameSingleton<ChieftainSpawner>
{
    [SerializeField] private ChieftainData[] chieftainDataList;

    private void Start()
    {
        int selectedId = Player.Chief.SelectedChiefId;

        if (selectedId == 0)
        {
            Debug.Log("ChieftainSpawner: 선택된 족장 없음 (SelectedChiefId = 0)");
            return;
        }

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
        var cell = Manager.Grid.GetCenterCell();
        if (cell == null || !cell.IsAvailable)
        {
            Debug.LogWarning("ChieftainSpawner: 중앙 셀 배치 불가");
            return;
        }

        var unit = Manager.UnitFactory.CreateUnit(data.unitType);
        if (unit == null) return;

        cell.TryPlaceUnit(unit);
        unit.transform.SetParent(cell.transform, false);

        var rt = unit.GetComponent<UnityEngine.RectTransform>();
        if (rt != null)
        {
            rt.anchorMin        = new UnityEngine.Vector2(0.5f, 0.5f);
            rt.anchorMax        = new UnityEngine.Vector2(0.5f, 0.5f);
            rt.pivot            = new UnityEngine.Vector2(0.5f, 0.5f);
            rt.anchoredPosition = UnityEngine.Vector2.zero;
        }

        var drag = unit.GetComponent<DragHandler>();
        if (drag != null) drag.SetOriginCell(cell);

        unit.OnPlaced(Manager.Currency, Manager.Boss?.CurrentBoss, cell);
    }
}
