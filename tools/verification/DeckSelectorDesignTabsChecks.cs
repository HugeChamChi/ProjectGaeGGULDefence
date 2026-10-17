using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GaeGGUL.DeckUpgrade;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public static class DeckSelectorDesignTabsChecks
{
 public static async Task<string> Check()
 {
  await Task.Delay(350);
  var selector=UnityEngine.Object.FindFirstObjectByType<DeckSelectorConcept>();var screen=selector.screen;
  var page=selector.transform.Find("DroneUpgradeCanvas/SafeArea/Page");
  var dialog=page.Find("UnitDetailsPopup/UnitInfoDialog");
  page.Find("DeckCollection/UnitListPanel/UnitViewport/UnitGrid/Unit_0").GetComponent<Button>().onClick.Invoke();await Task.Delay(350);
  var tabs=dialog.GetComponent<DeckUpgradeStatsTabs>();
  if(tabs.SelectedTab!=0||dialog.Find("StatsPanel/UpgradeStatRows")!=null)throw new Exception("Default skill tab or old stats removal failed.");
  if(dialog.GetComponentsInChildren<Transform>(true).Any(x=>x.name=="GoldIcon"))throw new Exception("Detail gold icons must be removed.");
  tabs.Select(1,true);if(!tabs.Sliding)throw new Exception("Tab should slide.");await Task.Delay(90);
  string output=Path.Combine(Directory.GetCurrentDirectory(),"outputs/unit-upgrade-designs");
  ScreenCapture.CaptureScreenshot(Path.Combine(output,"01-tab-transition.png"));await Task.Delay(200);
  if(tabs.Sliding||tabs.SelectedTab!=1)throw new Exception("Slide must complete.");
  tabs.Select(2,true);await Task.Delay(280);
  var value=dialog.Find("SpecialAbilityScroll/AbilityViewport/TabPageTrack/StatsPage/CurrentStats/AttackCurrentStat/CurrentValue").GetComponent<TMP_Text>();
  if(value.text.Contains("→")||value.text.Contains("›"))throw new Exception("Stats must show current values only.");
  var progress=(DeckUpgradeProgress)typeof(DroneUpgradeConceptScreen).GetField("progress",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(screen);
  string snapshot=JsonUtility.ToJson(progress);bool hadSave=PlayerPrefs.HasKey(DeckUpgradeProgress.SaveKey);string saved=PlayerPrefs.GetString(DeckUpgradeProgress.SaveKey,"");
  try {
   progress.money=1000000;var entry=progress.units.Find(x=>x.id==screen.units[0].name);if(entry!=null)entry.level=1;
   screen.SelectUnit(0);await Task.Delay(100);tabs.Select(0,false);await Task.Delay(100);
   string before=value.text;dialog.Find("UpgradeButton").GetComponent<Button>().onClick.Invoke();await Task.Delay(300);
   if(tabs.SelectedTab!=2||value.text==before)throw new Exception("Successful upgrade must select stats and refresh current values.");
   var gauge=dialog.Find("UpgradeButton/RequiredGoldCost").GetComponent<UpgradeCostGauge>();
   if(!gauge.Ready||gauge.Ratio!=1||!gauge.Arrow.gameObject.activeSelf)throw new Exception("Affordable upgrade must show filled gauge and arrow.");
   ScreenCapture.CaptureScreenshot(Path.Combine(output,"01-upgrade-ready.png"));await Task.Delay(180);
  } finally {
   var restored=JsonUtility.FromJson<DeckUpgradeProgress>(snapshot);progress.money=restored.money;progress.units=restored.units;
   if(hadSave)PlayerPrefs.SetString(DeckUpgradeProgress.SaveKey,saved);else PlayerPrefs.DeleteKey(DeckUpgradeProgress.SaveKey);PlayerPrefs.Save();
   screen.SelectUnit(0);await Task.Delay(350);tabs.Select(2,false);await Task.Delay(100);
  }
  ScreenCapture.CaptureScreenshot(Path.Combine(output,"01-stats.png"));await Task.Delay(180);screen.CloseDetails();
  File.WriteAllText(Path.Combine(output,"STATS-RESULT.txt"),"PASS: current-only stats, three horizontal sliding tabs, upgrade auto-selects stats, detail gold icons removed; preview progress restored.");
  return "PASS: sliding tabs, auto-select stats after real upgrade, current values only, gold icons removed; local preview data restored.";
 }
}
