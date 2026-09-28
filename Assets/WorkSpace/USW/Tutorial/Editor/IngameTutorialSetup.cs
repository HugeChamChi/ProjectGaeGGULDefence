using System;
using System.Linq;
using GaeGGUL.Tutorial;
using HSD.UI.Upgrade;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Wires the authored first-battle tutorial into the user's existing TutorialScene.</summary>
public static class IngameTutorialSetup
{
    private const string ScenePath = "Assets/WorkSpace/USW/Tutorial/Scenes/TutorialScene.unity";
    private const string DataPath = "Assets/WorkSpace/USW/Tutorial/Data";

    /// <summary>Creates missing tutorial data and connects only TutorialScene, preserving other scenes.</summary>
    [MenuItem("Tools/USW/Tutorial/Setup TutorialScene")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        TutorialAssetOrganization.Organize();
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || scene.isDirty)
            throw new InvalidOperationException("Open the saved TutorialScene before setup; unsaved scenes are never replaced.");
        System.IO.Directory.CreateDirectory("Temp");
        System.IO.File.Copy(ScenePath, "Temp/TutorialScene-before-setup.unity", true);
        if (!AssetDatabase.IsValidFolder(DataPath)) AssetDatabase.CreateFolder("Assets/WorkSpace/USW/Tutorial", "Data");
        var settings = AssetDatabase.LoadAssetAtPath<IngameTutorialSettings>(DataPath + "/IngameTutorialSettings.asset");
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<IngameTutorialSettings>();
            UnitData CloneUnit(string name)
            {
                var source = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/WorkSpace/USW/Data/DroneUnits/UnitData_" + name + ".asset");
                return CopyAsset(source, "TutorialUnit_" + name);
            }
            var attack = CloneUnit("Deltan");
            settings.SpawnUnits = new[] { attack, attack, CloneUnit("Zeltan"), CloneUnit("Gamman") };
            settings.SpawnCells = new[] { new Vector2Int(1,1), new Vector2Int(2,1), new Vector2Int(1,2), new Vector2Int(2,2) };
            AssetDatabase.CreateAsset(settings, DataPath + "/IngameTutorialSettings.asset");
        }
        var sequence = AssetDatabase.LoadAssetAtPath<TutorialSequence>(DataPath + "/IngameTutorialSequence.asset");
        if (sequence == null)
        {
            sequence = ScriptableObject.CreateInstance<TutorialSequence>();
            sequence.tutorialID = "FirstBattle";
            foreach (IngameTutorialStage stage in Enum.GetValues(typeof(IngameTutorialStage)))
                sequence.steps.Add(new IngameTutorialStep { Stage = stage });
            AssetDatabase.CreateAsset(sequence, DataPath + "/IngameTutorialSequence.asset");
        }
        var game = One<GameManager>(scene);
        string[] instructions =
        {
            "출격을 눌러 첫 유닛을 소환해 주세요!",
            "보스가 나타났어요. 유닛들이 보스를 공격합니다!",
            "식량이 모이면 출격을 세 번 눌러 동료를 불러 주세요!",
            "같은 유닛을 서로 겹치도록 끌어 합성해 주세요!",
            "레벨이 올랐어요! 원하는 능력을 하나 선택할 수 있어요.",
            "식량을 모았어요. 강화 버튼을 눌러 보세요!",
            "이 슬롯에서 유닛을 강화할 수 있어요!",
            "족장 스킬이 준비되면 이 버튼으로 사용할 수 있어요!",
            "보스를 물리쳤어요! 마음에 드는 토템을 선택해 주세요.",
            "토템 버튼을 눌러 획득한 토템을 확인해 주세요!",
            "토템을 표시된 빈칸으로 끌어 배치해 주세요!",
            "배치한 토템을 바로 끌어 표시된 칸으로 옮겨 주세요!",
            "토템을 꾹 눌러 게이지를 채운 뒤 옆으로 끌어 회전해 주세요!"
        };
        foreach(var step in sequence.steps.OfType<IngameTutorialStep>())
            if(string.IsNullOrWhiteSpace(step.Instruction)) step.Instruction=instructions[(int)step.Stage-1];
        EditorUtility.SetDirty(sequence);
        var plan = AssetDatabase.LoadAssetAtPath<IngameTutorialPlan>(DataPath + "/FirstBattleSequence.asset");
        if (plan == null)
        {
            if (!AssetDatabase.IsValidFolder(DataPath + "/Steps")) AssetDatabase.CreateFolder(DataPath, "Steps");
            plan = ScriptableObject.CreateInstance<IngameTutorialPlan>();
            foreach (var step in sequence.steps.OfType<IngameTutorialStep>())
            {
                var lesson = ScriptableObject.CreateInstance<IngameTutorialLesson>();
                lesson.Gameplay = step;
                AssetDatabase.CreateAsset(lesson, $"{DataPath}/Steps/{(int)step.Stage:00}_{step.Stage}.asset");
                plan.Lessons.Add(lesson);
            }
            AssetDatabase.CreateAsset(plan, DataPath + "/FirstBattleSequence.asset");
        }
        var exp = AssetDatabase.LoadAssetAtPath<ExpLevelData>(DataPath + "/TutorialExpLevels.asset");
        if(exp == null)
        {
            exp=ScriptableObject.CreateInstance<ExpLevelData>();
            var so=new SerializedObject(exp);var values=so.FindProperty("expRequired");values.arraySize=19;
            for(int i=0;i<values.arraySize;i++)values.GetArrayElementAtIndex(i).floatValue=i==0?10:1000;
            so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.CreateAsset(exp,DataPath+"/TutorialExpLevels.asset");
        }
        Set(One<ExpManager>(scene),"expLevelData",exp);
        bool createConfig = AssetDatabase.LoadAssetAtPath<GameConfig>(DataPath + "/TutorialGameConfig.asset") == null;
        var config = CopyAsset(game.Config, "TutorialGameConfig");
        if (createConfig)
        {
            config.startingFood = settings.InitialCost;
            config.countdownSeconds = 600;
            config.bossSpawnDelaySeconds = 0;
            EditorUtility.SetDirty(config);
        }
        Set(game, "config", config);
        var wave = One<WaveManager>(scene);
        Set(wave,"config",config);
        var stageSource = (StageData)new SerializedObject(wave).FindProperty("stageData").objectReferenceValue;
        var stageData = AssetDatabase.LoadAssetAtPath<StageData>(DataPath + "/TutorialStage.asset");
        if (stageData == null)
        {
            var sourceEntry = stageSource.waves[0].bosses[0];
            var boss = sourceEntry.Data != null ? Object.Instantiate(sourceEntry.Data) : ScriptableObject.CreateInstance<BossData>();
            boss.name = "TutorialBoss";
            var so = new SerializedObject(boss);
            so.FindProperty("_prefab").objectReferenceValue = sourceEntry.Prefab;
            so.FindProperty("_icon").objectReferenceValue = sourceEntry.Icon;
            so.FindProperty("_maxHp").longValue = 12000;
            so.FindProperty("_defense").doubleValue = 0;
            so.FindProperty("_expReward").floatValue = 100;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(boss, DataPath + "/TutorialBoss.asset");
            var first = ScriptableObject.CreateInstance<WaveData>();
            first.waveName = "Tutorial first boss"; first.bosses = new[] { new BossEntry { Data = boss } };
            AssetDatabase.CreateAsset(first, DataPath + "/TutorialWave.asset");
            stageData = Object.Instantiate(stageSource);
            stageData.waves = new[] {first}.Concat(stageSource.waves.Skip(1)).ToArray();
            AssetDatabase.CreateAsset(stageData, DataPath + "/TutorialStage.asset");
        }
        Set(wave,"stageData",stageData);
        foreach(var factory in All<UnitFactory>(scene))
        {
            var so = new SerializedObject(factory);
            so.FindProperty("_useAuthoredParty").boolValue = true;
            var list = so.FindProperty("unitDataList");
            var units = settings.SpawnUnits.Distinct().ToArray(); list.arraySize = units.Length;
            for(int i=0;i<units.Length;i++) list.GetArrayElementAtIndex(i).objectReferenceValue = units[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        Set(One<UnitSpawner>(scene),"_tutorialSettings",settings);
        var chiefSo=new SerializedObject(One<ChieftainSpawner>(scene));
        chiefSo.FindProperty("_useAuthoredSelection").boolValue=true;
        chiefSo.ApplyModifiedPropertiesWithoutUndo();
        var levelUp = One<LevelUpUI>(scene);
        var levelSo = new SerializedObject(levelUp);
        levelSo.FindProperty("_disableSelectionTimer").boolValue = true;
        levelSo.ApplyModifiedPropertiesWithoutUndo();
        var reward = One<TotemRewardUI>(scene);
        var rewardSo = new SerializedObject(reward);
        var pool = rewardSo.FindProperty("_totemPool"); pool.arraySize = 3;
        var ids = new[] {1001,1005,1006};
        for(int i=0;i<ids.Length;i++) pool.GetArrayElementAtIndex(i).objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<TotemData>($"Assets/WorkSpace/USW/Data/TotemData/Playable/TD{ids[i]}Data.asset");
        rewardSo.ApplyModifiedPropertiesWithoutUndo();
        var director = All<IngameTutorialDirector>(scene).FirstOrDefault();
        if (director == null) director = new GameObject("IngameTutorial").AddComponent<IngameTutorialDirector>();
        var overlay = director.GetComponentInChildren<IngameTutorialOverlay>(true);
        if (overlay == null)
        {
            var canvasGo = new GameObject("TutorialCanvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(director.transform,false);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
            var overlayGo = new GameObject("TutorialHighlight",typeof(RectTransform),typeof(CanvasRenderer),typeof(IngameTutorialOverlay));
            overlayGo.transform.SetParent(canvasGo.transform,false);
            var rect = (RectTransform)overlayGo.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            overlay = overlayGo.GetComponent<IngameTutorialOverlay>();
        }
        var uiSo = new SerializedObject(One<UIManager>(scene));
        var dialogue = director.GetComponentInChildren<IngameTutorialDialogue>(true);
        if(dialogue == null)
        {
            var canvas = overlay.GetComponentInParent<Canvas>();
            var scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=0;
            var panelGo=new GameObject("TutorialDialogue",typeof(RectTransform),typeof(Image),typeof(IngameTutorialDialogue));
            panelGo.transform.SetParent(canvas.transform,false);
            var rect=(RectTransform)panelGo.transform;rect.anchorMin=new Vector2(0.045f,0.83f);rect.anchorMax=new Vector2(0.955f,0.96f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            var bg=panelGo.GetComponent<Image>();bg.color=new Color(0.06f,0.085f,0.12f,0.96f);bg.raycastTarget=false;
            dialogue=panelGo.GetComponent<IngameTutorialDialogue>();
            var font=((TMPro.TMP_Text)uiSo.FindProperty("currencyText").objectReferenceValue).font;
            TMPro.TMP_Text Label(string name,Vector2 min,Vector2 max,float size,Color color)
            {
                var go=new GameObject(name,typeof(RectTransform),typeof(TMPro.TextMeshProUGUI));go.transform.SetParent(rect,false);
                var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;
                var text=go.GetComponent<TMPro.TextMeshProUGUI>();text.font=font;text.fontSize=size;text.color=color;text.raycastTarget=false;
                text.alignment=TMPro.TextAlignmentOptions.MidlineLeft;text.enableAutoSizing=true;text.fontSizeMin=size*0.75f;text.fontSizeMax=size;
                return text;
            }
            Set(dialogue,"_progress",Label("Progress",new Vector2(.04f,.76f),new Vector2(.96f,.96f),28,new Color(1,.8f,.35f)));
            Set(dialogue,"_instruction",Label("Instruction",new Vector2(.04f,.22f),new Vector2(.96f,.78f),38,Color.white));
            Set(dialogue,"_hint",Label("Hint",new Vector2(.04f,.04f),new Vector2(.96f,.24f),24,new Color(.75f,.8f,.85f)));
        }
        Set(director,"_dialogue",dialogue);
        Button ButtonNamed(string name) => All<Button>(scene).Single(b=>b.name==name && b.transform.root.name=="# MainUI");
        var summon = (Button)uiSo.FindProperty("summonButton").objectReferenceValue;
        var upgrade = ButtonNamed("EnchantButton");
        var chief = ButtonNamed("Skill_Button");
        var inventory = ButtonNamed("TotemButton");
        Set(director,"_settings",settings); Set(director,"_overlay",overlay);
        Set(director,"_plan",plan);
        var bindings = new System.Collections.Generic.List<TutorialTargetBinding>();
        foreach (var pair in new[] { ("Summon",summon), ("Upgrade",upgrade), ("ChiefSkill",chief), ("TotemInventory",inventory) })
        {
            var binding = pair.Item2.GetComponent<TutorialTargetBinding>() ?? pair.Item2.gameObject.AddComponent<TutorialTargetBinding>();
            binding.Key = pair.Item1; binding.Button = pair.Item2; binding.Highlight = (RectTransform)pair.Item2.transform;
            bindings.Add(binding);
        }
        var bindingSo = new SerializedObject(director); var targets = bindingSo.FindProperty("_targets");
        // Keep designer-added bindings when rerunning scene setup.
        var allBindings = All<TutorialTargetBinding>(scene);
        targets.arraySize = allBindings.Length;
        for (int i=0;i<allBindings.Length;i++) targets.GetArrayElementAtIndex(i).objectReferenceValue = allBindings[i];
        bindingSo.ApplyModifiedPropertiesWithoutUndo();
        Set(director,"_summonButton",summon); Set(director,"_upgradeButton",upgrade); Set(director,"_chiefButton",chief);
        Set(director,"_inventoryButton",inventory); Set(director,"_levelUpUI",levelUp); Set(director,"_rewardUI",reward);
        Set(director,"_inventoryUI",One<TotemInventoryUI>(scene));
        Set(director,"_upgradeGroup",Group(upgrade.transform.parent.gameObject));
        Set(director,"_chiefGroup",Group(chief.gameObject));
        Set(director,"_inventoryGroup",Group(inventory.transform.parent.gameObject));
        var panel = One<UI_UpgradePanel>(scene);
        Set(director,"_upgradePanel",panel);
        Set(director,"_upgradeSlots",new SerializedObject(panel).FindProperty("itemContainer").objectReferenceValue);
        var directorSo = new SerializedObject(director);
        var hud = directorSo.FindProperty("_bossHud"); hud.arraySize = 3;
        hud.GetArrayElementAtIndex(0).objectReferenceValue = Group(((Component)uiSo.FindProperty("_bossHpBar").objectReferenceValue).transform.parent.gameObject);
        hud.GetArrayElementAtIndex(1).objectReferenceValue = Group(((Component)uiSo.FindProperty("timerText").objectReferenceValue).transform.parent.gameObject);
        hud.GetArrayElementAtIndex(2).objectReferenceValue = Group(One<UI_WaveText>(scene).gameObject);
        directorSo.ApplyModifiedPropertiesWithoutUndo();
        var scope = One<InGameLifetimeScope>(scene);
        var scopeSo = new SerializedObject(scope); var inject = scopeSo.FindProperty("autoInjectGameObjects");
        bool present = false;
        for(int i=0;i<inject.arraySize;i++) if(inject.GetArrayElementAtIndex(i).objectReferenceValue==director.gameObject) present=true;
        if(!present) { int n=inject.arraySize++; inject.GetArrayElementAtIndex(n).objectReferenceValue=director.gameObject; }
        scopeSo.ApplyModifiedPropertiesWithoutUndo();
        // Lab/debug controls copied into this scene are not part of the learning flow.
        foreach(var root in scene.GetRootGameObjects())
            if(root.name=="Debug Debug" || root.name=="DamageStyleTestCanvas") root.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = director.gameObject;
        Debug.Log("TutorialScene: 13 lessons wired; original gacha sequence preserved.");
    }

    private static T CopyAsset<T>(T source,string name) where T:ScriptableObject
    {
        string path=DataPath+"/"+name+".asset";
        var result=AssetDatabase.LoadAssetAtPath<T>(path); if(result!=null)return result;
        result=Object.Instantiate(source);result.name=name;AssetDatabase.CreateAsset(result,path);return result;
    }
    private static T[] All<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
    private static T One<T>(Scene scene) where T:Component => All<T>(scene).Single();
    private static CanvasGroup Group(GameObject go)
    {
        var group = go.GetComponent<CanvasGroup>();
        if (group == null) group = Undo.AddComponent<CanvasGroup>(go);
        EditorUtility.SetDirty(go);
        EditorUtility.SetDirty(group);
        if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.RecordPrefabInstancePropertyModifications(group);
        return group;
    }
    private static void Set(Object target,string property,Object value)
    {
        var so=new SerializedObject(target);so.FindProperty(property).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();
    }
}
