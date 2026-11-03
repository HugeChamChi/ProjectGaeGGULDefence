using System;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using TMPro;
using GaeGGUL.DeckUpgrade;
public static class LiveBonusDescriptionChecks {
 public static async Task<string> RunPrepared(){
  var rows=LitJson.JsonMapper.ToObject(System.IO.File.ReadAllText("outputs/bonus-description-rows.json"));
  var d=typeof(UnitBonusChart).GetField("definitions",BindingFlags.NonPublic|BindingFlags.Static);var p=typeof(UnitBonusChart).GetField("presentations",BindingFlags.NonPublic|BindingFlags.Static);var oldD=d.GetValue(null);var oldP=p.GetValue(null);
  try{d.SetValue(null,UnitBonusChart.Parse(rows));p.SetValue(null,UnitBonusChart.ParsePresentations(rows));return "Prepared local chart: "+await Run();}finally{d.SetValue(null,oldD);p.SetValue(null,oldP);}
 }
 public static async Task<string> Run(){
  await UnitBonusChart.InitializeAsync(CancellationToken.None);
  if(UnitBonusChart.GetPresentation("AlphanChief",12)==null)throw new Exception("Remote chief description missing");
  var canvas=new GameObject("IsolatedLiveBonusTextCheck",typeof(RectTransform),typeof(Canvas));
  canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
  var content=new GameObject("BonusRows",typeof(RectTransform));content.transform.SetParent(canvas.transform,false);
  var texts=new TMP_Text[5];
  for(int i=0;i<5;i++){
   var row=new GameObject("LevelBonus_"+((i+1)*3),typeof(RectTransform));row.transform.SetParent(content.transform,false);
   var text=new GameObject("BonusDescription",typeof(RectTransform),typeof(TextMeshProUGUI));text.transform.SetParent(row.transform,false);texts[i]=text.GetComponent<TMP_Text>();
  }
  try{
   var view=content.AddComponent<DeckUpgradeBonusView>();
   var settings=AssetDatabase.LoadAssetAtPath<ResearchViewSettings>("Assets/WorkSpace/USW/Data/OutgameUpgrade/UpgradeViewSettings.asset");
   view.Build(settings,content.transform);
   foreach(var key in new[]{"UnitData_Betan","UnitData_Deltan","UnitData_Gamman","UnitData_Disigman","UnitData_Zeltan"}){
    var source=AssetDatabase.LoadAssetAtPath<UnitData>("Assets/WorkSpace/USW/Data/DroneUnits/"+key+".asset");var data=UnitBalanceRuntime.IsLoaded?UnitBalanceRuntime.Resolve(source):source;view.Refresh(data.unitName,data);
    for(int i=0;i<5;i++){var actual=System.Text.RegularExpressions.Regex.Replace(texts[i].text,"<[^>]*>","");var expected=UnitBonusChart.GetPresentation(key,(i+1)*3).Text;if(actual!=expected)throw new Exception("UI text mismatch "+key+"/"+(i+1)*3);}
   }
   view.Refresh("알팡",null);
   Canvas.ForceUpdateCanvases();foreach(var text in texts)text.ForceMeshUpdate();
   if(!texts[0].text.Contains("#FFA64D")||!texts[0].text.Contains("chief_skill"))throw new Exception("Number/term rendering failed: "+texts[0].text);
   if(!texts[3].text.Contains("upgrade:chief_attack_speed")||texts[3].text.Contains("{공격속도효과}")||texts[3].textInfo.linkCount<1)throw new Exception("Effect link rendering failed");
   var target=texts[3].GetComponent<TermLinkTapTarget>();
   typeof(DeckUpgradeBonusView).GetMethod("ShowTerm",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(view,new object[]{target,"upgrade:chief_attack_speed"});
   if(view.EffectTooltip==null)throw new Exception("Effect tooltip not created");
   if(UnitBonusChart.Resolve("AlphanChief",null)!=null&&(!view.EffectTooltip.Body.Contains("10")||!view.EffectTooltip.Body.Contains("20%")))throw new Exception("Tooltip differs from chart: "+view.EffectTooltip.Body);
   return "PASS all 5 drone UI descriptions including Disigman, live chief descriptions, orange numbers, glossary link markup, TMP effect link and tooltip creation; isolated UI, no physical pointer gesture";
  }finally{UnityEngine.Object.DestroyImmediate(canvas);}
 }
}
