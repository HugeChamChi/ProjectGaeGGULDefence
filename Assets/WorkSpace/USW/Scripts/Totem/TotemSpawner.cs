using UnityEngine;

// ════════════════════════════════════════════════════════
// TotemSpawner — InGameSingleton 교체 + Manager 접근 통일
// ════════════════════════════════════════════════════════
public class TotemSpawner : InGameSingleton<TotemSpawner>
{
    [Header("토템 프리팹 배열 (인덱스 순서대로 등록)")]
    [SerializeField] private GameObject[] totemPrefabs;

    protected override void Awake()
    {
        base.Awake();
        ValidatePrefabs();
    }

    public int TotemPrefabCount => totemPrefabs != null ? totemPrefabs.Length : 0;

    public TotemData GetTotemData(int index)
    {
        if (totemPrefabs == null || index < 0 || index >= totemPrefabs.Length) return null;
        var go = totemPrefabs[index];
        if (go == null) return null;
        var totem = go.GetComponent<TotemBase>();
        return totem != null ? totem.Data : null;
    }

    public bool SpawnTotemByIndex(int index)
    {
        if (totemPrefabs == null || index < 0 || index >= totemPrefabs.Length)
        {
            Debug.LogError($"TotemSpawner: 인덱스 {index}에 해당하는 프리팹 없음");
            return false;
        }
        return SpawnTotem(totemPrefabs[index]);
    }

    /// <summary>랜덤 토템 1개 자동 소환 (보스 처치 보상용)</summary>
    public bool SpawnRandomTotem()
    {
        if (totemPrefabs == null || totemPrefabs.Length == 0)
        {
            Debug.LogError("TotemSpawner: 토템 프리팹 없음");
            return false;
        }

        int index = Random.Range(0, totemPrefabs.Length);
        return SpawnTotem(totemPrefabs[index]);
    }

    private bool SpawnTotem(GameObject prefab)
    {
        if (prefab == null) { Debug.LogError("TotemSpawner: prefab null"); return false; }

        var empty = Manager.Grid.GetEmptyCells();
        if (empty.Count == 0) return false;

        var go    = Instantiate(prefab);
        var totem = go.GetComponent<TotemBase>();

        if (totem == null)
        {
            Debug.LogError($"TotemSpawner: {prefab.name}에 TotemBase 없음");
            Destroy(go);
            return false;
        }

        var cell = empty[Random.Range(0, empty.Count)];
        cell.TryPlaceTotem(totem);
        go.transform.SetParent(cell.transform, false);

        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        var drag = go.GetComponent<DragHandler>();
        if (drag != null) drag.SetOriginCell(cell);

        totem.OnPlaced(cell);
        return true;
    }

    private void ValidatePrefabs()
    {
        if (totemPrefabs == null || totemPrefabs.Length == 0)
        {
            Debug.LogError("TotemSpawner: totemPrefabs 배열 비어있음");
            return;
        }

        for (int i = 0; i < totemPrefabs.Length; i++)
        {
            if (totemPrefabs[i] == null)
            { Debug.LogError($"TotemSpawner: totemPrefabs[{i}] null"); continue; }

            if (totemPrefabs[i].GetComponent<TotemBase>() == null)
                Debug.LogError($"TotemSpawner: totemPrefabs[{i}]에 TotemBase 없음");

            if (totemPrefabs[i].GetComponent<DragHandler>() == null)
                Debug.LogWarning($"TotemSpawner: totemPrefabs[{i}]에 DragHandler 없음");
        }

        Debug.Log($"TotemSpawner: 토템 프리팹 {totemPrefabs.Length}종 등록 완료");
    }
}
