using UnityEngine;
using VContainer;
using VContainer.Unity;
using Cysharp.Threading.Tasks;
using System.Threading;

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
public class TotemSpawner : MonoBehaviour
{
 
    public void Init()
    {

        
    }

    [Inject] private IObjectResolver _resolver;
    [Inject] private GridManager _gridManager;
    [Inject] private CurrencyManager _currencyManager;
    [Inject] private UnitFactory _unitFactory;
    private CenterToast _centerToast;

    [Inject]
    private void ConfigureToast(IObjectResolver resolver) => resolver.TryResolve(out _centerToast);

    [SerializeField] private GameObject genericPrefab;

    [Header("Totem Settings")]
    [SerializeField] private Vector3 spawnScale = new Vector3(0.6f, 0.6f, 0.6f);

    /// <summary>
    /// TotemData SO를 기반으로 토템을 소환하고 빈 셀에 배치한다.
    /// data.prefab이 있으면 그 프리팹을 사용 (특수 동작 토템).
    /// 없으면 genericPrefab을 사용하고 SO 데이터를 주입 (일반 수치 토템).
    /// </summary>
    public async Cysharp.Threading.Tasks.UniTask<bool> SpawnTotemByData(TotemData data)
    {
        if (data == null)
        {
            Debug.LogError("TotemSpawner: data null");
            return false;
        }

        await data.LoadAssetsAsync();
        bool useGeneric = data.prefab == null;
        var  prefab     = useGeneric ? genericPrefab : data.prefab;

        if (prefab == null)
        {
            Debug.LogError($"TotemSpawner: '{data.totemName}' — prefab 미연결 (genericPrefab도 없음)");
            return false;
        }

        var empty = _gridManager.GetEmptyCells();
        if (empty.Count == 0) return false;

        var cell  = empty[Random.Range(0, empty.Count)];
        return PlaceLoadedTotem(data, prefab, cell);
    }

    /// <summary>Places a stored totem in the requested empty cell; never chooses a random cell.</summary>
    public async UniTask<bool> PlaceTotemAtCellAsync(TotemData data, GridCell cell, CancellationToken token)
    {
        if (data == null || cell == null || !cell.IsAvailable) return false;
        await data.LoadAssetsAsync().AttachExternalCancellation(token);
        token.ThrowIfCancellationRequested();
        if (cell == null || !cell.IsAvailable) return false;
        var prefab = data.prefab != null ? data.prefab : genericPrefab;
        if (prefab == null) return false;
        return PlaceLoadedTotem(data, prefab, cell);
    }

    /// <summary>Loads and validates the incoming object before removing the expected occupant. No await occurs after removal.</summary>
    public async UniTask<bool> PlaceOrExchangeTotemAsync(TotemData data, int rotation, GridCell cell,
        TotemBase expected, System.Func<bool> canCommit, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (data == null || !CanExchange(cell, expected) || canCommit?.Invoke() == false) return false;
        await data.LoadAssetsAsync().AttachExternalCancellation(token);
        token.ThrowIfCancellationRequested();
        if (!CanExchange(cell, expected) || canCommit?.Invoke() == false) return false;
        var prefab = data.prefab != null ? data.prefab : genericPrefab;
        if (prefab == null) return false;
        var go = RM.Instantiate(prefab, transform);
        if (go == null) return false;
        var incoming = go.GetComponent<TotemBase>();
        if (incoming == null) { Destroy(go); return false; }
        incoming.SetTotemData(data);
        if (token.IsCancellationRequested || !CanExchange(cell, expected) || canCommit?.Invoke() == false)
        { Destroy(go); token.ThrowIfCancellationRequested(); return false; }

        if (expected != null) { cell.RemoveTotem(); expected.OnRemoved(); }
        if (!cell.TryPlaceTotem(incoming))
        {
            if (expected != null && cell.TryPlaceTotem(expected)) expected.OnPlaced(cell);
            Destroy(go);
            return false;
        }
        go.transform.SetParent(cell.transform, false);
        FinishPlacement(incoming, cell, rotation);
        if (expected != null) { expected.gameObject.SetActive(false); Destroy(expected.gameObject); }
        return true;
    }

    private static bool CanExchange(GridCell cell, TotemBase expected)
        => cell != null && cell.Model != null && cell.Model.IsAvailable && cell.OccupyingUnit == null
            && ReferenceEquals(cell.OccupyingTotem, expected)
            && (ReferenceEquals(expected, null) || expected != null && expected.IsPlaced && expected.CurrentCell == cell);

    /// <summary>Removes field occupancy and effects without a sale; the caller retains the authored data first.</summary>
    public bool RemoveTotemFromField(TotemBase totem)
    {
        if (totem == null || !totem.IsPlaced || totem.CurrentCell == null || totem.CurrentCell.OccupyingTotem != totem) return false;
        var cell = totem.CurrentCell;
        cell.RemoveTotem();
        totem.OnRemoved();
        totem.gameObject.SetActive(false);
        Destroy(totem.gameObject);
        return true;
    }

    private bool PlaceLoadedTotem(TotemData data, GameObject prefab, GridCell cell)
    {
        var go    = RM.Instantiate(prefab, cell.transform);
        if (go == null) return false;
        var totem = go.GetComponent<TotemBase>();

        if (totem == null)
        {
            Debug.LogError($"TotemSpawner: {prefab.name}에 TotemBase 컴포넌트 없음");
            Destroy(go);
            return false;
        }

        totem.SetTotemData(data);
        if (!cell.TryPlaceTotem(totem))
        {
            Destroy(go);
            return false;
        }

        FinishPlacement(totem, cell, 0);
        return true;
    }

    private void FinishPlacement(TotemBase totem, GridCell cell, int rotation)
    {
        // 최초 설치와 이동(DragHandler.PlaceSelfAt) 양쪽 모두 UnitFactory.InitTotemTransform()
        // 하나로 통일한다. 유닛 전용 스폰 Y보정은 여기서 적용하지 않는다.
        var t = totem.transform;
        _unitFactory.InitTotemTransform(t);
        t.localScale = spawnScale;

        var drag = totem.GetComponent<DragHandler>();
        if (drag != null) 
        {
            drag.SetOriginCell(cell);
            drag.UpdateDepthSorting();
        }

        totem.OnPlaced(cell);
        totem.SetRotationStep(rotation);
    }

    /// <summary>
    /// 배치된 토템을 판매(제거)하고 환급금을 지급한다.
    /// TotemActionPopupUI의 판매 버튼에서 호출.
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
            _currencyManager.AddCurrency(refund);
        _centerToast?.Show("토템이 판매되었습니다", CenterToastKind.Info);
    }
}
