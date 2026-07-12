using UnityEngine;
using System.Collections.Generic;

// 토템 종류
public enum TotemType
{
    AttackBuff,  // 좌우 1칸 — 공격력 +10%,             장판색: 빨강
    SpeedBuff,   // X자 대각 4칸 — 속도 +30%,           장판색: 주황
    FoodBuff,    // 우 2칸 — 식량 생산 간격 -20%,        장판색: 초록
    AttackTop,   // 최상단 전체 — 공격력 +30%,            장판색: 분홍
    Berserk,     // 상 1칸 — 공격력+10% 속도+10%,        장판색: 빨강+주황
    United,      // 상1+하1칸 — 앞 배치 시 공격력+속도+20%, 장판색: 노랑
    OverWelm,    // 상3칸 공격불가 + 하1칸 공격력+70%,   장판색: 청록+금색
}

[CreateAssetMenu(fileName = "TotemData", menuName = "Game/TotemData")]
public class TotemData : ScriptableObject, ILoadableAsset
{
    [Header("기본 정보")]
    public TotemType   totemType;
    public Tier        tier;
    public string      totemName;

    [Header("Addressables")]
    public string      iconAddress;
    public string      prefabAddress;
    public string[]    rotationSpriteAddresses = new string[4];

    [HideInInspector] public Sprite      icon;
    [HideInInspector] public GameObject  prefab;
    [HideInInspector] public Sprite[]    rotationSprites = new Sprite[4];
    [TextArea] public string description;

    public bool IsLoaded => icon != null || prefab != null;

    public Sprite DisplaySprite
        => rotationSprites != null && rotationSprites.Length > 0 && rotationSprites[0] != null
            ? rotationSprites[0]
            : icon;

    [Header("기능 / 범위 (SelectableReference)")]
    [SerializeReference, SelectableReference]
    public List<ITotemFunction> functions = new List<ITotemFunction>();
    [SerializeReference, SelectableReference]
    public List<ITotemRange> effectRanges = new List<ITotemRange>();
    [SerializeReference, SelectableReference]
    public List<ITotemRange> attackDisabledRanges = new List<ITotemRange>();

    /// <summary>functions 중 kind가 일치하는 첫 SimpleBuffFunction의 amount. 없으면 0.</summary>
    public float GetSimpleAmount(TotemBuffKind kind)
    {
        foreach (var fn in functions)
            if (fn is SimpleBuffFunction simple && simple.kind == kind) return simple.amount;
        return 0f;
    }

    /// <summary>functions 중 첫 FoodGeneratorFunction. 없으면 null (끝없는 수확류가 아닌 토템).</summary>
    public FoodGeneratorFunction GetFoodGenerator()
    {
        foreach (var fn in functions)
            if (fn is FoodGeneratorFunction gen) return gen;
        return null;
    }

    public List<GridCell> GetEffectCells(TotemBase totem, GridManager gridManager)
        => CollectCells(effectRanges, totem, gridManager);

    public List<GridCell> GetAttackDisabledCells(TotemBase totem, GridManager gridManager)
        => CollectCells(attackDisabledRanges, totem, gridManager);

    private static List<GridCell> CollectCells(List<ITotemRange> ranges, TotemBase totem, GridManager gridManager)
    {
        var result = new List<GridCell>();
        foreach (var range in ranges)
        {
            if (range == null) continue;
            foreach (var cell in range.GetCells(totem, gridManager))
                if (!result.Contains(cell)) result.Add(cell);
        }
        return result;
    }

    /// <summary>배치 전 UI 미리보기용 오프셋 (GridManager 불필요).</summary>
    public List<Vector2Int> GetEffectPreviewOffsets() => CollectPreviewOffsets(effectRanges);

    public List<Vector2Int> GetAttackDisabledPreviewOffsets() => CollectPreviewOffsets(attackDisabledRanges);

    private static List<Vector2Int> CollectPreviewOffsets(List<ITotemRange> ranges)
    {
        var result = new List<Vector2Int>();
        foreach (var range in ranges)
            if (range != null) result.AddRange(range.GetPreviewOffsets());
        return result;
    }

    [Header("시트 연동")]
    [Tooltip("구글 시트 totem_id 컬럼 값. 런타임 시트 데이터 매핑 키.")]
    public int  totemId     = 0;
    [Tooltip("회전 가능 여부. 시트 is_rotatable 컬럼이 있으면 런타임에 덮어쓰기됨.")]
    public bool isRotatable = true;

    /// <summary>
    /// 구글 시트에서 가져온 토템 데이터를 SO 인스턴스에 적용합니다.
    /// </summary>
    public void ApplySheetData(GameDataManager.TotemSheetRow row)
    {
        if (row == null) return;

        // 1. 기본 정보 덮어쓰기
        if (!string.IsNullOrEmpty(row.TotemName)) totemName = row.TotemName;
        tier = row.Grade;
        isRotatable = row.IsRotatable;

        // 2. functions 재구성 (시트의 flat 수치 기준 — 조건부 버프 등 SO 전용 구성은 시트에 없으므로
        //    여기서는 항상 단순 버프로만 재구성한다)
        functions = new List<ITotemFunction>();
        AddSimpleFunctionIfPositive(TotemBuffKind.Attack,     row.AtkIncreaseRate);
        AddSimpleFunctionIfPositive(TotemBuffKind.Speed,      row.AttackSpeedIncreaseRate);
        AddSimpleFunctionIfPositive(TotemBuffKind.FoodSpeed,  row.FoodProductionRate);
        AddSimpleFunctionIfPositive(TotemBuffKind.FoodAmount, row.FoodAmount);
        AddSimpleFunctionIfPositive(TotemBuffKind.CritChance, row.CriticalChanceRate);
        AddSimpleFunctionIfPositive(TotemBuffKind.CritDamage, row.CriticalDamageRate);

        // 3. 범위 데이터 덮어쓰기 (시트에 데이터가 존재할 경우에만 — 없으면 SO에 설정된 범위 유지)
        if (row.EffectRange != null && row.EffectRange.Count > 0)
        {
            effectRanges = new List<ITotemRange> { new TotemRelativeOffsetRange { offsets = new List<Vector2Int>(row.EffectRange) } };
        }

        if (row.AttackDisabledRange != null && row.AttackDisabledRange.Count > 0)
        {
            attackDisabledRanges = new List<ITotemRange> { new TotemRelativeOffsetRange { offsets = new List<Vector2Int>(row.AttackDisabledRange) } };
        }
    }

    private void AddSimpleFunctionIfPositive(TotemBuffKind kind, float amount)
    {
        if (amount > 0f) functions.Add(new SimpleBuffFunction { kind = kind, amount = amount });
    }

    public async Cysharp.Threading.Tasks.UniTask LoadAssetsAsync()
    {
        if (!string.IsNullOrEmpty(iconAddress) && icon == null)
            icon = await RM.LoadAsync<Sprite>(iconAddress);
            
        if (!string.IsNullOrEmpty(prefabAddress) && prefab == null)
            prefab = await RM.LoadAsync<GameObject>(prefabAddress);

        if (rotationSprites == null || rotationSprites.Length != 4)
            rotationSprites = new Sprite[4];

        if (rotationSpriteAddresses != null)
        {
            for (int i = 0; i < rotationSpriteAddresses.Length; i++)
            {
                if (!string.IsNullOrEmpty(rotationSpriteAddresses[i]) && i < rotationSprites.Length && rotationSprites[i] == null)
                    rotationSprites[i] = await RM.LoadAsync<Sprite>(rotationSpriteAddresses[i]);
            }
        }
    }

    public void UnloadAssets()
    {
        if (icon != null)
        {
            RM.Unload(icon);
            icon = null;
        }
        if (prefab != null)
        {
            RM.Unload(prefab);
            prefab = null;
        }
        if (rotationSprites != null)
        {
            for (int i = 0; i < rotationSprites.Length; i++)
            {
                if (rotationSprites[i] != null)
                {
                    RM.Unload(rotationSprites[i]);
                    rotationSprites[i] = null;
                }
            }
        }
    }
}
