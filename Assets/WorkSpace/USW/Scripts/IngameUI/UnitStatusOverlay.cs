using System.Collections.Generic;
using UnityEngine;

/// <summary>공용 월드 캔버스에서 유닛 상태 프리팹의 생성·배치·수명을 관리합니다.</summary>
public sealed class UnitStatusOverlay : MonoBehaviour
{
    // 레벨 배지는 유닛·드론보다 항상 위에 그린다. 같은 "Unit" 레이어에서는 정렬 순서를 올려도 드론 스프라이트가
    // 위로 그려져서(실측 2026-09-24), 한 단계 위 레이어("FX")에 둔다.
    private const string SortingLayerAboveUnits = "FX";
    private const int SortingOrderAboveUnits = 29000;
    private readonly List<UnitStatusGraphic> _views = new List<UnitStatusGraphic>();
    private UnitStatusGraphic _prefab;

    /// <summary>그리드 수명의 캔버스를 만듭니다. 미지정 시 공통 Resources 프리팹을 사용합니다.</summary>
    public static UnitStatusOverlay Create(Transform owner, UnitStatusGraphic prefab = null)
    {
        var root = new GameObject("Unit Status Overlay", typeof(RectTransform), typeof(Canvas));
        root.layer = owner.gameObject.layer;
        root.transform.SetParent(owner, true);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingLayerName = SortingLayerAboveUnits;
        canvas.sortingOrder = SortingOrderAboveUnits;
        canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
        ((RectTransform)root.transform).sizeDelta = new Vector2(100f, 100f);
        var overlay = root.AddComponent<UnitStatusOverlay>();
        overlay._prefab = prefab != null ? prefab : Resources.Load<UnitStatusGraphic>("UnitStatus");
        if (overlay._prefab == null) Debug.LogError("UnitStatus: Resources/UnitStatus 프리팹이 없습니다.", owner);
        return overlay;
    }

    /// <summary>한 유닛에 한 프리팹을 연결합니다. 모든 표시가 같은 머티리얼을 공유합니다.</summary>
    public void Register(UnitBase unit)
    {
        if (unit == null || _prefab == null) return;
        foreach (var view in _views)
            if (view != null && view.Unit == unit) return;
        var instance = Instantiate(_prefab, transform, false);
        instance.name = "Status - " + unit.name;
        instance.Bind(unit);
        _views.Add(instance);
    }

    private void LateUpdate()
    {
        for (int i = _views.Count - 1; i >= 0; i--)
        {
            var view = _views[i];
            if (view == null || view.Unit == null)
            {
                if (view != null) Destroy(view.gameObject);
                _views.RemoveAt(i);
                continue;
            }
            view.Refresh();
        }
    }
}
