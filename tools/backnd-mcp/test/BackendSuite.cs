using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using Cysharp.Threading.Tasks;
public static class BackendSuite
{
 const string Folder="Assets/WorkSpace/HSD/Test_Backend";
 public static string Setup() {
  if(!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/WorkSpace/HSD","Test_Backend");
  if(AssetDatabase.LoadAssetAtPath<SceneAsset>(Folder+"/BootScene.unity")==null) {
   var error=AssetDatabase.MoveAsset("Assets/Tests/BootSceneCopy/BootScene.unity",Folder+"/BootScene.unity");
   if(!string.IsNullOrEmpty(error)) throw new Exception(error);
  }
  if(AssetDatabase.LoadAssetAtPath<SceneAsset>(Folder+"/LobbyScene.unity")==null && !AssetDatabase.CopyAsset("Assets/Scenes/LobbyScene.unity",Folder+"/LobbyScene.unity")) throw new Exception("Lobby copy failed");
  EditorSceneManager.OpenScene(Folder+"/BootScene.unity");
  return Folder;
 }
 public static async Task<string> Run() {
  if(!BackEnd.Backend.IsLogin) throw new Exception("Existing test account login required");
  var root=UnityEngine.Object.FindFirstObjectByType<RootLifetimeScope>();
  var init=root.Container.Resolve<AppInitialization>();
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));
  await init.ReloadAuthenticatedPlayerAsync(timeout.Token);
  if(!init.IsReady || !UnitBalanceRuntime.IsLoaded || !GGD.Events.EventDataBridge.IsConfiguredFor(BackEnd.Backend.UserInDate)) throw new Exception("Initialization incomplete");
  await EditorSceneManager.LoadSceneAsyncInPlayMode(Folder+"/LobbyScene.unity",new LoadSceneParameters(LoadSceneMode.Single)).ToUniTask();
  root.Container.Resolve<AppLoadingScreen>().Hide();
  await UniTask.Delay(2000);
  var scope=UnityEngine.Object.FindFirstObjectByType<LobbyLifetimeScope>();
  if(scope==null || scope.Container==null) throw new Exception("Lobby injection missing");
  return "PASS existing test account initialization, remote unit/mission/research preparation, copied LobbyScene loaded and DI ready";
 }
}
