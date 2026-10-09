using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

/// <summary>
/// 디시그망 임시 레일건 연출 — 기본공격/스킬 투사체 프리팹 2종과 ProjectileData 2종을 만들고
/// Addressable(Data 그룹, ProjectileData 라벨)에 등록한 뒤 DisigmanBasicAttack/DisigmanSkill에 id를 연결한다.
/// 이 도구가 만든 에셋만 다시 덮어쓴다. 머티리얼은 하늘 레이저(SkyLaser) 공용 머티리얼을 재사용한다.
/// </summary>
public static class DisigmanRailgunFxSetup
{
    private const string MatRoot   = "Assets/WorkSpace/USW/Materials/Fx/SkyLaser";
    private const string OutRoot   = "Assets/WorkSpace/USW/Prefab/Effect/DisigmanRailgun";
    private const string DataRoot  = "Assets/WorkSpace/USW/Data/Projectile";
    private const string UnitRoot  = "Assets/WorkSpace/USW/Data/DroneUnits";
    private const string Group     = "Data";
    private const string Label     = "ProjectileData";
    private const string HitSfx    = "05.Drone_Attack_Hit";

    private const int BasicId = 6;
    private const int SkillId = 7;
    private const float SkillSize = 1.6f;

    private struct Variant
    {
        public string Name;
        public int Id;
        public float Charge;
        public Color Tint;
    }

    private static readonly Variant Basic = new Variant { Name = "Basic", Id = BasicId, Charge = 0.4f,  Tint = new Color(0.45f, 0.62f, 1f) };
    private static readonly Variant Skill = new Variant { Name = "Skill", Id = SkillId, Charge = 0.55f, Tint = new Color(0.55f, 0.9f, 1f) };

    [MenuItem("Tools/USW/Disigman Railgun FX/Build And Assign")]
    public static void BuildAll()
    {
        EnsureFolder(OutRoot);
        var basic = BuildData(Basic, BuildPrefab(Basic));
        var skill = BuildData(Skill, BuildPrefab(Skill));
        RegisterAddressable(basic);
        RegisterAddressable(skill);
        AssignSkills();
        AssetDatabase.SaveAssets();
        Debug.Log("[DisigmanRailgunFxSetup] 디시그망 레일건 투사체 2종 생성/연결 완료");
    }

    private static RailgunProjectile BuildPrefab(Variant v)
    {
        var root = new GameObject($"RailgunProjectile_{v.Name}");
        var projectile = root.AddComponent<RailgunProjectile>();
        var fxGo = new GameObject("Fx");
        fxGo.transform.SetParent(root.transform, false);
        var fx = fxGo.AddComponent<RailgunBeamFx>();

        var fxSo = new SerializedObject(fx);
        fxSo.FindProperty("_beamMaterial").objectReferenceValue  = Mat("M_SkyLaser_Beam");
        fxSo.FindProperty("_glowMaterial").objectReferenceValue  = Mat("M_SkyLaser_Glow");
        fxSo.FindProperty("_pointMaterial").objectReferenceValue = Mat("M_SkyLaser_Point");
        fxSo.FindProperty("_ringMaterial").objectReferenceValue  = Mat("M_SkyLaser_Ring");
        fxSo.FindProperty("_flareMaterial").objectReferenceValue = Mat("M_SkyLaser_Flare");
        fxSo.FindProperty("_tint").colorValue = v.Tint;
        fxSo.ApplyModifiedPropertiesWithoutUndo();

        var pSo = new SerializedObject(projectile);
        pSo.FindProperty("_chargeSeconds").floatValue = v.Charge;
        pSo.FindProperty("_fx").objectReferenceValue = fx;
        pSo.ApplyModifiedPropertiesWithoutUndo();

        string path = $"{OutRoot}/{root.name}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<RailgunProjectile>();
    }

    private static ProjectileData BuildData(Variant v, RailgunProjectile prefab)
    {
        string path = $"{DataRoot}/ProjectileData_DisigmanRailgun_{v.Name}.asset";
        var data = AssetDatabase.LoadAssetAtPath<ProjectileData>(path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<ProjectileData>();
            AssetDatabase.CreateAsset(data, path);
        }
        data.id = v.Id;
        data.projectilePrefab = prefab;
        data.projectileAddress = string.Empty;
        data.movement = new StraightMovement();
        data.fireEffect = null;
        data.hitEffect = new PrefabProjectileEffect { sfxName = HitSfx };
        EditorUtility.SetDirty(data);
        return data;
    }

    private static void RegisterAddressable(ProjectileData data)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings != null ? settings.FindGroup(Group) : null;
        if (group == null) { Debug.LogError($"[DisigmanRailgunFxSetup] Addressable 그룹 '{Group}' 없음"); return; }
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data));
        var entry = settings.CreateOrMoveEntry(guid, group);
        entry.address = $"ProjectileData/{data.name}";
        entry.SetLabel(Label, true, true);
        EditorUtility.SetDirty(settings);
    }

    private static void AssignSkills()
    {
        var basic = AssetDatabase.LoadAssetAtPath<SkillData>($"{UnitRoot}/DisigmanBasicAttack.asset");
        if (basic?.action is MultiShotSkillAction shot)
        {
            shot.projectileDataId = BasicId;
            EditorUtility.SetDirty(basic);
        }
        else Debug.LogError("[DisigmanRailgunFxSetup] DisigmanBasicAttack의 action이 MultiShotSkillAction이 아님");

        var skill = AssetDatabase.LoadAssetAtPath<SkillData>($"{UnitRoot}/DisigmanSkill.asset");
        if (skill?.action is DisigmanSkillAction cast)
        {
            cast.projectileDataId = SkillId;
            cast.sizeMultiplier = SkillSize;
            EditorUtility.SetDirty(skill);
        }
        else Debug.LogError("[DisigmanRailgunFxSetup] DisigmanSkill의 action이 DisigmanSkillAction이 아님");
    }

    private static Material Mat(string name)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MatRoot}/{name}.mat");
        if (mat == null) Debug.LogError($"[DisigmanRailgunFxSetup] 머티리얼 없음: {name}");
        return mat;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
