using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using GaeGGUL.DeckUpgrade;
using Object=UnityEngine.Object;
public static class UpgradeTokenUiChecks
{
 public static async Task<string> WaitAndRun()
 {
  var deadline=DateTime.UtcNow.AddSeconds(60);
  while(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="ShopTestLobby"&&DateTime.UtcNow<deadline)await Task.Delay(500);
  if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="ShopTestLobby")throw new Exception("Test lobby entry timed out");
  await Task.Delay(300);return await Run();
 }
 public static async Task<string> Run()
 {
  const string folder="outputs/upgrade-token-ui-20261107";Directory.CreateDirectory(folder);
  var report=new System.Collections.Generic.List<string>();
  void Check(bool ok,string label){if(!ok)throw new Exception(label);report.Add("PASS "+label);}
  var tabs=Object.FindFirstObjectByType<BottomPanelController>();tabs.SelectButton(1);await Task.Delay(500);
  var screen=Object.FindFirstObjectByType<DroneUpgradeConceptScreen>();
  var doc=Player.Characters;var original=doc.Data;var property=doc.GetType().GetProperty("Data");
  if(Player.PlayerData.IsDirty||doc.IsDirty)throw new Exception("Clean account snapshot required");
  var fixture=JsonUtility.FromJson<AccountCharacterData>(JsonUtility.ToJson(original));
  string id=PlayerCharacterManager.UnitId(screen.units[0].characterId.Get(Tier.Normal));
  var entry=fixture.Characters.Find(e=>e.Id==id);if(entry==null){entry=new AccountCharacterData.Entry{Id=id};fixture.Characters.Add(entry);}entry.Count=1;
  var items=Player.PlayerData.Data.RewardInventory.Owned;
  var token=items.Find(e=>e.Type==RewardType.Currency&&e.TargetId==4);bool added=token==null;int originalAmount=token?.Count??0;
  if(added){token=new RewardOwnedEntry{Type=RewardType.Currency,TargetId=4};items.Add(token);}
  try
  {
   property.SetValue(doc,fixture);token.Count=12345;
   screen.SelectUnit(0);await Task.Delay(700);
   var focus=screen.GetComponent<UnitUpgradeGoldFocus>();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var root=(RectTransform)typeof(UnitUpgradeGoldFocus).GetField("_root",flags).GetValue(focus);
   var originalCard=(RectTransform)typeof(UnitUpgradeGoldFocus).GetField("_balanceCard",flags).GetValue(focus);
   var card=(RectTransform)root.Find("UpgradeTokenCard");
   var amount=(TMP_Text)typeof(UnitUpgradeGoldFocus).GetField("_tokenAmount",flags).GetValue(focus);
   Check(root.gameObject.activeInHierarchy&&card.gameObject.activeInHierarchy,"upgrade token balance shown with detail popup");
   Check(root.Find("GoldHighlight")==null,"gold highlight removed");
   Check(amount.text=="12,345","header displays actual token balance instead of gold");
   amount.ForceMeshUpdate();Check(!amount.isTextOverflowing,"token balance fits existing gold layout");
   var a=new Vector3[4];var b=new Vector3[4];originalCard.GetWorldCorners(a);card.GetWorldCorners(b);
   Check(originalCard.name=="DiamondCard"&&a.Zip(b,(x,y)=>(x-y).magnitude).All(d=>d<1),"token card matches upper-right diamond position and size");
   Check(card.GetComponentsInChildren<UnityEngine.UI.Image>().Any(i=>i.sprite==DeckUpgradeCurrency.Icon),"token icon rendered");
   var dialog=screen.transform.Find("DroneUpgradeCanvas/SafeArea/Page/UnitDetailsPopup/UnitInfoDialog");
   var plate=(RectTransform)dialog.Find("UpgradeButton/RequiredGoldCost");var text=plate.Find("RequiredCost").GetComponent<TMP_Text>();var icon=(RectTransform)plate.Find("GoldIcon");
   text.rectTransform.GetWorldCorners(a);icon.GetWorldCorners(b);
   float left=plate.InverseTransformPoint(b[0]).x,right=plate.InverseTransformPoint(a[2]).x;
   Check(Math.Abs((left+right)*.5f-plate.rect.center.x)<1,"upgrade token icon and number centered together");
   Check(text.alignment==TextAlignmentOptions.Midline,"upgrade price vertically and horizontally centered");
   ScreenCapture.CaptureScreenshot(folder+"/detail.png");await Task.Delay(150);
   token.Count=7;await Task.Delay(150);Check(amount.text=="7","token display updates when balance changes");
   screen.CloseDetails();await Task.Delay(400);Check(!root.gameObject.activeSelf,"token overlay hides after closing details");
   File.WriteAllLines(folder+"/checks.txt",report);return "PASS "+report.Count+" token header and price layout checks; unsaved fixture restored";
  }
  finally{screen.CloseDetails();property.SetValue(doc,original);if(added)items.Remove(token);else token.Count=originalAmount;typeof(DroneUpgradeConceptScreen).GetMethod("Refresh",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(screen,null);}
 }
}
