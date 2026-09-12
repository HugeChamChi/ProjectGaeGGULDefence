using System;
using UnityEditor;
using UnityEngine;

/// <summary>IDebuffSource 공통 조회를 사용하는 로컬 SO/FK 검증 도구.</summary>
public static class DebuffDataValidator
{
    /// <summary>시트 연결 전/후 에셋의 정의와 등급별 FK를 검사한다.</summary>
    [MenuItem("Tools/Combat/Validate Debuff Data")]
    public static void Validate()
    {
        var settings = Resources.Load<DebuffSettings>("DebuffSettings");
        if (settings == null) throw new InvalidOperationException("Missing DebuffSettings.");
        var catalog = new DebuffCatalog(settings);
        int checkedBindings = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/WorkSpace" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<UnitData>(path);
            foreach (var tier in TierUtil.All)
                checkedBindings += ValidateSource(new UnitDebuffSource(data, tier), catalog, $"{path}/{tier}", true);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:TotemData", new[] { "Assets/WorkSpace" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            checkedBindings += ValidateSource(AssetDatabase.LoadAssetAtPath<TotemData>(path), catalog, path, false);
        }
        Debug.Log($"[DebuffDataValidator] Validated {checkedBindings} local bindings. Remote CSV validation runs separately during loading.");
    }
    private static int ValidateSource(IDebuffSource source, DebuffCatalog catalog, string path, bool unit)
    {
        if (source == null || !source.TryGetDebuffBinding(out var binding)) return 0;
        _ = new DebuffBinding(binding.DebuffId, binding.Trigger, binding.StacksPerApply);
        if (!catalog.TryGet(binding.DebuffId, out var definition)) throw new InvalidOperationException($"{path}: unresolved debuff_id {binding.DebuffId}");
        if (unit ? binding.Trigger != DebuffTrigger.SkillActivated : binding.Trigger == DebuffTrigger.SkillActivated)
            throw new InvalidOperationException($"{path}: unsupported trigger {binding.Trigger}");
        if (binding.Trigger == DebuffTrigger.AffectedUnitBasicAttackAttempt && (binding.StacksPerApply != 1 || definition.Kind != DebuffKind.ArmorBreak))
            throw new InvalidOperationException($"{path}: expected shared ArmorBreak +1");
        return 1;
    }
}
