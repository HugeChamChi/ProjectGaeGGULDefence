using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using LitJson;
public static class DevelopMergeChecks {
 static void Set(object obj,string key,object value){for(var type=obj.GetType();type!=null;type=type.BaseType){var field=type.GetField(key,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(field!=null){field.SetValue(obj,value);return;}}throw new Exception(key);}
 public static string Run(){
  var scene=EditorSceneManager.NewPreviewScene();var assets=new List<UnityEngine.Object>();int count=0;
  T Component<T>()where T:Component{var go=new GameObject(typeof(T).Name);go.SetActive(false);SceneManager.MoveGameObjectToScene(go,scene);return go.AddComponent<T>();}
  T Asset<T>()where T:ScriptableObject{var value=ScriptableObject.CreateInstance<T>();assets.Add(value);return value;}
  void Near(float value,float expected,string label){if(Math.Abs(value-expected)>.0001f)throw new Exception(label+": "+value+" != "+expected);count++;}
  try{
   var rows=JsonMapper.ToObject(File.ReadAllText("outputs/bonus-description-rows.json"));var definitions=UnitBonusChart.Parse(rows);var manager=Component<LevelUpManager>();var drones=Component<DroneManager>();
   var deps=new UnitDependencies{DroneEffects=manager.Effects,SelectionCombat=manager.Effects,SelectionChanges=manager.Effects,CombatSettings=manager.CombatConfiguration};
   void Configure(UnitBase unit,string key){Set(unit,"_deps",deps);Set(unit,"<LevelBonuses>k__BackingField",new UnitLevelBonusController(definitions[key],15));Set(unit,"_droneManager",drones);}
   var delta=Component<Drone_Deltan>();delta.unitData=Asset<UnitData>();delta.unitData.Hacking=Asset<DroneHackingData>();delta.unitData.Hacking.AttacksToCharge.normal=6;delta.unitData.Hacking.StacksProduced.normal=2;Configure(delta,"UnitData_Deltan");
   Near(delta.RequiredAttacks,5,"Level charge reduction");Near(delta.NextProduction,3,"Level production");var compression=Asset<LevelUpData>();compression.chooseId=9109;DroneSelectionPresets.Configure(compression,(DroneSelectionKind)9);manager.ApplyEffect(compression);
   Near(delta.RequiredAttacks,8,"Develop compression plus level reduction");Near(delta.NextProduction,6,"Develop compression doubles level production too");
   var gamma=Component<Drone_Gamman>();Configure(gamma,"UnitData_Gamman");for(int i=0;i<12;i++)drones.RegisterDrone(Component<DroneUnit>());
   var frequency=Asset<LevelUpData>();frequency.chooseId=9104;DroneSelectionPresets.Configure(frequency,(DroneSelectionKind)4);manager.ApplyEffect(frequency);Near(gamma.StackDamageMultiplier,1.2f*1.06f,"New frequency plus level stack damage");
   var zelta=Component<Drone_Zeltan>();zelta.unitData=Asset<UnitData>();zelta.unitData.foodProduction.normal=4;Set(zelta,"_deps",deps);Set(zelta,"<LevelBonuses>k__BackingField",new UnitLevelBonusController(definitions["UnitData_Zeltan"],15));
   var fryer=Asset<LevelUpData>();fryer.chooseId=9106;DroneSelectionPresets.Configure(fryer,(DroneSelectionKind)6);manager.ApplyEffect(fryer);Near(zelta.GetBaseFoodPerSecond(),4*1.15f*1.2f,"New production selection plus level amount");
   var upgrade=Component<UpgradeManager>();Set(zelta,"_upgradeManager",upgrade);Near(zelta.FoodPerDronePerSecond,zelta.AuthoredFoodPerDrone*1.2f,"Fleet coefficient stays independent of own production level bonus");
   return "PASS "+count+" combined Develop selection + level15 bonus assertions (compression charge/production, stack-only damage, independent own/fleet food).";
  }finally{EditorSceneManager.ClosePreviewScene(scene);foreach(var asset in assets)UnityEngine.Object.DestroyImmediate(asset);}
 }
}
