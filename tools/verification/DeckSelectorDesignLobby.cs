using System;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using UnityEngine;
public static class DeckSelectorDesignLobby
{
 public static string Inspect() {
 var scene=EditorSceneManager.OpenScene("Assets/Scenes/LobbyScene.unity",OpenSceneMode.Additive);
 try { return string.Join("\n",scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<RectTransform>(true)).Where(x=>x.name.IndexOf("gold",StringComparison.OrdinalIgnoreCase)>=0||x.name.IndexOf("stamina",StringComparison.OrdinalIgnoreCase)>=0||x.name.IndexOf("dia",StringComparison.OrdinalIgnoreCase)>=0||x.name.IndexOf("profile",StringComparison.OrdinalIgnoreCase)>=0||x.name.IndexOf("currency",StringComparison.OrdinalIgnoreCase)>=0||x.name.IndexOf("Top",StringComparison.OrdinalIgnoreCase)>=0).Select(x=>Path(x)+" "+x.rect+" pos="+x.anchoredPosition+" scale="+x.localScale)); }
 finally { EditorSceneManager.CloseScene(scene,true); }
 }
 public static string Copy() {
 var scene=EditorSceneManager.OpenScene("Assets/Scenes/LobbyScene.unity",OpenSceneMode.Additive);
 GameObject clone=null;
 try {
  var source=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<RectTransform>(true)).First(x=>x.name=="MainUI_TopPanel");
  clone=UnityEngine.Object.Instantiate(source.gameObject); clone.name="LobbyTopPreview";
  foreach(var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true)) {
   string ns=behaviour.GetType().Namespace??"";
   if(!ns.StartsWith("UnityEngine.UI")&&!ns.StartsWith("TMPro")&&!ns.StartsWith("UnityEngine.EventSystems")) UnityEngine.Object.DestroyImmediate(behaviour);
  }
  foreach(var button in clone.GetComponentsInChildren<Button>(true)) {
   button.onClick=new Button.ButtonClickedEvent(); button.interactable=false; button.transition=Selectable.Transition.None;
  }
  foreach(var text in clone.GetComponentsInChildren<TMP_Text>(true)) {
   text.fontStyle|=FontStyles.Bold;
   if(text.name=="MainUI_TopPanel_PlayerNameText")text.text="Player";
   if(text.name=="MainUI_TopPanel_StaminaText")text.text="30 / 30";
   if(text.name=="MainUI_TopPanel_StaminaTimerText")text.gameObject.SetActive(false);
   if(text.name=="MainUI_TopPanel_GoldText")text.text="12,000";
   if(text.name=="MainUI_TopPanel_DiamondText")text.text="1,000";
  }
  clone.SetActive(true);
  var saved=PrefabUtility.SaveAsPrefabAsset(clone,"Assets/WorkSpace/USW/DeckUpgrade/Concepts/LobbyTopPreview.prefab");
  if(saved==null)throw new Exception("Lobby top copy failed.");
  return "Copied lobby top visuals with temporary values and no account binding.";
 } finally {if(clone!=null)UnityEngine.Object.DestroyImmediate(clone);EditorSceneManager.CloseScene(scene,true);}
 }
 private static string Path(Transform t) {return t.parent==null?t.name:Path(t.parent)+"/"+t.name;}
}
