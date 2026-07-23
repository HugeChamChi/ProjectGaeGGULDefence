using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>SkillData/BuffData/UnitData 에셋이 프로젝트 창·인스펙터에서 HSD/Editor의 전용
/// 아이콘으로 표시되도록, 각 클래스의 스크립트 에셋 자체에 커스텀 아이콘을 설정한다.</summary>
public static class AssignScriptIcons
{
    private const string IconDir = "Assets/WorkSpace/HSD/Editor";

    [MenuItem("Tools/Assign Script Icons")]
    public static void Assign()
    {
        int count = 0;
        count += SetIcon<SkillData>($"{IconDir}/SkillIcon.png");
        count += SetIcon<BuffData>($"{IconDir}/BuffIcon.png");
        count += SetIcon<UnitData>($"{IconDir}/UnitIcon.png");

        AssetDatabase.SaveAssets();
        Debug.Log($"[AssignScriptIcons] {count}개 타입에 아이콘을 설정했습니다.");
    }

    private static int SetIcon<T>(string iconPath)
    {
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        if (icon == null)
        {
            Debug.LogWarning($"[AssignScriptIcons] 아이콘을 찾을 수 없습니다: {iconPath}");
            return 0;
        }

        var script = MonoImporter.GetAllRuntimeMonoScripts()
            .FirstOrDefault(s => s.GetClass() == typeof(T));
        if (script == null)
        {
            Debug.LogWarning($"[AssignScriptIcons] {typeof(T).Name}의 스크립트 에셋을 찾을 수 없습니다.");
            return 0;
        }

        string scriptPath = AssetDatabase.GetAssetPath(script);
        var importer = (MonoImporter)AssetImporter.GetAtPath(scriptPath);
        importer.SetIcon(icon);
        importer.SaveAndReimport();
        return 1;
    }
}
