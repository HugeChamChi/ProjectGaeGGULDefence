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
public class TotemData : ScriptableObject, ILoadableAsset, IDebuffSource
{
    /// <summary>GenericBuffTotem groups replace shared functions/ranges when populated.</summary>
    [Header("효과별 설명 · 색상 · 기능 · 범위 (GenericBuffTotem)")]
    public List<TotemEffectGroup> EffectGroups = new List<TotemEffectGroup>();
    /// <summary>EffectGroups가 없거나, 대괄호 수가 그룹 수보다 많을 때 나머지 대괄호에 쓰는 색.</summary>
    [Tooltip("description의 [대괄호] 하이라이트 색. EffectGroups가 없는 토템(TD1010처럼 functions/effectRanges만 쓰는 경우)도 이 색으로 대괄호가 칠해집니다.")]
    public Color descriptionHighlightColor = new Color(1f, 0.5f, 0.05f, 1f);
    /// <summary>Whether this data uses independent effect groups.</summary>
    public bool HasEffectGroups => EffectGroups != null && EffectGroups.Exists(group => group != null);
    /// <summary>Rich-text descriptions use the same color as each effect range.
    /// description에 [대괄호]가 있으면 그 구간만 순서대로 EffectGroups 색(모자라면 descriptionHighlightColor)을 입힌 한 문장으로 표시하고,
    /// 대괄호가 없고 EffectGroups가 있으면 description을 색 없는 일반 설명으로 먼저 보여준 뒤 그 아래 효과별 색상 줄을 붙인다.</summary>
    public string GetDisplayDescription()
    {
        if (!string.IsNullOrEmpty(description) && description.Contains('['))
            return ApplyInlineGroupColors(description, EffectGroups);
        if (!HasEffectGroups) return description;
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(description)) lines.Add(description);
        foreach (var group in EffectGroups)
            if (group != null && !string.IsNullOrWhiteSpace(group.Description))
                lines.Add($"<color=#{ColorUtility.ToHtmlStringRGB(group.Color)}>{group.Description}</color>");
        return string.Join("\n", lines);
    }

    /// <summary>텍스트 안의 [대괄호] 구간을 나타난 순서대로 EffectGroups[0], [1]... 색으로 감싼다.
    /// 그룹이 없거나 대괄호 수가 그룹 수보다 많으면 descriptionHighlightColor를 쓴다.
    /// 대괄호 자체는 결과에서 사라지고 안쪽 글자만 남는다.</summary>
    private string ApplyInlineGroupColors(string text, List<TotemEffectGroup> groups)
    {
        var sb = new System.Text.StringBuilder();
        int groupIndex = 0;
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '[')
            {
                int close = text.IndexOf(']', i + 1);
                if (close < 0) { sb.Append(text, i, text.Length - i); break; } // 닫는 대괄호 없으면 나머지는 그대로 출력
                string inner = text.Substring(i + 1, close - i - 1);
                var group = groups != null && groupIndex < groups.Count ? groups[groupIndex] : null;
                var color = group != null ? group.Color : descriptionHighlightColor;
                sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{inner}</color>");
                groupIndex++;
                i = close + 1;
            }
            else { sb.Append(text[i]); i++; }
        }
        return sb.ToString();
    }
    /// <summary>TD1007 일반 공격 그림자 재현 구성.</summary>
    [Header("그림자 공격 (TotemShadowAttack)")]
    public TotemShadowAttackSettings ShadowAttack = new TotemShadowAttackSettings();

    /// <summary>기존 시트 동기화를 사용할지 여부. SO 제작 시 false.</summary>
    [Header("데이터 원본")]
    [Tooltip("끄면 SO의 수치/기능/범위/디버프를 그대로 사용합니다. 기존 시트 토템만 켜세요.")]
    public bool UseSheetData = true;

    /// <summary>일반 공격 반응 보조 투사체 구성.</summary>
    [Header("보조 투사체 (TotemBonusProjectile)")]
    public TotemBonusProjectileSettings BonusProjectile = new TotemBonusProjectileSettings();

    /// <summary>처치 수별 범위 프리셋.</summary>
    [Header("처치별 범위 성장 (TotemKillRangeGrowth)")]
    public List<TotemGrowthStage> GrowthStages = new List<TotemGrowthStage>();

    /// <summary>조커 생성 주기/유닛 구성.</summary>
    [Header("조커 소환 (TotemWildcardSpawner)")]
    public TotemWildcardSpawnSettings WildcardSpawn = new TotemWildcardSpawnSettings();

    /// <summary>임시 등급의 전체 UnitData 교체 설정.</summary>
    [Header("임시 등급업 (TotemTemporaryTierBoost)")]
    [Tooltip("기본은 같은 UnitData의 다음 등급 수치. 스킬/패시브까지 바꾸려면 상위 UnitData를 등록합니다.")]
    public List<TotemTierUpgrade> TierUpgrades = new List<TotemTierUpgrade>();

    [Header("디버프 (TD1003 / TD1006)")]
    [SerializeField] private DebuffBinding _debuffBinding;
    [SerializeField] private double _debuffFireInterval;
    [SerializeField] private double _debuffImpactDamage;
    private decimal? _sheetDebuffImpactDamage;
    [SerializeField] private ProjectileData _debuffProjectile;
    /// <summary>발사 간격(초), 부여자 설정.</summary>
    public double DebuffFireInterval => _debuffFireInterval;
    /// <summary>화염구 즉발 기본 피해, 부여자 설정.</summary>
    public decimal DebuffImpactDamage => _sheetDebuffImpactDamage ?? (decimal)_debuffImpactDamage;
    /// <summary>발사체 시각/이동 구성.</summary>
    public ProjectileData DebuffProjectile => _debuffProjectile;
    /// <inheritdoc />
    public bool TryGetDebuffBinding(out DebuffBinding binding)
    { binding = _debuffBinding; return binding.IsConfigured; }
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
    public float GetSimpleAmount(StatKind kind)
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
    {
        if (!HasEffectGroups) return CollectCells(effectRanges, totem, gridManager);
        var cells = new List<GridCell>();
        foreach (var group in EffectGroups)
            if (group != null) foreach (var cell in group.GetCells(totem, gridManager))
                if (!cells.Contains(cell)) cells.Add(cell);
        return cells;
    }

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
    public List<Vector2Int> GetEffectPreviewOffsets()
    {
        if (!HasEffectGroups) return CollectPreviewOffsets(effectRanges);
        var offsets = new List<Vector2Int>();
        foreach (var group in EffectGroups)
            if (group != null) foreach (var offset in group.GetPreviewOffsets())
                if (!offsets.Contains(offset)) offsets.Add(offset);
        return offsets;
    }

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
        if (!UseSheetData || row == null) return;

        // 1. 기본 정보 덮어쓰기
        if (!string.IsNullOrEmpty(row.TotemName)) totemName = row.TotemName;
        tier = row.Grade;
        if (row.DebuffBinding.HasValue) _debuffBinding = row.DebuffBinding.Value;
        if (row.DebuffFireInterval.HasValue) _debuffFireInterval = row.DebuffFireInterval.Value;
        if (row.DebuffImpactDamage.HasValue) _sheetDebuffImpactDamage = row.DebuffImpactDamage.Value;
        isRotatable = row.IsRotatable;

        // 2. functions 재구성 (시트의 flat 수치 기준 — 조건부 버프 등 SO 전용 구성은 시트에 없으므로
        //    여기서는 항상 단순 버프로만 재구성한다)
        functions = new List<ITotemFunction>();
        AddSimpleFunctionIfPositive(StatKind.AttackPercent,     row.AtkIncreaseRate);
        AddSimpleFunctionIfPositive(StatKind.Speed,      row.AttackSpeedIncreaseRate);
        AddSimpleFunctionIfPositive(StatKind.FoodSpeed,  row.FoodProductionRate);
        AddSimpleFunctionIfPositive(StatKind.FoodAmount, row.FoodAmount);
        AddSimpleFunctionIfPositive(StatKind.CritChance, row.CriticalChanceRate);
        AddSimpleFunctionIfPositive(StatKind.CritDamage, row.CriticalDamageRate);

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

    private void AddSimpleFunctionIfPositive(StatKind kind, float amount)
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
