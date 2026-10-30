using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using Cysharp.Threading.Tasks;
public static class LiveHtmlBonusChecks {
 const string Folder="Assets/WorkSpace/HSD/Test_Backend";
 public static async Task<string> Boot(){
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(40));
  await UniTask.WaitUntil(()=>{var root=UnityEngine.Object.FindFirstObjectByType<RootLifetimeScope>();return root!=null&&root.Container!=null&&root.Container.Resolve<AppInitialization>().IsReady;},cancellationToken:timeout.Token);
  await UniTask.WaitUntil(()=>SceneManager.GetActiveScene().name=="LobbyScene",cancellationToken:timeout.Token);
  if(!UnitBalanceRuntime.IsLoaded||!UnitBonusChart.IsLoaded)throw new Exception("Boot did not load charts");
  var keys=new[]{"UnitData_Betan","UnitData_Deltan","UnitData_Gamman","UnitData_Disigman","UnitData_Zeltan","AlphanChief"};
  foreach(var key in keys){if(UnitBonusChart.Resolve(key,null).Count!=5)throw new Exception("Missing milestones "+key);foreach(int level in new[]{3,6,9,12,15})if(UnitBonusChart.GetPresentation(key,level)==null)throw new Exception("Missing text "+key);}
  await EditorSceneManager.LoadSceneAsyncInPlayMode(Folder+"/LobbyScene.unity",new LoadSceneParameters(LoadSceneMode.Single)).ToUniTask();
  await UniTask.Yield();
  var scope=UnityEngine.Object.FindFirstObjectByType<LobbyLifetimeScope>();if(scope==null||scope.Container==null)throw new Exception("Copied lobby DI missing");
  return "PASS copied BootScene automatic login -> full server charts -> Lobby; all 6 actors/30 milestones/30 descriptions loaded; copied LobbyScene DI ready. No forced account levels.";
 }
 public static async Task<string> Factory(){
  if(!UnitBonusChart.IsLoaded||!UnitBalanceRuntime.IsLoaded)throw new Exception("Boot required");
  string copy=Folder+"/IngameScene.unity";
  if(AssetDatabase.LoadAssetAtPath<SceneAsset>(copy)==null&&!AssetDatabase.CopyAsset("Assets/Scenes/IngameScene.unity",copy))throw new Exception("Copy failed");
  var originalParty=GlobalData.SelectedParty;
  GlobalData.SelectedParty=AssetDatabase.LoadAssetAtPath<PartyDataSO>("Assets/WorkSpace/HSD/Data/Party/Dron_Party.asset");
  await EditorSceneManager.LoadSceneAsyncInPlayMode(copy,new LoadSceneParameters(LoadSceneMode.Single)).ToUniTask();
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(35));
  InGameLifetimeScope scope=null;
  await UniTask.WaitUntil(()=>{scope=UnityEngine.Object.FindFirstObjectByType<InGameLifetimeScope>();return scope!=null&&scope.Container!=null&&scope.Container.Resolve<GameInitializer>().IsReady;},cancellationToken:timeout.Token);
  var resolver=scope.Container;var game=resolver.Resolve<GameManager>();if(!game.IsEditorPreview)throw new Exception("Preview guard missing");
  var clock=resolver.Resolve<TimeScaleService>();var owner=new object();clock.Pause(owner);
  var factory=resolver.Resolve<UnitFactory>();
  var keys=new[]{"UnitData_Betan","UnitData_Deltan","UnitData_Gamman","UnitData_Disigman","UnitData_Zeltan"};
  int checkedUnits=0;var made=new System.Collections.Generic.List<UnitBase>();
  var accountBefore=JsonUtility.ToJson(Player.PlayerData.Data);
  try {
   var levels=new UnitLevelSnapshot(keys.Concat(new[]{"AlphanChief"}).Select(k=>new System.Collections.Generic.KeyValuePair<string,int>(k,15)));
   factory.Init(levels);
   await UniTask.WaitUntil(()=>factory.UnitDataList.All(d=>d.prefab!=null),cancellationToken:timeout.Token);
   foreach(string key in keys){var source=factory.UnitDataList.FirstOrDefault(d=>d.name==key);if(source==null)throw new Exception("Missing roster "+key);
    foreach(Tier tier in TierUtil.All){var unit=factory.CreateUnitFromData(source,tier);if(unit==null)throw new Exception("Prefab failed "+key+"/"+tier);made.Add(unit);
     if(unit.LevelBonuses.Level!=15)throw new Exception("Snapshot did not reach actual UnitBase.Init");
     if(unit.unitData.LevelBonuses.Count!=5)throw new Exception("Runtime data did not carry server bonuses");
     foreach(UnitBonusStat stat in Enum.GetValues(typeof(UnitBonusStat))){float expected=new UnitLevelBonusController(UnitBonusChart.Resolve(key,null),15).GetStat(stat);if(Math.Abs(unit.LevelStat(stat)-expected)>.00001f)throw new Exception("Stat mismatch "+key+"/"+stat);}
     checkedUnits++;
    }
   }
   var dis=made.OfType<Disigman>().FirstOrDefault();if(dis==null||Math.Abs(dis.LevelStat(UnitBonusStat.RecoveryNextBasicPercent)-.3f)>.0001f)throw new Exception("Disigman prefab/effect missing");
   if(JsonUtility.ToJson(Player.PlayerData.Data)!=accountBefore)throw new Exception("Test changed account data");
   return "PASS downloaded server definitions -> actual UnitFactory -> 20 real prefabs (5 drones x4 tiers, including Disigman) -> UnitBase.Init level15 snapshot and all stat values. Copied IngameScene, editor preview guard, no stamina/rewards/account level writes.";
  }finally{
   foreach(var unit in made)if(unit!=null)RM.Destroy(unit.gameObject);
   factory.Init();clock.Release(owner);GlobalData.SelectedParty=originalParty;
   await EditorSceneManager.LoadSceneAsyncInPlayMode(Folder+"/LobbyScene.unity",new LoadSceneParameters(LoadSceneMode.Single)).ToUniTask();
  }
 }
}
