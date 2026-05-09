using UnityEngine;

/// <summary>
/// 인게임 진입 시 선택된 족장을 그리드 중앙에 자동 배치
/// Inspector: chieftainDataList — 등록된 모든 ChieftainData 할당
/// </summary>
public class ChieftainSpawner : InGameSingleton<ChieftainSpawner>
{
    [SerializeField] private ChieftainData[] chieftainDataList;

    private void Start()
    {
        var cached = BackendGameData.Instance?.CachedData;
        if (cached == null || string.IsNullOrEmpty(cached.SelectedChieftainId)) return;

        var data = System.Array.Find(
            chieftainDataList,
            d => d != null && d.chieftainId == cached.SelectedChieftainId);

        if (data == null)
        {
            Debug.LogWarning($"ChieftainSpawner: '{cached.SelectedChieftainId}' 에 맞는 ChieftainData 없음");
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

        unit.OnPlaced(Manager.Currency, Manager.Boss?.CurrentBoss, cell);
    }
}
