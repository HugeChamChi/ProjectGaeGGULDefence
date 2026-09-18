using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>드론 선택지의 획득/해제, 주기, 대상 분리 및 설명 표시를 임시 씬에서 검사한다.</summary>
public static class DroneSelectionChecks
{
    private static Scene _scene;
    private static readonly List<UnityEngine.Object> _assets = new();
    private static int _passed;
    /// <summary>현재 씬/프리팹/SO를 저장하지 않는 행동 검사.</summary>
    [MenuItem("Tools/Selections/Run Drone Selection Checks")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var random=UnityEngine.Random.state;
        _scene=EditorSceneManager.NewPreviewScene(); _passed=0;
        try
        {
            var manager=Component<LevelUpManager>();
            var cards=new LevelUpData[12];
            for(int i=1;i<=11;i++)
            {
                cards[i]=Asset<LevelUpData>(); cards[i].chooseId=9100+i;
                DroneSelectionPresets.Configure(cards[i],(DroneSelectionKind)i);
                Check(cards[i].droneEffect.Kind==(DroneSelectionKind)i,"Preset "+i);
            }
            string preview=manager.GetChoiceDescription(cards[8]);
            Check(preview==manager.GetChoiceDescription(cards[8]),"Preview is stable");
            var ui=Component<LevelUpCardUI>(); var label=Component<TMPro.TextMeshProUGUI>();
            Set(ui,"descriptionText",label); ui.Setup(cards[8],null,preview);
            Check(label.text==preview && !preview.Contains("{value}"),"TMP resolved description");
            manager.ApplyEffect(cards[8]);
            float roll=manager.DroneSelections.Get(DroneSelectionKind.DeltanDamageTaken).Value;
            Check(roll>=0.01f && roll<=0.1f && preview.Contains(Mathf.RoundToInt(roll*100)+"%"),"Displayed value applied");
            Check(cards[8].droneEffect.Value==0.01f && cards[8].description.Contains("{value}"),"SO untouched");
            manager.ApplyEffect(cards[8]);
            Check(manager.DroneSelections.Get(DroneSelectionKind.DeltanDamageTaken).Value==roll,"Duplicate apply does not reroll");

            var drones=Component<DroneManager>(); Set(drones,"_levelUpManager",manager);
            var beta=Component<DroneSelectionCheckBetan>(); beta.unitData=Asset<UnitData>();
            beta.unitData.attackSpeed.normal=1; beta.Init(new UnitDependencies { LevelUpManager=manager });
            var cell=Component<GridCell>(); Set(cell,"<Model>k__BackingField",new GridCellModel());
            Set(beta,"<currentCell>k__BackingField",cell); beta.gameObject.SetActive(true);
            for(int i=0;i<12;i++)
            {
                var drone=Component<DroneUnit>(); Set(drone,"_owner",beta); drone.gameObject.SetActive(true); drones.RegisterDrone(drone);
            }
            drones.TickSelections(20); Check(beta.Spawned==0,"No selection no extra bombs");
            manager.ApplyEffect(cards[1]); drones.TickSelections(9); Check(beta.Spawned==0,"Normal waits");
            drones.TickSelections(1); Check(beta.Spawned==1,"Normal one bomb at ten seconds");
            manager.ApplyEffect(cards[3]); drones.TickSelections(20); Check(beta.Spawned==13,"Normal plus epic ten cap");
            manager.RemoveEffect(cards[1]);manager.RemoveEffect(cards[3]); drones.TickSelections(60);
            Check(beta.Spawned==13,"Removed periodic effects stop");
            var ownDrone=drones.Drones[0];float interval=ownDrone.AttackInterval;
            manager.ApplyEffect(cards[2]); Check(Mathf.Approximately(ownDrone.AttackInterval,interval/1.2f),"Betan speed plus twenty percent");
            manager.RemoveEffect(cards[2]);Check(Mathf.Approximately(ownDrone.AttackInterval,interval),"Speed restored");

            var currency=Component<CurrencyManager>(); var zelta=Component<Drone_Zeltan>(); zelta.unitData=Asset<UnitData>();
            zelta.unitData.foodProduction.normal=4;zelta.Init(new UnitDependencies { LevelUpManager=manager,CurrencyManager=currency });
            manager.ApplyEffect(cards[6]); var resource=zelta.GetComponent<UnitResourceComponent>();
            resource.TickFoodProduction(2);Check(currency.Currency==0,"Air fryer holds two seconds");
            resource.TickFoodProduction(1);Check(Mathf.Approximately(currency.Currency,14.4f),"Air fryer pays three seconds at 120 percent");
            Check(Mathf.Approximately(zelta.CurrentFoodProductionPerSecond,4.8f),"Food UI remains per second");
            manager.RemoveEffect(cards[6]);resource.TickFoodProduction(1);Check(Mathf.Approximately(currency.Currency,18.4f),"Food revert");
            var delta=Component<Drone_Deltan>();delta.unitData=Asset<UnitData>();delta.unitData.skillCooldown.normal=20;
            delta.Init(new UnitDependencies { LevelUpManager=manager });manager.ApplyEffect(cards[9]);
            Check(Mathf.Approximately(delta.GetCurrentSkillInterval(),18),"Deltan cooldown");
            var alpha=Component<Drone_Alphan>();alpha.unitData=Asset<UnitData>();alpha.unitData.skillCooldown.normal=20;
            alpha.Init(new UnitDependencies { LevelUpManager=manager });manager.ApplyEffect(cards[10]);
            Check(Mathf.Approximately(alpha.GetCurrentSkillInterval(),16) && !alpha.CanAutoSkill,"Alphan manual cooldown");

            var gamma=Component<Drone_Gamman>();gamma.unitData=Asset<UnitData>();gamma.Init(new UnitDependencies {LevelUpManager=manager});
            Set(gamma,"_droneManager",drones);manager.ApplyEffect(cards[4]);
            gamma.ApplyDroneBuff();Check(Mathf.Approximately(drones.DroneAtkMultiplier,1.036f),"Gamman frequency twelve drones");
            for(int i=0;i<20;i++) drones.RegisterDrone(Component<DroneUnit>());
            gamma.ApplyDroneBuff();Check(Mathf.Approximately(drones.DroneAtkMultiplier,1.09f),"Frequency nine percent cap");
            var gammaDrone=Component<DroneUnit>();Set(gammaDrone,"_owner",gamma);drones.RegisterDrone(gammaDrone);
            Set(gamma,"<currentCell>k__BackingField",cell);gamma.gameObject.SetActive(true);
            Set(drones,"_buffEndTime",-1f);manager.ApplyEffect(cards[5]);drones.ApplyEmergencyBuffs();
            Check(Mathf.Approximately(drones.DroneAtkMultiplier,1.09f),"Emergency applies placed Gamman buff");

            var debuffs=new DebuffController(_=>{},()=>true);
            var definition=new DebuffDefinition(1003,"test",DebuffKind.DamageTakenIncrease,"test",5,1,damageMultiplier:1.2m);
            debuffs.Apply(definition,new DebuffApplyContext(1,1,0,(decimal)roll,0.1));
            Check(debuffs.DamageTakenMultiplier==1.2m+(decimal)roll && Math.Abs(debuffs.DefenseReduction-0.1)<1e-6,"Debuff additive bonuses");
            debuffs.Advance(5);Check(debuffs.DamageTakenMultiplier==1 && debuffs.DefenseReduction==0,"Both bonuses expire");
            var boss=Component<BossNormal>();boss.Init(100000);Set(boss,"_defense",500d);Set(boss,"_defenseScale",500d);
            boss.Debuffs.Apply(definition,new DebuffApplyContext(1,1,0,0.05m,0.1));
            long finalDamage=boss.CalculateFinalDamageUnits(100);
            long expected=DamageCalculator.Calculate(100,450,500,1,1.25m,long.MaxValue);
            Check(finalDamage==expected && boss.CurrentHp==100000,"Final damage preview applies defense reduction once without HP mutation");
            long shot=(long)Math.Round(finalDamage*(decimal)cards[11].droneEffect.Value,MidpointRounding.AwayFromZero);
            boss.ApplyRecordedDamage(shot);boss.ApplyRecordedDamage(shot);
            Check(boss.CurrentHp==100000-(decimal)(shot*2)/CombatHealth.Scale,"Two final sixty percent hits bypass recalculation");
            Debug.Log($"[DroneSelectionChecks] PASS {_passed} assertions.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(_scene);
            foreach(var asset in _assets) UnityEngine.Object.DestroyImmediate(asset);
            _assets.Clear();UnityEngine.Random.state=random;
        }
    }
    private static T Component<T>() where T:Component
    {
        var go=new GameObject(typeof(T).Name);go.SetActive(false);SceneManager.MoveGameObjectToScene(go,_scene);return go.AddComponent<T>();
    }
    private static T Asset<T>() where T:ScriptableObject
    {var asset=ScriptableObject.CreateInstance<T>();_assets.Add(asset);return asset;}
    private static void Set(object target,string name,object value)
    {
        for(var type=target.GetType();type!=null;type=type.BaseType)
        {var field=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(field!=null){field.SetValue(target,value);return;}}
        throw new MissingFieldException(name);
    }
    private static void Check(bool ok,string message)
    {if(!ok)throw new InvalidOperationException(message);_passed++;}
}
