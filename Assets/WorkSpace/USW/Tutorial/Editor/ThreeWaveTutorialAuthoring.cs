using System;
using System.Linq;
using GaeGGUL.Tutorial;
using UnityEditor;
using UnityEngine;

/// <summary>Updates only the authored tutorial assets to the approved three-wave lesson order.</summary>
public static class ThreeWaveTutorialAuthoring
{
    private const string Root = "Assets/WorkSpace/USW/Tutorial/Data/";

    /// <summary>Idempotent authoring, preserving existing asset GUIDs and scene bindings.</summary>
    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before authoring.");
        var settings = Load<IngameTutorialSettings>("IngameTutorialSettings.asset");
        settings.MergeUnit = settings.SpawnUnits[0]; settings.UpgradeTarget = "Betan";
        settings.GrantedChoices = Array.Empty<LevelUpData>();
        settings.RequiredTotem = AssetDatabase.LoadAssetAtPath<TotemData>("Assets/WorkSpace/USW/Resources/LevelTotems/LT2001.asset");
        settings.TotemChoices = new[] { settings.RequiredTotem,
            AssetDatabase.LoadAssetAtPath<TotemData>("Assets/WorkSpace/USW/Resources/LevelTotems/LT2002.asset"),
            AssetDatabase.LoadAssetAtPath<TotemData>("Assets/WorkSpace/USW/Resources/LevelTotems/LT2003.asset") };
        settings.TotemRotateSuccessInstruction = "기본 레벨 2에 토템 +2가 더해져 4레벨이 됐어요. 3레벨 효과도 켜져요!";
        settings.ChoicePreviewInstruction = "필드 보기 버튼으로 배치와 레벨을 확인할 수 있어요.";
        settings.CombatPracticeInstruction = "저주 해제까지 배웠어요. 이제 사슴을 물리쳐 보세요!";
        settings.DeerPreparationHpFloor = .55f; settings.PracticeConversionStones = 1;
        EditorUtility.SetDirty(settings);
        var plan = Load<IngameTutorialPlan>("FirstBattleSequence.asset"); plan.Lessons.Clear();
        Add(plan, IngameTutorialStage.BossEntrance, "첫 전투를 시작해요.");
        Add(plan, IngameTutorialStage.TimeLimit, "남은 시간 안에 적을 물리쳐요. 처치하면 60초로 돌아와요.");
        Add(plan, IngameTutorialStage.FirstSummon, "식량으로 출격해요. 출격 버튼을 네 번 눌러 주세요.");
        Add(plan, IngameTutorialStage.ObserveCombat, "유닛이 자동으로 공격해요.");
        Add(plan, IngameTutorialStage.Merge, "같은 종류, 같은 기본 레벨의 베탕을 겹쳐 주세요. 1레벨 둘이 2레벨 하나가 돼요.");
        Add(plan, IngameTutorialStage.OpenUpgrade, "이번에는 식량으로 베탕 종류를 강화해 볼게요.");
        Add(plan, IngameTutorialStage.UpgradeSlots, "베탕 강화를 한 번 눌러 주세요. 지금 있는 베탕과 앞으로 나올 베탕이 함께 강해져요.");
        Add(plan, IngameTutorialStage.SecondWave, "악어가 나타나요.");
        Add(plan, IngameTutorialStage.EarthquakeTelegraph, "빛나는 칸은 공격 예고예요.");
        Add(plan, IngameTutorialStage.ExperienceGauge, "적에게 피해를 주면 경험치가 쌓여요. 가득 차면 선택지를 골라요.");
        Add(plan, IngameTutorialStage.LevelUp, "세 선택지 중 하나를 골라 이번 전투를 강화할 수 있어요.");
        Add(plan, IngameTutorialStage.TotemChoice, "전방 +2 토템을 골라 주세요. 앞 칸 유닛의 레벨을 2 올려요.");
        Add(plan, IngameTutorialStage.OpenInventory, "토템 보관함을 열어 주세요.");
        Add(plan, IngameTutorialStage.PlaceTotem, "토템을 표시된 빈칸에 놓아 주세요.");
        Add(plan, IngameTutorialStage.MoveTotem, "베탕 옆으로 토템을 옮겨 주세요.");
        Add(plan, IngameTutorialStage.RotateTotem, "토템을 길게 누른 뒤 베탕 쪽으로 끌어 회전해 주세요.");
        Add(plan, IngameTutorialStage.Conversion, "변환석으로 유닛의 종류를 바꿀 수 있어요.");
        Add(plan, IngameTutorialStage.DeerCurse, "저주에 묶인 유닛을 직접 눌러 풀어 주세요.");
        Add(plan, IngameTutorialStage.FinalBattle, "사슴을 물리쳐 주세요.");
        if (plan.Validate() is string error) throw new InvalidOperationException(error);
        EditorUtility.SetDirty(plan);
        var config = Load<GameConfig>("TutorialGameConfig.asset"); config.countdownSeconds = 60; EditorUtility.SetDirty(config);
        ConfigureBoss("TutorialBoss.asset", "Assets/WorkSpace/USW/Data/BossData/BossData_Round02.asset", 5000, 0);
        ConfigureBoss("TutorialSecondBoss.asset", "Assets/WorkSpace/USW/Data/BossData/Crocodile/CrocodilePrototypeData.asset", 7000, 100);
        ConfigureBoss("TutorialThirdBoss.asset", "Assets/WorkSpace/USW/Data/BossData/Deertree/DeertreeBossData.asset", 9000, 0);
        var deerSettings = Copy<DeertreeBossSettings>("Assets/WorkSpace/USW/Data/BossData/Deertree/DeertreePatterns.asset", "TutorialDeerSettings.asset");
        deerSettings.CurseOnlyPractice = true; deerSettings.CurseTargets = 1; deerSettings.CurseDuration = 15;
        EditorUtility.SetDirty(deerSettings);
        var deerData = Load<BossData>("TutorialThirdBoss.asset");
        var deerSo = new SerializedObject(deerData);
        var source = AssetDatabase.GetAssetPath(deerSo.FindProperty("_prefab").objectReferenceValue);
        string prefabPath = Root + "TutorialDeer.prefab";
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)) AssetDatabase.CopyAsset(source, prefabPath);
        var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var so = new SerializedObject(prefab.GetComponent<BossDeertree>());
            so.FindProperty("_settings").objectReferenceValue = deerSettings; so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        deerSo.FindProperty("_prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        deerSo.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return "Authored 19 lessons, three encounters, level totems, 60 seconds and one-target curse practice.";
    }

    private static T Load<T>(string name) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(Root + name);
    private static T Copy<T>(string source, string name) where T : UnityEngine.Object
    {
        if (!Load<T>(name)) AssetDatabase.CopyAsset(source, Root + name);
        return Load<T>(name);
    }
    private static void Add(IngameTutorialPlan plan, IngameTutorialStage stage, string instruction)
    {
        string name = $"Steps/{(int)stage:00}_{stage}.asset";
        var lesson = Load<IngameTutorialLesson>(name);
        if (lesson == null) { lesson = ScriptableObject.CreateInstance<IngameTutorialLesson>(); AssetDatabase.CreateAsset(lesson, Root + name); }
        lesson.Kind = IngameTutorialLesson.LessonKind.GameplayRecipe;
        lesson.Gameplay = new IngameTutorialStep { Stage = stage, Instruction = instruction };
        plan.Lessons.Add(lesson); EditorUtility.SetDirty(lesson);
    }
    private static void ConfigureBoss(string target, string source, float hp, float exp)
    {
        var data = Load<BossData>(target);
        EditorUtility.CopySerialized(AssetDatabase.LoadAssetAtPath<BossData>(source), data);
        data.name = System.IO.Path.GetFileNameWithoutExtension(target);
        var so = new SerializedObject(data);
        so.FindProperty("_maxHp").longValue = (long)hp; so.FindProperty("_expReward").floatValue = exp;
        so.FindProperty("_defense").floatValue = 0; so.FindProperty("_hpLineCount").intValue = 3;
        so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(data);
    }
}
