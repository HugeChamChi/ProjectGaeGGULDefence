using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

/// <summary>알팡의 그리드 없는 선택/충전/발동/기존 버튼 연결을 검증한다.</summary>
public static class AlphanActiveSkillChecks
{
    private static Scene _scene;
    private static readonly List<UnityEngine.Object> _assets=new();
    private static int _passed;
    /// <summary>실제 씬이나 에셋을 저장하지 않는 임시 씬 검사.</summary>
    [MenuItem("Tools/Selections/Run Alphan Active Skill Checks")]
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        _scene=EditorSceneManager.NewPreviewScene();_passed=0;
        float oldScale=Time.timeScale;Time.timeScale=1;
        var previousParty=GlobalData.SelectedParty;
        AlphanActiveSkill skill=null;
        try
        {
            var spawner=Component<ChieftainSpawner>();var manager=Component<LevelUpManager>();
            var drones=Component<AlphanSkillCheckDroneManager>();var game=Component<GameManager>();
            Set(game,"<CurrentState>k__BackingField",GameManager.GameState.Playing);
            var data=Asset<AlphanSkillData>();data.CooldownSeconds=14;data.DamagePerDrone=80;data.SoundAddress="";
            skill=new AlphanActiveSkill(spawner,new DroneManager[]{drones},manager,game,null);
            Set(skill,"_cutsceneSearched",true);skill.Initialize();
            Check(!skill.IsAvailable && spawner.ActiveSkill==null,"No selection stays unavailable");
            var selected=Asset<UnitData>();selected.AlphanSkill=data;
            typeof(ChieftainSpawner).GetMethod("SpawnChieftainByUnitData",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(spawner,new object[]{selected});
            Check(spawner.ChieftainUnit==null && spawner.ActiveSkill==skill,"Selected Alphan has no grid unit or factory dependency");
            Check(skill.CooldownProgress==0 && skill.CooldownRemaining==14,"First use starts empty");
            skill.Advance(13);Check(!skill.CanActivate && skill.CooldownRemaining==1,"First thirteen seconds locked");
            skill.Advance(1);Check(skill.CooldownProgress==1 && !skill.TryActivate() && drones.CastCount==0,"Zero drones cannot cast");
            Check(skill.CooldownRemaining==0,"Zero drones consume no cooldown");
            var drone=Component<DroneUnit>();drones.RegisterDrone(drone);
            Check(skill.CanActivate,"Drone arrival unlocks already charged skill");
            var view=Component<UI_ChiefSkillButtonView>();var button=Component<UnityEngine.UI.Button>();
            var slider=Component<UnityEngine.UI.Slider>();var label=Component<TMPro.TextMeshProUGUI>();
            Set(view,"btn_Skill",button);Set(view,"slider_Cooldown",slider);Set(view,"txt_Cooldown",label);
            var presenter=new UI_ChiefSkillPresenter(view);presenter.SetActiveSkill(skill);
            Check(button.interactable && slider.value==0,"Existing UI handles no-grid skill");
            presenter.ExecuteSkill();
            Check(drones.CastCount==1 && drones.EmergencyCount==1 && drones.Damage==80,"Manual cast runs rally and emergency hook once");
            Check(skill.CooldownRemaining==14 && !skill.TryActivate(),"Successful cast consumes cooldown and double click blocked");
            presenter.OnUpdate(0);Check(!button.interactable && label.text.Contains("14.0"),"UI shows cooldown");
            Time.timeScale=0;skill.Advance(100);Check(skill.CooldownRemaining==14,"Time pause does not charge");Time.timeScale=1;
            Set(game,"<CurrentState>k__BackingField",GameManager.GameState.LevelUp);
            skill.Advance(100);Check(skill.CooldownRemaining==14 && !skill.TryActivate(),"Level-up state freezes and prevents activation");
            Set(game,"<CurrentState>k__BackingField",GameManager.GameState.Playing);
            var cooldown=Asset<LevelUpData>();cooldown.chooseId=9110;DroneSelectionPresets.Configure(cooldown,DroneSelectionKind.AlphanCooldown);manager.ApplyEffect(cooldown);
            Check(Mathf.Approximately(skill.CooldownSeconds,11.2f),"Twenty percent cooldown applies to independent SO");
            var protocol=Asset<LevelUpData>();protocol.chooseId=9111;DroneSelectionPresets.Configure(protocol,DroneSelectionKind.AlphanDoubleShot);manager.ApplyEffect(protocol);
            skill.Advance(11.2f);Check(skill.TryActivate() && drones.Protocol?.Count==2 && drones.Protocol.Value==0.6f,"Protocol passed to rally");
            manager.RemoveEffect(cooldown);Check(skill.CooldownSeconds==14,"Cooldown removal restored");
            skill.Advance(14);drones.UnregisterDrone(drone);presenter.OnUpdate(0);
            Check(!button.interactable && label.text=="" && !skill.TryActivate() && skill.CooldownRemaining==0,"Ready but no drones stays disabled without spending charge");
            drones.RegisterDrone(drone);drones.HoldCast=true;Check(skill.TryActivate(),"Long cast started");
            skill.Advance(14);Check(!skill.CanActivate,"Casting blocks reentry even after charging");
            var castToken=drones.LastToken;spawner.SelectAlphanSkill(null);
            Check(castToken.IsCancellationRequested && !skill.IsAvailable && spawner.ActiveSkill==null,"Changing selection cancels cast and unbinds");
            Check(spawner.ChieftainUnit==null,"No hidden unit was created");
            using(var noDrones=new AlphanActiveSkill(null,Array.Empty<DroneManager>(),manager,game,null))
            {noDrones.Configure(data,null);Check(!noDrones.IsAvailable,"Non-drone scene dependency is optional");}
            using(var builderContainer=BuildContainer(spawner,drones,manager,game))
                Check(builderContainer.Resolve<AlphanActiveSkill>()!=null,"VContainer constructor registration resolves");
            using(var builderContainer=BuildContainer(spawner,null,manager,game))
                Check(builderContainer.Resolve<AlphanActiveSkill>()!=null,"VContainer accepts non-drone scene without manager registration");
            var party=AssetDatabase.LoadAssetAtPath<PartyDataSO>("Assets/WorkSpace/HSD/Data/Party/Dron_Party.asset");
            Check(party!=null && party.chieftainData.AlphanSkill!=null && party.chieftainData.AlphanSkill.CooldownSeconds==14,"Real drone party points to fourteen-second skill SO");
            GlobalData.SelectedParty=party;
            typeof(ChieftainSpawner).GetMethod("HandleGameStart",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(spawner,null);
            Check(spawner.ChieftainUnit==null && spawner.ActiveSkill==skill && skill.CooldownRemaining==14,"Real party start bypasses grid and starts charging");
            var factory=Component<UnitFactory>();
            Check(factory.CreateUnitFromData(party.chieftainData)==null,"Direct factory path rejects active-skill selector");
            var chief=Component<Drone_Alphan>();chief.unitData=Asset<UnitData>();chief.unitData.skillCooldown.normal=14;
            chief.Init(new UnitDependencies());var cell=Component<GridCell>();Set(cell,"<Model>k__BackingField",new GridCellModel());
            Set(chief,"<currentCell>k__BackingField",cell);chief.gameObject.SetActive(true);chief.Combat.SkillTimer=14;
            var legacy=new UnitChiefActiveSkill(chief);presenter.SetActiveSkill(legacy);
            Check(legacy.CanActivate && button.interactable,"Existing grid ChiefUnit still displays and can activate");
            skill.Dispose();Check(!skill.CanActivate,"Disposed scene skill unavailable");
            Debug.Log($"[AlphanActiveSkillChecks] PASS {_passed} assertions.");
        }
        finally
        {
            GlobalData.SelectedParty=previousParty;
            skill?.Dispose();EditorSceneManager.ClosePreviewScene(_scene);
            foreach(var asset in _assets)UnityEngine.Object.DestroyImmediate(asset);_assets.Clear();Time.timeScale=oldScale;
        }
    }
    private static VContainer.IObjectResolver BuildContainer(ChieftainSpawner spawner,DroneManager drones,LevelUpManager manager,GameManager game)
    {
        var builder=new VContainer.ContainerBuilder();
        VContainer.ContainerBuilderExtensions.RegisterInstance(builder,spawner);
        if(drones!=null)VContainer.ContainerBuilderExtensions.RegisterInstance(builder,drones);
        VContainer.ContainerBuilderExtensions.RegisterInstance(builder,manager);
        VContainer.ContainerBuilderExtensions.RegisterInstance(builder,game);
        VContainer.ContainerBuilderExtensions.RegisterInstance(builder,Component<AudioManager>());
        VContainer.ContainerBuilderExtensions.Register<AlphanActiveSkill>(builder,VContainer.Lifetime.Scoped);
        return builder.Build();
    }
    private static T Component<T>() where T:Component
    {var go=new GameObject(typeof(T).Name);go.SetActive(false);SceneManager.MoveGameObjectToScene(go,_scene);return go.AddComponent<T>();}
    private static T Asset<T>() where T:ScriptableObject
    {var asset=ScriptableObject.CreateInstance<T>();_assets.Add(asset);return asset;}
    private static void Set(object target,string name,object value)
    {for(var type=target.GetType();type!=null;type=type.BaseType){var f=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(f!=null){f.SetValue(target,value);return;}}throw new MissingFieldException(name);}
    private static void Check(bool ok,string message)
    {if(!ok)throw new InvalidOperationException(message);_passed++;}
}
