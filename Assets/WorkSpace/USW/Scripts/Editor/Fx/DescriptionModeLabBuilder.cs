using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Object = UnityEngine.Object;

/// <summary>Builds an isolated description preview from the existing term-popup artwork.</summary>
public static class DescriptionModeLabBuilder
{
    public const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_DescriptionMode.unity";
    private const string Folder = "Assets/WorkSpace/USW/Data/UI/DescriptionModeLab";
    private const string SourceScene = "Assets/WorkSpace/USW/Scene/FxLab_TermPopup.unity";

    /// <summary>Creates a separate scene and sample assets; original scenes and glossary are preserved.</summary>
    [MenuItem("Tools/USW/Fx/Build Description Mode Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before building the lab.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save pending scene edits first.");
        var original = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/WorkSpace/USW/Data/UI", "DescriptionModeLab");
            var data = AssetDatabase.LoadAssetAtPath<DescriptionModeLabData>(Folder + "/Samples.asset");
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<DescriptionModeLabData>();
                data.Glossary = MakeGlossary();
                data.Selections = new[]
                {
                    Selection(2, "베탕의 전투 드론 공격 속도 +{percent}%.", "베탕 소유 전투 드론의 일반 공격 속도가 {percent}% 증가합니다."),
                    Selection(11, "족장 스킬이 {percent}% 피해로 {count}번 사격합니다.", "족장 집결 피해가 원래 피해의 {percent}%로 {interval}초 간격으로 {count}회 적용됩니다."),
                    Selection(22, "자폭 드론이 피해 대신 해킹 스택 +{count}. 수리 키트도 발동합니다.", "모든 베탕 자폭 드론이 폭발 피해 대신 적중 시 해킹 스택 {count}개를 쌓습니다. 수리 키트도 발동합니다.")
                };
                data.Totems = new[]
                {
                    Totem(1001, "범위 안 유닛의 일반 공격 시 [{chance}% 확률로 공격력 {coefficient}%의 보조 투사체] 발사.",
                        "범위 안 유닛이 일반 공격할 때마다 [{chance}% 확률로 보조 투사체]를 발사합니다. 피해는 해당 유닛 공격력의 {coefficient}%입니다."),
                    Totem(1002, "전투 {mature}분 동안 공격력 +{start}→{end}%·범위 성장. 만개 후 {harvest}분마다 식량 {food}. 이동·회수 시 초기화.", "{growthDetails}"),
                    Totem(1009, "범위 안 유닛 [공격력 +{attack}%·공격 속도 -{speedPenalty}%].", "범위 안 유닛의 [공격력이 {attack}% 증가]하고, 공격 속도가 {speedPenalty}% 감소합니다.")
                };
                AssetDatabase.CreateAsset(data, Folder + "/Samples.asset");
            }
            foreach (var entry in data.Selections.Concat(data.Totems)) entry.Resolve();
            var source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(source, ScenePath, true)) throw new InvalidOperationException("Could not copy source scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var old = Find<TermPopupLab>(scene);
            if (old == null) throw new InvalidOperationException("Source lab driver missing.");
            var oldSo = new SerializedObject(old);
            var canvas = oldSo.FindProperty("_popupCanvas").objectReferenceValue;
            var container = oldSo.FindProperty("_cardContainer").objectReferenceValue;
            var prefab = oldSo.FindProperty("_cardPrefab").objectReferenceValue;
            var rounded = oldSo.FindProperty("_roundedSprite").objectReferenceValue;
            var blur = oldSo.FindProperty("_blurShader").objectReferenceValue;
            var root = (RectTransform)canvas;
            var buttons = root.Find("LabButtons");
            if (buttons != null) Object.DestroyImmediate(buttons.gameObject);
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                if (text.text.Contains("필드")) text.gameObject.SetActive(false);
            Object.DestroyImmediate(old.gameObject);
            var driver = new GameObject("DescriptionModeLab").AddComponent<DescriptionModeLab>();
            var so = new SerializedObject(driver);
            so.FindProperty("_canvas").objectReferenceValue = canvas;
            so.FindProperty("_cardContainer").objectReferenceValue = container;
            so.FindProperty("_cardPrefab").objectReferenceValue = prefab;
            // Opening the source scene may unload newly-created, unreferenced assets.
            so.FindProperty("_data").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DescriptionModeLabData>(Folder + "/Samples.asset");
            so.FindProperty("_rounded").objectReferenceValue = rounded;
            so.FindProperty("_blur").objectReferenceValue = blur;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        catch
        {
            EditorSceneManager.RestoreSceneManagerSetup(original);
            throw;
        }
    }

    private static TermGlossary MakeGlossary()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TermGlossary>(Folder + "/Glossary.asset");
        if (existing != null) return existing;
        var glossary = Object.Instantiate(AssetDatabase.LoadAssetAtPath<TermGlossary>("Assets/WorkSpace/USW/Data/UI/TermGlossary.asset"));
        glossary.name = "DescriptionModeLabGlossary";
        SetTerm(glossary, "chief_skill", "족장 스킬", "버튼을 눌러 드론을 모아 일제 사격합니다.", "참여 드론이 많을수록 피해가 커집니다. 보스의 패턴을 끊을 수 있습니다.");
        SetTerm(glossary, "self_destruct_drone", "자폭 드론", "보스에게 돌진해 폭발하는 드론입니다.", "기본 피해는 소유 유닛 공격력 ×{coefficient}를 기준으로 계산합니다. 폭탄마다 치명타를 따로 판정합니다.");
        SetTerm(glossary, "hack_stack", "해킹", "보스에게 스택을 쌓아 기폭 피해를 높입니다.", "같은 보스의 스택을 공유하며 최대 {capacity}개까지 쌓습니다. 기폭 시 소비한 스택만큼 추가 피해를 줍니다.");
        SetTerm(glossary, "time_manipulation", "글리치", "시간을 회복하거나 소모해 추가 효과를 얻습니다.", "회복은 남은 시간을 늘리고, 소비는 시간을 지불해 효과를 얻습니다. 자연 감소는 소비에 포함하지 않습니다.");
        AddTerm(glossary, "combat_drone", "전투 드론", new[] { "전투 드론", "전투드론" }, "유닛 곁에서 보스를 공격하는 드론입니다.", "일반 공격을 반복하고 족장 집결 사격에 참여합니다. 자폭 드론은 개수에 포함하지 않습니다.");
        AddTerm(glossary, "food", "식량", new[] { "식량" }, "유닛 출격과 강화에 쓰는 재화입니다.", "유닛과 전투 드론이 생산하며, 생산량은 현재 효과에 따라 달라집니다.");
        AssetDatabase.CreateAsset(glossary, Folder + "/Glossary.asset");
        return glossary;
    }

    private static void SetTerm(TermGlossary glossary, string id, string title, string brief, string detailed)
    {
        if (!glossary.TryFind(id, out var entry)) throw new InvalidOperationException("Missing original term: " + id);
        entry.DisplayName = title;
        entry.Aliases = entry.Aliases.Concat(new[] { title }).Distinct().ToArray();
        entry.BriefBody = brief;
        entry.DetailedBody = detailed;
    }
    private static void AddTerm(TermGlossary glossary, string id, string title, string[] aliases, string brief, string detailed)
    {
        glossary.AddIfMissing(new TermEntry { Id = id, DisplayName = title, Aliases = aliases, Kind = "용어",
            Body = brief, BriefBody = brief, DetailedBody = detailed, KindColor = new Color(0.25f, 0.57f, 0.70f) });
    }
    private static DescriptionModeLabEntry Selection(int number, string brief, string detailed)
        => new DescriptionModeLabEntry { Selection = AssetDatabase.LoadAssetAtPath<LevelUpData>($"Assets/WorkSpace/USW/Data/SelectionData/DroneSelection{number}.asset"), BriefTemplate = brief, DetailedTemplate = detailed };
    private static DescriptionModeLabEntry Totem(int number, string brief, string detailed)
        => new DescriptionModeLabEntry { Totem = AssetDatabase.LoadAssetAtPath<TotemData>($"Assets/WorkSpace/USW/Data/TotemData/Playable/TD{number}Data.asset"), BriefTemplate = brief, DetailedTemplate = detailed };
    private static T Find<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        { var component = root.GetComponentInChildren<T>(true); if (component != null) return component; }
        return null;
    }
}
