using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using GaeGGUL.DeckUpgrade;
using Object=UnityEngine.Object;

public static class DeckOwnershipTestChecks
{
 const string Out="outputs/deck-test-ownership-20261107";
 static PlayerData Clone(PlayerData d)=>JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(d));
 sealed class Backend : BackendGameData
 {
  public PlayerData Server=new PlayerData {Gold=1234,Diamond=56,SaveId="initial"};
  public string CharacterJson="";public bool Fail;public int Transactions;
  public override void GameDataGet(Action<PlayerData> onComplete=null)=>onComplete?.Invoke(Clone(Server));
  public override void GameDataUpdate(PlayerData data,Action onComplete=null,Action<bool> onResult=null){Server=Clone(data);onResult?.Invoke(true);onComplete?.Invoke();}
  public override UniTask<bool> CommitCharacterUpgradeAsync(PlayerData next,string expectedSaveId,AccountJsonStore.Row row,string json)
  {
   Transactions++;if(Fail||Server.SaveId!=expectedSaveId)return UniTask.FromResult(false);
   Server=Clone(next);CharacterJson=json;return UniTask.FromResult(true);
  }
 }
 sealed class CharacterStore : AccountJsonStore
 {
  public Backend Backend;
  public override UniTask<Row> ReadAsync(string table,string owner,System.Threading.CancellationToken token)=>UniTask.FromResult(new Row{InDate="fixture",Json=Backend.CharacterJson});
 }
 public static async Task<string> Service()
 {
  Directory.CreateDirectory(Out);var log=new System.Collections.Generic.List<string>();
  void Check(bool ok,string label){if(!ok)throw new Exception(label);log.Add("PASS "+label);}
  var backend=new Backend();backend.CharacterJson=JsonUtility.ToJson(new AccountCharacterData{SaveId="characters",LegacyOwnershipImported=true,Characters=new System.Collections.Generic.List<AccountCharacterData.Entry>{new AccountCharacterData.Entry{Id="unit:1",Count=1,Level=15},new AccountCharacterData.Entry{Id="chief:1",Count=1,Level=9}}});
  backend.Server.RewardInventory.Owned.Add(new RewardOwnedEntry{Type=RewardType.Unit,TargetId=99,Count=1});
  var controller=new PlayerDataController(()=>"ownership-service",()=>1000,"OwnershipService."+Guid.NewGuid()+".");controller.Inject(backend);await controller.InitalizeAsync();
  var playerProperty=typeof(Player).GetProperty("PlayerData");var originalPlayer=Player.PlayerData;var document=Player.Characters;
  var names=new[]{"_store","_ownerProvider","_owner","_confirmed","_attemptSaveId","<Data>k__BackingField","<IsDirty>k__BackingField","<IsLocked>k__BackingField"};
  var fields=names.Select(n=>document.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic)).ToArray();var original=fields.Select(f=>f.GetValue(document)).ToArray();
  void Set(string name,object value)=>fields[Array.IndexOf(names,name)].SetValue(document,value);
  DeckUpgradeService service=null;
  try
  {
   playerProperty.SetValue(null,controller);Set("_store",new CharacterStore{Backend=backend});Set("_ownerProvider",(Func<string>)(()=>"ownership-service"));Set("_owner","ownership-service");Set("_confirmed","characters");Set("_attemptSaveId","");Set("<IsDirty>k__BackingField",false);Set("<IsLocked>k__BackingField",false);Set("<Data>k__BackingField",JsonUtility.FromJson<AccountCharacterData>(backend.CharacterJson));
   service=new DeckUpgradeService(new AccountDeckUpgradeStore());string[] ids={"unit:1","unit:2","chief:1"};
   Check(await service.SetTestCharacterOwnershipAsync(ids,true),"actual service unlocks all requested units and chief");
   Check(ids.All(id=>document.Data.Characters.Any(e=>e.Id==id&&e.Count==1)),"character account owns each roster entry");
   Check(service.GetLevel("unit:1")==15&&service.GetLevel("chief:1")==9,"unlock preserves existing upgrade levels");
   Check(await service.SetTestCharacterOwnershipAsync(ids,false),"actual service deletes all ownership");
   Check(document.Data.Characters.All(e=>e.Count==0)&&controller.Data.RewardInventory.Owned.Where(e=>e.Type==RewardType.Unit).All(e=>e.Count==0),"both character and reward ownership cleared");
   Check(document.Data.Characters.Find(e=>e.Id=="unit:1").Level==15&&document.Data.LegacyOwnershipImported,"delete preserves levels and prevents legacy reimport");
   Check(await service.SetTestCharacterOwnershipAsync(ids,true),"deleted roster can be unlocked again");
   Check(document.Data.Characters.Count==3&&service.GetLevel("unit:1")==15,"re-unlock restores ownership without duplicate records or level reset");
   Check(controller.Data.Gold==1234&&controller.Data.Diamond==56,"service preserves account money");
   Check(!service.IsBusy&&!document.IsLocked,"service releases busy and document lock");
   File.WriteAllLines(Out+"/service.txt",log);return "PASS "+log.Count+" actual service checks with isolated transports";
  }
  finally{service?.Dispose();for(int i=0;i<fields.Length;i++)fields[i].SetValue(document,original[i]);playerProperty.SetValue(null,originalPlayer);controller.Dispose();}
 }
 public static async Task<string> Run()
 {
  Directory.CreateDirectory(Out);var report=new System.Collections.Generic.List<string>();
  void Check(bool ok,string label){if(!ok)throw new Exception(label);report.Add("PASS "+label);}
  var backend=new Backend();backend.Server.RewardInventory.Owned.Add(new RewardOwnedEntry{Type=RewardType.Currency,TargetId=4,Count=20});
  backend.Server.RewardInventory.Owned.Add(new RewardOwnedEntry{Type=RewardType.Unit,TargetId=99,Count=1});
  var c=new PlayerDataController(()=>"ownership-test",()=>1000,"OwnershipTest."+Guid.NewGuid()+".");c.Inject(backend);
  try
  {
   await c.InitalizeAsync();
   var row=new AccountJsonStore.Row{InDate="fixture",Json="{}"};
   Check(await c.TryCommitTestCharacterOwnershipAsync(row,"unlocked",new[]{1,2,3,4,5},true),"unlock transaction succeeds");
   Check(new[]{1,2,3,4,5}.All(id=>c.Data.RewardInventory.Count(RewardType.Unit,id)==1),"all supplied units unlocked");
   Check(c.Data.RewardInventory.Count(RewardType.Unit,99)==1,"unlock preserves other ownership");
   Check(await c.TryCommitTestCharacterOwnershipAsync(row,"unlocked",new[]{1,2,3,4,5},true),"repeat unlock accepted");
   Check(new[]{1,2,3,4,5}.All(id=>c.Data.RewardInventory.Count(RewardType.Unit,id)==1),"repeat unlock remains single ownership");
   Check(await c.TryCommitTestCharacterOwnershipAsync(row,"deleted",new[]{1,2,3,4,5},false),"delete transaction succeeds");
   Check(c.Data.RewardInventory.Owned.Where(e=>e.Type==RewardType.Unit).All(e=>e.Count==0),"delete clears all reward-inventory units including older holdings");
   Check(c.Data.Gold==1234&&c.Data.Diamond==56&&c.Data.RewardInventory.Count(RewardType.Currency,4)==20,"ownership operations preserve currencies and tokens");
   Check(backend.CharacterJson=="deleted"&&backend.Transactions==3,"character document and reward ownership use same transaction");
   await c.InitalizeAsync();Check(c.Data.RewardInventory.Count(RewardType.Unit,99)==0,"deleted ownership restored after reload");
   Check(await c.TryCommitCharacterUpgradeAsync(3,row,"level-up",DeckUpgradeCurrency.Token),"existing upgrade transaction still succeeds");
   Check(c.Data.RewardInventory.Count(RewardType.Currency,4)==17&&c.Data.Gold==1234,"upgrade still spends only requested token cost");
   backend.Fail=true;Check(!await c.TryCommitTestCharacterOwnershipAsync(row,"failed",new[]{1},true),"failed ownership transaction rejected");
   Check(c.Data.RewardInventory.Count(RewardType.Unit,1)==0&&backend.CharacterJson=="level-up","failure preserves prior local and server ownership");
   Check(c.HasSaveConflict,"uncertain write blocks further edits");
   File.WriteAllLines(Out+"/isolated.txt",report);return "PASS "+report.Count+" isolated ownership and upgrade checks";
  }
  finally{c.Dispose();}
 }
 public static string Prepare()
 {
  if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
  Directory.CreateDirectory(Out);SessionState.SetString("OwnershipTest.PlayStart",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
  EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/ShopTestTitle.unity");return "Test title prepared";
 }
 public static string Restore(){if(EditorApplication.isPlaying)throw new Exception("Stop first");EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("OwnershipTest.PlayStart",""));return "Previous start restored";}
 public static string State()=>SceneManager.GetActiveScene().name+" account="+Player.PlayerData.IsReady+" chars="+Player.Characters.IsReady;
 public static async Task<string> InspectUI()
 {
  var tabs=Object.FindFirstObjectByType<BottomPanelController>();tabs.SelectButton(1);await Task.Delay(600);
  var screen=Object.FindFirstObjectByType<DroneUpgradeConceptScreen>();var page=screen.transform.Find("DroneUpgradeCanvas/SafeArea/Page");
  var unlock=(RectTransform)page.Find("TestUnlockAll");var remove=(RectTransform)page.Find("TestDeleteAll");var token=(RectTransform)page.Find("TestTokenGrant");
  if(unlock==null||remove==null||token==null||unlock.anchoredPosition.y<=token.anchoredPosition.y||remove.anchoredPosition.y<=token.anchoredPosition.y)throw new Exception("Test controls missing or not above token control");
  if(!unlock.GetComponent<Button>().interactable||!remove.GetComponent<Button>().interactable)throw new Exception("Loaded account controls disabled");
  ScreenCapture.CaptureScreenshot(Out+"/current-ownership.png");await Task.Delay(150);
  var property=Player.Characters.GetType().GetProperty("Data");var original=Player.Characters.Data;
  var fixture=JsonUtility.FromJson<AccountCharacterData>(JsonUtility.ToJson(original));
  foreach(var unit in screen.units){string id=PlayerCharacterManager.UnitId(unit.characterId.Get(Tier.Normal));var e=fixture.Characters.Find(x=>x.Id==id);if(e==null){e=new AccountCharacterData.Entry{Id=id};fixture.Characters.Add(e);}e.Count=1;}
  string chief=PlayerCharacterManager.ChiefId(screen.chief.ChieftainId);var ce=fixture.Characters.Find(x=>x.Id==chief);if(ce==null){ce=new AccountCharacterData.Entry{Id=chief};fixture.Characters.Add(ce);}ce.Count=1;
  var refresh=typeof(DroneUpgradeConceptScreen).GetMethod("Refresh",BindingFlags.Instance|BindingFlags.NonPublic);
  try{property.SetValue(Player.Characters,fixture);refresh.Invoke(screen,null);Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Out+"/all-owned-layout.png");await Task.Delay(150);}
  finally{property.SetValue(Player.Characters,original);refresh.Invoke(screen,null);}
  return "PASS UI controls above token button; current/all-owned comparison captured using unsaved fixture; account restored. No actual test button invoked.";
 }
}
