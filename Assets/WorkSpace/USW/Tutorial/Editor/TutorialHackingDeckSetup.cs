using System;
using System.Collections.Generic;
using System.Linq;
using GaeGGUL.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Authoring preset for the Betan merge / Deltan producer / Gamman consumer tutorial.</summary>
public static class TutorialHackingDeckSetup
{
    public const string DataPath = "Assets/WorkSpace/USW/Tutorial/Data";
    private const string ScenePath = "Assets/WorkSpace/USW/Tutorial/Scenes/TutorialScene.unity";

    public static void ConfigureSettings(IngameTutorialSettings settings)
    {
        UnitData Unit(string name)
        {
            string path = $"{DataPath}/TutorialUnit_{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<UnitData>(path) == null)
                AssetDatabase.CopyAsset($"Assets/WorkSpace/USW/Data/DroneUnits/UnitData_{name}.asset", path);
            return AssetDatabase.LoadAssetAtPath<UnitData>(path);
        }
        var betan = Unit("Betan");
        settings.SpawnUnits = new[] { betan, betan, Unit("Gamman"), Unit("Zeltan") };
        settings.MergeUnit = Unit("Deltan");
        settings.UpgradeTarget = "Deltan";
        settings.LevelUpChoices = new[] { 7, 8, 9 }.Select(id =>
            AssetDatabase.LoadAssetAtPath<LevelUpData>($"Assets/WorkSpace/USW/Data/SelectionData/DroneSelection{id}.asset")).ToArray();

        string totemPath = DataPath + "/TutorialSpeedTotem.asset";
        var totem = AssetDatabase.LoadAssetAtPath<TotemData>(totemPath);
        if (totem == null)
        {
            AssetDatabase.CopyAsset("Assets/WorkSpace/USW/Data/TotemData/Playable/TD1008Data.asset", totemPath);
            totem = AssetDatabase.LoadAssetAtPath<TotemData>(totemPath);
            totem.totemId = 99003;
            totem.totemName = "가속 토템";
            totem.totemType = TotemType.SpeedBuff;
            totem.UseSheetData = false;
            totem.isRotatable = true;
            totem.EffectGroups.Clear();
            totem.description = "앞쪽 1칸에 배치된 유닛의 「공격 속도」가 10% 증가합니다.\n회전하면 효과 범위의 방향이 바뀝니다.";
            totem.functions = new List<ITotemFunction> { new SimpleBuffFunction { kind = StatKind.Speed, amount = 0.1f } };
            totem.effectRanges = new List<ITotemRange> { new TotemRelativeOffsetRange { offsets = new List<Vector2Int> { Vector2Int.up } } };
            totem.attackDisabledRanges.Clear();
            EditorUtility.SetDirty(totem);
        }
        settings.TotemChoices = new[] { totem,
            AssetDatabase.LoadAssetAtPath<TotemData>("Assets/WorkSpace/USW/Data/TotemData/Playable/TD1001Data.asset"),
            AssetDatabase.LoadAssetAtPath<TotemData>("Assets/WorkSpace/USW/Data/TotemData/Playable/TD1005Data.asset") };
        settings.RequiredTotem = totem;
        EditorUtility.SetDirty(settings);
    }

    [MenuItem("Tools/USW/Tutorial/Apply Hacking Deck")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath || scene.isDirty)
            throw new InvalidOperationException("Open the saved TutorialScene outside Play Mode first.");
        var settings = AssetDatabase.LoadAssetAtPath<IngameTutorialSettings>(DataPath + "/IngameTutorialSettings.asset");
        ConfigureSettings(settings);
        foreach (var factory in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UnitFactory>(true)))
        {
            var so = new SerializedObject(factory);
            var units = settings.SpawnUnits.Append(settings.MergeUnit).Distinct().ToArray();
            var list = so.FindProperty("unitDataList");
            list.arraySize = units.Length;
            for (int i = 0; i < units.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = units[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (var reward in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TotemRewardUI>(true)))
        {
            var so = new SerializedObject(reward);
            so.FindProperty("_tutorialSettings").objectReferenceValue = settings;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        ConfigureDescriptions();
        ConfigureLessons();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void ConfigureLessons()
    {
        var texts = new Dictionary<IngameTutorialStage, string>
        {
            [IngameTutorialStage.FirstSummon] = "출격 버튼을 눌러 <color=#D66A27>「유닛」</color>을 소환해 보세요.",
            [IngameTutorialStage.Merge] = "강조된 두 유닛을 겹쳐 <color=#D66A27>「합성」</color>해 보세요.\n같은 유닛을 합성하면 더 강한 유닛을 얻을 수 있어요.",
            [IngameTutorialStage.UpgradeSlots] = "식량으로 <color=#D66A27>「델탕」</color>을 한 번 강화해 보세요.",
            [IngameTutorialStage.LevelUp] = "게이지가 가득 차면 <color=#D66A27>「선택지 보상」</color>을 얻어요.\n유닛을 강화할 보상을 선택해 보세요.",
            [IngameTutorialStage.TotemChoice] = "보스 처치! <color=#D66A27>「가속 토템」</color>을 선택해 보세요.\n효과 범위 안 유닛의 「공격 속도」가 증가해요.",
            [IngameTutorialStage.PlaceTotem] = "「가속 토템」을 강조된 빈칸으로\n<color=#D66A27>끌어서 설치</color>해 보세요.",
            [IngameTutorialStage.RotateTotem] = "토템을 <color=#D66A27>길게 누른 뒤 옆으로</color> 끌어 보세요.\n회전하여 「공격 속도」 증가 범위의 방향을 바꿀 수 있어요."
        };
        foreach (var pair in texts)
        {
            var lesson = AssetDatabase.LoadAssetAtPath<IngameTutorialLesson>($"{DataPath}/Steps/{(int)pair.Key:00}_{pair.Key}.asset");
            if (lesson == null) continue;
            lesson.Gameplay.Instruction = pair.Value;
            EditorUtility.SetDirty(lesson);
        }
    }

    private static void ConfigureDescriptions()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<DescriptionTermCatalog>("Assets/WorkSpace/USW/Resources/DescriptionTermCatalog.asset");
        void Term(string id, string title, string body)
        {
            var entry = catalog.Entries.FirstOrDefault(e => e.Id == id);
            if (entry == null) { entry = new DescriptionTermCatalog.Entry { Id = id }; catalog.Entries.Add(entry); }
            entry.DisplayName = title;
            entry.Body = body;
        }
        Term("hacking-stack", "해킹 스택", "델탕이 일반 공격으로 완충한 뒤 생산하는 자원입니다. 감망은 대상의 해킹 스택을 등급별 한도까지 소비해 스킬에 추가 피해를 더합니다.");
        Term("deltan-charge", "델탕 완충", "델탕은 일반 공격으로 충전합니다. 필요한 공격 횟수를 채우면 해킹 스택을 생산합니다. 공격 속도가 높으면 더 빠르게 충전합니다.");
        Term("gamman-buff", "감망 기폭", "감망은 해킹 스택을 소비해 기본 스킬 피해에 추가 피해를 더합니다. 주파수 동조는 스택 추가 피해 배율을 높이고, 긴급 교신은 알팡 액티브 사용 시 감망의 기폭을 발동합니다.");
        Term("alphan-active", "알팡 액티브", "알팡 족장의 액티브 스킬입니다. 필드의 전투 드론이 공격에 참여합니다. 선택지에 따라 쿨타임, 발사 횟수, 피해 또는 감망 기폭 발동이 달라집니다.");
        EditorUtility.SetDirty(catalog);
        foreach (int id in new[] { 7, 8, 9 })
        {
            var card = AssetDatabase.LoadAssetAtPath<LevelUpData>($"Assets/WorkSpace/USW/Data/SelectionData/DroneSelection{id}.asset");
            ChoiceDescriptionContent.Configure(card);
            EditorUtility.SetDirty(card);
        }
    }
}
