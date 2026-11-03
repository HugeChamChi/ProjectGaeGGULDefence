using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BackEnd;
using LitJson;
using UnityEngine;
public static class AccountStructureProbe {
 public static async Task<string> Run() {
  var completion=new TaskCompletionSource<BackendReturnObject>();
  Backend.GameData.GetMyData("PlayerData",new Where(),r=>completion.TrySetResult(r));
  if(await Task.WhenAny(completion.Task,Task.Delay(15000))!=completion.Task) return "read_timeout";
  var response=await completion.Task;
  if(!response.IsSuccess())return "read_failed:"+response.GetStatusCode();
  var rows=response.FlattenRows();
  if(rows.Count!=1)return "row_count:"+rows.Count;
  var row=rows[0]; var columns=new List<string>(); foreach(string k in row.Keys)columns.Add(k);
  string server=row.ContainsKey("PlayerSaveId")?row["PlayerSaveId"].ToString():"";
  var key="PlayerData.Pending.v1."+Backend.UserInDate;
  if(PlayerPrefs.HasKey(key)) {
   var pending=JsonUtility.FromJson<PlayerDataJournal>(PlayerPrefs.GetString(key));
   var method=typeof(BackendGameData).GetMethod("ParsePlayerData",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
   var current=(PlayerData)method.Invoke(new BackendGameData(),new object[]{row});
   var diffs=new List<string>();
   foreach(var field in typeof(PlayerData).GetFields()) {
    if(field.Name=="PlayerUid" || field.Name=="PlayerName")continue;
    if(!field.FieldType.IsPrimitive && field.FieldType!=typeof(string))continue;
    var a=field.GetValue(current);var b=field.GetValue(pending.Data);
    if(!Equals(a,b))diffs.Add(field.Name+": server="+a+", local="+b);
   }
   var report="ServerSaveId="+server+"; LocalBaseSaveId="+pending.BaseSaveId+"; LocalSaveId="+pending.Data.SaveId+"; ServerUpdatedAt="+(row.ContainsKey("updatedAt")?row["updatedAt"].ToString():"missing")+"; Differences="+string.Join(" | ",diffs);
   Debug.Log("[AccountConflictProbe] "+report);
   System.IO.File.WriteAllText("outputs/account-conflict-detail.txt",report);
  }
  bool journal=PlayerPrefs.HasKey(key); bool parsed=false, data=false, same=false, baseline=false;
  if(journal) { try { var pending=JsonUtility.FromJson<PlayerDataJournal>(PlayerPrefs.GetString(key)); parsed=pending!=null; data=pending?.Data!=null; if(data){same=pending.Data.SaveId==server;baseline=(pending.BaseSaveId??"")==server;} }catch{} }
  return JsonMapper.ToJson(new {table="PlayerData",rows=rows.Count,columns,serverSaveIdPresent=row.ContainsKey("PlayerSaveId"),serverSaveIdEmpty=string.IsNullOrEmpty(server),journalPresent=journal,journalParsed=parsed,journalData=data,pendingMatchesServer=same,baselineMatchesServer=baseline});
 }
}
