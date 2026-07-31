using System.Collections.Generic;
using UnityEngine;

/// <summary>이름/설명/아이콘을 갖는 패시브 데이터. 실제 수치 계산은 effects에 위임한다.</summary>
[CreateAssetMenu(fileName = "PassiveData", menuName = "Game/PassiveData")]
public class PassiveData : ScriptableObject, ILoadableAsset
{
    [ColorFoldoutGroup("기본 정보", "#8A8F98")] public int passiveId;
    [ColorFoldoutGroup("기본 정보", "#8A8F98")] public string passiveName;
    [ColorFoldoutGroup("기본 정보", "#8A8F98"), TextArea] public string description;

    [ColorFoldoutGroup("Addressables", "#14B8A6")] public string iconAddress;
    [HideInInspector] public Sprite icon;

    public bool IsLoaded => icon != null;

    [ColorFoldoutGroup("자기 자신에게 항상 적용되는 효과", "#4C8BF5")]
    [SerializeReference, SelectableReference]
    public List<IUnitPassiveEffect> selfEffects = new List<IUnitPassiveEffect>();

    [ColorFoldoutGroup("리더로 배치됐을 때 파티 전체에 적용되는 효과", "#A855F7")]
    [SerializeReference, SelectableReference]
    public List<IUnitPassiveEffect> partyEffects = new List<IUnitPassiveEffect>();

    /// <summary>selfEffects의 kind 스탯 보너스 합을 반환한다.</summary>
    public float GetSelfBonus(StatKind kind, UnitBase target, UnitDependencies deps, float bonusScale = 1f)
        => SumEffects(selfEffects, kind, target, deps, bonusScale);

    /// <summary>partyEffects의 kind 스탯 보너스 합을 반환한다.</summary>
    public float GetPartyBonus(StatKind kind, UnitBase target, UnitDependencies deps, float bonusScale = 1f)
        => SumEffects(partyEffects, kind, target, deps, bonusScale);

    /// <summary>selfEffects + partyEffects 전체 합을 반환한다 (CombinedPassiveEffect가 다른 PassiveData를 참조할 때 사용).</summary>
    public float GetAllBonus(StatKind kind, UnitBase target, UnitDependencies deps, float bonusScale = 1f)
        => GetSelfBonus(kind, target, deps, bonusScale) + GetPartyBonus(kind, target, deps, bonusScale);

    private static float SumEffects(List<IUnitPassiveEffect> effects, StatKind kind, UnitBase target, UnitDependencies deps, float bonusScale)
    {
        float bonus = 0f;
        if (effects == null) return bonus;

        foreach (var effect in effects)
        {
            if (effect != null) bonus += effect.GetBonus(kind, target, deps, bonusScale);
        }
        return bonus;
    }

    public async Cysharp.Threading.Tasks.UniTask LoadAssetsAsync()
    {
        if (!string.IsNullOrEmpty(iconAddress) && icon == null)
            icon = await RM.LoadAsync<Sprite>(iconAddress);
    }

    public void UnloadAssets()
    {
        if (icon != null)
        {
            RM.Unload(icon);
            icon = null;
        }
    }
}
