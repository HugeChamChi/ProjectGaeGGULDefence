using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LitJson;
using UnityEditor;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Reflection;

public static class ResearchChartChecks
{
    private sealed class MemoryStore : IResearchSaveStore
    {
        public int Writes;
        public UniTask<ResearchSaveData> LoadAsync(ResearchSaveAddress address,CancellationToken token) =>
            UniTask.FromResult(new ResearchSaveData {TreeKey=address.TreeKey});
        public UniTask SaveAsync(ResearchSaveAddress address,ResearchSaveData data,CancellationToken token)
        { Writes++; return UniTask.CompletedTask; }
    }

    public static async Task<string> Screen()
    {
        if (!Application.isPlaying) throw new Exception("Screen check requires Play mode");
        var original=AssetDatabase.LoadAssetAtPath<ResearchTreeData>("Assets/WorkSpace/USW/Data/OutgameUpgrade/UpgradeTree_Basic.asset");
        var settings=AssetDatabase.LoadAssetAtPath<ResearchViewSettings>("Assets/WorkSpace/USW/Data/OutgameUpgrade/UpgradeViewSettings.asset");
        var go=new GameObject("ResearchChartScreenCheck",typeof(RectTransform),typeof(Canvas));
        go.SetActive(false);
        var store=new MemoryStore();
        using var charts=new ResearchChartService();
        using var saves=new ResearchSaveService(store,charts);
        try
        {
            var screen=go.AddComponent<ResearchScreen>();
            typeof(ResearchScreen).GetField("_trees",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(screen,new[]{original});
            typeof(ResearchScreen).GetField("_settings",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(screen,settings);
            screen.Construct(saves,new ResearchAccountContext(),null,original);
            go.SetActive(true);
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await UniTask.WaitUntil(()=>screen.Progress!=null,cancellationToken:timeout.Token);
            if(!screen.Progress.Tree.IsChartDefinition || screen.Selected?.Icon==null) throw new Exception("Screen is not using chart and Addressables");
            var selected=screen.Selected;
            screen.Upgrade(selected);
            await UniTask.Yield();
            if(screen.Progress.GetLevel(selected)!=1 || store.Writes!=1) throw new Exception("Chart upgrade/save path failed");
            var shared=await saves.ResolveTreeAsync(original,CancellationToken.None);
            var session=saves.GetSession("local",shared,"OutgameUpgrade.");
            if(!ReferenceEquals(session.Progress,screen.Progress)) throw new Exception("Screen/battle session differs");
            return JsonMapper.ToJson(new{nodes=shared.Nodes.Count,upgraded=selected.Id,writes=store.Writes,storage="isolated memory",chartDefinition=true});
        }
        finally {UnityEngine.Object.DestroyImmediate(go);}
    }
    public static async Task<string> Run()
    {
        if (!Application.isPlaying) throw new Exception("Research chart checks require Play mode");
        var source = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/WorkSpace/USW/Resources/ResearchBalance.json");
        var parsed = ResearchChartParser.Parse(JsonMapper.ToObject(source.text));
        int nodes = 0, comparisons = 0;
        var originals = AssetDatabase.FindAssets("t:ResearchTreeData",new[]{"Assets/WorkSpace/USW/Data/OutgameUpgrade"})
            .Select(g=>AssetDatabase.LoadAssetAtPath<ResearchTreeData>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        try
        {
            foreach (var original in originals)
            {
                var actual = parsed[original.TreeKey];
                if (actual.Nodes.Count != original.Nodes.Count || actual.LegacySaveName != original.LegacySaveName) throw new Exception("Tree mismatch");
                var levels = new Dictionary<string,int>();
                foreach(var node in original.Nodes)
                {
                    var converted=actual.Find(node.Id);
                    if (converted == null || converted.Stat!=node.Stat || converted.MaxLevel!=node.MaxLevel || converted.ValuePerLevel!=node.ValuePerLevel ||
                        converted.Shape!=node.Shape || converted.Row!=node.Row || converted.Column!=node.Column ||
                        converted.RequiredTotalLevel!=node.RequiredTotalLevel || converted.RequireAnyParent!=node.RequireAnyParent ||
                        converted.ExclusiveGroup!=(node.ExclusiveGroup??"") || converted.RecommendOrder!=node.RecommendOrder ||
                        !converted.Parents.SequenceEqual(node.Parents)) throw new Exception("Node mismatch: "+original.TreeKey+"/"+node.Id);
                    levels[node.Id]=node.MaxLevel;
                    nodes++;
                }
                var before=new ResearchProgress(original,levels); var after=new ResearchProgress(actual,levels);
                foreach(ResearchStat stat in Enum.GetValues(typeof(ResearchStat)))
                { if (before.GetTotal(stat)!=after.GetTotal(stat)) throw new Exception("Bonus mismatch: "+stat); comparisons++; }
            }
            var changed=JsonMapper.ToObject(source.text);
            changed[0]["ValuePerLevel"]=0.25;
            var modified=ResearchChartParser.Parse(changed);
            try
            {
                string key=changed[0]["TreeKey"].ToString(), id=changed[0]["NodeId"].ToString();
                var progress=new ResearchProgress(modified[key],new Dictionary<string,int>{{id,2}});
                if (progress.GetTotal(modified[key].Find(id).Stat)!=0.5f) throw new Exception("Edited chart has no effect");
                var settings=AssetDatabase.LoadAssetAtPath<ResearchViewSettings>("Assets/WorkSpace/USW/Data/OutgameUpgrade/UpgradeViewSettings.asset");
                if (settings.GetPreview(modified[key],modified[key].Find(id))!=null) throw new Exception("Preview overrides chart");
            }
            finally {ResearchChartParser.Destroy(modified);}
        }
        finally {ResearchChartParser.Destroy(parsed);}

        int rejected=0;
        foreach(string kind in new[]{"duplicate","cycle","missingParent","stat","nan","mixedRelease","missingIcon"})
        {
            var rows=JsonMapper.ToObject(source.text);
            if(kind=="duplicate") rows.Add(rows[0]);
            if(kind=="cycle") rows[0]["Parents"]=JsonMapper.ToJson(new[]{rows[0]["NodeId"].ToString()});
            if(kind=="missingParent") rows[0]["Parents"]="[\"absent\"]";
            if(kind=="stat") rows[0]["Stat"]="0";
            if(kind=="nan") rows[0]["ValuePerLevel"]="NaN";
            if(kind=="mixedRelease") rows[0]["ReleaseKey"]="different";
            if(kind=="missingIcon") rows[0]["IconAddress"]="";
            try { var invalid=ResearchChartParser.Parse(rows); ResearchChartParser.Destroy(invalid); }
            catch(FormatException) {rejected++; continue;}
            throw new Exception("Invalid input accepted: "+kind);
        }
        using(var service=new ResearchChartService())
        {
            foreach(var original in originals)
            {
                var tree=await service.ResolveAsync(original,CancellationToken.None);
                if (!ReferenceEquals(tree,await service.ResolveAsync(original,CancellationToken.None))) throw new Exception("Shared snapshot mismatch");
                foreach(var node in tree.Nodes)
                    if(node.Icon==null || node.Icon!=original.Find(node.Id).Icon) throw new Exception("Addressable icon mismatch: "+node.IconAddress);
            }
        }
        return JsonMapper.ToJson(new{trees=originals.Length,nodes,statComparisons=comparisons,rejected,icons="all matched original sprites",chart=ResearchChartService.ChartName});
    }
}
