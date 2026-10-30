using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using LitJson;
using UnityEditor;
using UnityEngine;

public sealed class BonusValueTestWindow : EditorWindow
{
 Dictionary<string,List<UnitLevelBonusDefinition>> definitions;
 Dictionary<string,UnitBonusChart.Presentation> presentations;
 string[] keys=Array.Empty<string>();
 int selected,level=15;
 string file="",status="업로드용 엑셀을 선택하세요.";
 Vector2 scroll;
 UnitLevelBonusController controller;
 [MenuItem("Tools/Backend Tests/보너스 Value 규약 검사")]
 public static void Open()=>GetWindow<BonusValueTestWindow>("보너스 Value 검사");
 public static JsonData ReadWorkbook(string path)
 {
  string script=Path.GetFullPath("tools/planner-studio/read-bonus-workbook.cjs");
  var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=System.Text.Encoding.UTF8,StandardErrorEncoding=System.Text.Encoding.UTF8};
  start.Arguments="\""+script+"\" \""+Path.GetFullPath(path)+"\"";
  using var process=Process.Start(start);
  var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
  if(!process.WaitForExit(15000)){process.Kill();throw new TimeoutException("엑셀 읽기 시간 초과");}
  if(process.ExitCode!=0)throw new FormatException(error.GetAwaiter().GetResult());
  return JsonMapper.ToObject(output.GetAwaiter().GetResult());
 }
 public void LoadWorkbook(string path)
 {
  var rows=ReadWorkbook(path);var parsed=UnitBonusChart.Parse(rows);var texts=UnitBonusChart.ParsePresentations(rows);
  definitions=parsed;presentations=texts;keys=parsed.Keys.OrderBy(k=>k).ToArray();selected=0;file=path;ResetController();
  status=$"검증 통과: {keys.Length}종 / {texts.Count}개 보너스 / {rows.Count}행";
 }
 void ResetController(){if(definitions!=null)controller=new UnitLevelBonusController(definitions[keys[selected]],level);}
 void OnGUI()
 {
  EditorGUILayout.HelpBox("실제 게임과 같은 파서·레벨 보너스 계산기로 검사합니다. 엑셀 변경 후 다시 불러오세요. 계정과 서버에는 저장하지 않습니다.",MessageType.Info);
  if(GUILayout.Button("업로드용 보너스 엑셀 불러오기")){
   string path=EditorUtility.OpenFilePanel("UnitLevelBonuses 업로드용 엑셀",Path.GetDirectoryName(file),"xlsx");
   if(path!="")try{LoadWorkbook(path);}catch(Exception e){definitions=null;status=e.Message;}
  }
  EditorGUILayout.LabelField(status,EditorStyles.wordWrappedLabel);
  if(definitions==null)return;
  EditorGUILayout.SelectableLabel(file,EditorStyles.wordWrappedMiniLabel,GUILayout.Height(32));
  EditorGUI.BeginChangeCheck();selected=EditorGUILayout.Popup("대상 유닛",selected,keys);level=EditorGUILayout.IntSlider("테스트 레벨",level,1,1000);
  if(EditorGUI.EndChangeCheck())ResetController();
  scroll=EditorGUILayout.BeginScrollView(scroll);
  foreach(var bonus in definitions[keys[selected]].OrderBy(b=>b.Level)){
   EditorGUILayout.LabelField($"Lv.{bonus.Level} {(bonus.Level<=level?"해금":"잠김")}",EditorStyles.boldLabel);
   EditorGUILayout.LabelField(presentations[keys[selected]+"/"+bonus.Level].Text,EditorStyles.wordWrappedLabel);
  }
  EditorGUILayout.Space();EditorGUILayout.LabelField("실제 누적 능력치 (게임 내부 단위)",EditorStyles.boldLabel);
  foreach(UnitBonusStat stat in Enum.GetValues(typeof(UnitBonusStat))){float value=controller.GetStat(stat);if(value!=0)EditorGUILayout.LabelField(stat.ToString(),value.ToString("0.######"));}
  if(keys[selected]=="UnitData_Betan"){
   var next=controller.PeekCycle("basic_skill");EditorGUILayout.LabelField($"다음 자폭 #{next.Number}: 피해 ×{next.DamageMultiplier}, 충전 ×{next.ChargeTimeMultiplier}");
   if(GUILayout.Button("자폭 1회 완료"))controller.CompleteCycle("basic_skill");
   if(GUILayout.Button("자폭 횟수 초기화"))ResetController();
  }
  EditorGUILayout.EndScrollView();
 }
}
