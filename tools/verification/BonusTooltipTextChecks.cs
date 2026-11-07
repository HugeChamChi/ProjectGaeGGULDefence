using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LitJson;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using GaeGGUL.DeckUpgrade;
using Object=UnityEngine.Object;
public static class BonusTooltipTextChecks
{
 public static string Run()
 {
  const string folder="outputs/bonus-description-20261107";var log=new System.Collections.Generic.List<string>();
  void Check(bool ok,string label){if(!ok)throw new Exception(label);log.Add("PASS "+label);}
  Directory.CreateDirectory(folder);
  var rows=JsonMapper.ToObject(File.ReadAllText("tools/verification/fixtures/unit-bonus-tooltips.json"));
  var parsed=UnitBonusChart.Parse(rows);var presentations=UnitBonusChart.ParsePresentations(rows);
  Check(parsed.Count==6&&parsed.Values.Sum(v=>v.Count)==30&&presentations.Count==30,"real game parser accepts all 30 bonuses and descriptions");
  var df=typeof(UnitBonusChart).GetField("definitions",BindingFlags.Static|BindingFlags.NonPublic);var pf=typeof(UnitBonusChart).GetField("presentations",BindingFlags.Static|BindingFlags.NonPublic);
  var oldDefinitions=df.GetValue(null);var oldPresentations=pf.GetValue(null);
  var scene=EditorSceneManager.NewPreviewScene();DeckUpgradeBonusView view=null;RenderTexture texture=null;Texture2D pixels=null;
  try
  {
   df.SetValue(null,parsed);pf.SetValue(null,presentations);
   var style=AssetDatabase.FindAssets("t:ResearchViewSettings").Select(g=>AssetDatabase.LoadAssetAtPath<ResearchViewSettings>(AssetDatabase.GUIDToAssetPath(g))).First(s=>s.FontBold!=null&&s.RoundedFill!=null);
   var go=new GameObject("BonusTextCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));SceneManager.MoveGameObjectToScene(go,scene);
   var cg=go.GetComponent<Canvas>();go.GetComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;go.GetComponent<CanvasScaler>().referenceResolution=new Vector2(1080,1920);
   var cameraGo=new GameObject("Camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraGo,scene);var camera=cameraGo.GetComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.nearClipPlane=.1f;camera.farClipPlane=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.1f,.08f,.15f);
   texture=new RenderTexture(1080,1920,24);camera.targetTexture=texture;cg.renderMode=RenderMode.ScreenSpaceCamera;cg.worldCamera=camera;cg.planeDistance=1;
   var container=new GameObject("BonusRows",typeof(RectTransform)).GetComponent<RectTransform>();container.SetParent(go.transform,false);container.anchorMin=Vector2.zero;container.anchorMax=Vector2.one;container.offsetMin=container.offsetMax=Vector2.zero;
   for(int i=1;i<=5;i++)
   {
    var row=new GameObject("LevelBonus_"+(i*3),typeof(RectTransform)).GetComponent<RectTransform>();row.SetParent(container,false);row.anchorMin=row.anchorMax=row.pivot=new Vector2(0,1);row.anchoredPosition=new Vector2(100,-500-i*140);row.sizeDelta=new Vector2(880,120);
    var text=new GameObject("BonusDescription",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();text.transform.SetParent(row,false);text.font=style.FontBold;
   }
   view=container.gameObject.AddComponent<DeckUpgradeBonusView>();view.Build(style,go.transform);
   var cases=new[]{("델탕","UnitData_Deltan","upgrade:hacking_production","노말 +1 / 레어 +1\n에픽 +2 / 전설 +3"),("감망","UnitData_Gamman","upgrade:hacking_consumption","노말 +1 / 레어 +1\n에픽 +2 / 전설 +4"),("알팡","AlphanChief","upgrade:chief_attack_speed","10초간 전체 유닛 공격속도 20% 증가.")};
   foreach(var item in cases)
   {
    var data=item.Item2=="AlphanChief"?null:AssetDatabase.LoadAssetAtPath<UnitData>("Assets/WorkSpace/USW/Data/DroneUnits/"+item.Item2+".asset");view.Refresh(item.Item1,data);
    var text=container.Find("LevelBonus_12/BonusDescription").GetComponent<TMP_Text>();Canvas.ForceUpdateCanvases();text.ForceMeshUpdate(true,true);
    Check(Enumerable.Range(0,text.textInfo.linkCount).Any(i=>text.textInfo.linkInfo[i].GetLinkID()==item.Item3),item.Item1+" level12 has clickable effect link");
    Check(!text.text.Contains("노말")&&!text.text.Contains("갱신")&&!text.text.Contains("액티브"),item.Item1+" short description removes tier table and old terminology");
    if(item.Item2=="AlphanChief")Check(text.text.Contains("족장스킬"),"chief skill label renamed");
    var target=text.GetComponent<TermLinkTapTarget>();typeof(DeckUpgradeBonusView).GetMethod("ShowTerm",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(view,new object[]{target,item.Item3});
    Check(view.EffectTooltip!=null&&view.EffectTooltip.IsOpen,item.Item1+" link opens actual tooltip");
    Check(view.EffectTooltip.Body==item.Item4,item.Item1+" tooltip uses unchanged chart values");
    foreach(var label in view.EffectTooltip.GetComponentsInChildren<TMP_Text>()){label.ForceMeshUpdate(true,true);Check(!label.isTextOverflowing,item.Item1+" tooltip "+label.name+" fits");}
    view.EffectTooltip.GetComponentInChildren<CanvasGroup>().alpha=1;view.EffectTooltip.transform.Find("EffectBubble").localScale=Vector3.one;
    camera.Render();var previous=RenderTexture.active;RenderTexture.active=texture;pixels=new Texture2D(1080,1920,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1080,1920),0,0);pixels.Apply();RenderTexture.active=previous;File.WriteAllBytes(folder+"/"+item.Item2+".png",pixels.EncodeToPNG());Object.DestroyImmediate(pixels);pixels=null;
   }
   Check(presentations.Values.All(p=>!p.Text.Contains("(")&&!p.Text.Contains("갱신")&&!p.Text.Contains("액티브 스킬")),"all exported descriptions cleaned");
   File.WriteAllLines(folder+"/unity-checks.txt",log);return "PASS "+log.Count+" parser, actual link callback and tooltip layout checks";
  }
  finally{df.SetValue(null,oldDefinitions);pf.SetValue(null,oldPresentations);if(view!=null&&view.EffectTooltip!=null)Object.DestroyImmediate(view.EffectTooltip.gameObject);if(pixels!=null)Object.DestroyImmediate(pixels);if(texture!=null){texture.Release();Object.DestroyImmediate(texture);}EditorSceneManager.ClosePreviewScene(scene);}
 }
}
