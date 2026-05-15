using UnityEngine;

/// <summary>
/// 토템 소환 서비스. 배치 로직만 담당.
///
/// Inspector:
///   genericPrefab — GenericBuffTotem + SpriteRenderer + DragHandler 가 붙은 프리팹 1개
///
/// 흐름:
///   Instantiate(genericPrefab) → SetTotemData(data) → OnPlaced(cell)
///   → UpdateSprite()가 rotationSprites[0] or icon 으로 스프라이트 설정
/// </summary>
public class TotemSpawner : InGameSingleton<TotemSpawner>
{
    [SerializeField] private GameObject genericPrefab;

    /// <summary>
    /// TotemData SO를 기반으로 토템을 소환하고 빈 셀에 배치한다.
    /// data.prefab이 있으면 그 프리팹을 사용 (특수 동작 토템).
    /// 없으면 genericPrefab을 사용하고 SO 데이터를 주입 (일반 수치 토템).
    /// </summary>
    public bool SpawnTotemByData(TotemData data)
    {
        if (data == null)
        {
            Debug.LogError("TotemSpawner: data null");
            return false;
        }

        bool useGeneric = data.prefab == null;
        var  prefab     = useGeneric ? genericPrefab : data.prefab;

        if (prefab == null)
        {
            Debug.LogError($"TotemSpawner: '{data.totemName}' — prefab 미연결 (genericPrefab도 없음)");
            return false;
        }

        var empty = Manager.Grid.GetEmptyCells();
        if (empty.Count == 0) return false;

        var go    = Instantiate(prefab);
        var totem = go.GetComponent<TotemBase>();

        if (totem == null)
        {
            Debug.LogError($"TotemSpawner: {prefab.name}에 TotemBase 컴포넌트 없음");
            Destroy(go);
            return false;
        }

        if (useGeneric) totem.SetTotemData(data);

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

    /// <summary>
    /// 배치된 토템을 판매(제거)하고 환급금을 지급한다.
    /// TotemInfoPopupUI의 판매 버튼에서 호출.
    /// </summary>
    public void SellTotem(TotemBase totem)
    {
        if (totem == null) return;

        var cell = totem.CurrentCell;

        totem.OnRemoved();
        if (cell != null) cell.RemoveTotem();
        Destroy(totem.gameObject);

        // 환급금: 추후 GameDataManager 또는 TotemData에 sellPrice 필드 추가 후 교체
        float refund = 0f;
        if (refund > 0f)
            Manager.Currency.AddCurrency(refund);
    }
}
