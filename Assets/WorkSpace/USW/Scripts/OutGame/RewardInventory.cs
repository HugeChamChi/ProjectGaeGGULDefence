using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public sealed class RewardOwnedEntry { public RewardType Type; public int TargetId; public int Count; }
[Serializable] public sealed class RewardGrantReceipt { public string GrantId; public RewardAmount[] Rewards; }
[Serializable] public sealed class RewardInventory
{
    public List<RewardOwnedEntry> Owned = new();
    public List<RewardGrantReceipt> Receipts = new();
    public int Count(RewardType type,int target) => Owned.Find(x=>x.Type==type && x.TargetId==target)?.Count ?? 0;

    // Apply to a detached snapshot first: any invalid item leaves the original untouched.
    public static PlayerData Apply(PlayerData current,RewardCatalog catalog,string grantId,RewardAmount[] rewards)
    {
        if(current==null || catalog==null || string.IsNullOrWhiteSpace(grantId)) throw new ArgumentException("Missing grant context");
        if(current.RewardInventory.Receipts.Exists(x=>x.GrantId==grantId)) return null;
        var expanded=catalog.Expand(rewards);
        var next=JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(current));
        foreach(var (definition,amount) in expanded) {
            if(definition.Type==RewardType.Currency) {
                switch(definition.TargetId) {
                    case 1: next.Gold=checked(next.Gold+amount); break;
                    case 2: next.Diamond=checked(next.Diamond+amount); break;
                    case 3: next.Stamina=checked(next.Stamina+amount); break;
                    default: throw new ArgumentException("Unsupported currency");
                }
            } else {
                var item=next.RewardInventory.Owned.Find(x=>x.Type==definition.Type && x.TargetId==definition.TargetId);
                if(item==null) {item=new RewardOwnedEntry {Type=definition.Type,TargetId=definition.TargetId};next.RewardInventory.Owned.Add(item);}
                item.Count=definition.Type==RewardType.Unit ? checked(item.Count+amount) : 1;
            }
        }
        var saved=new RewardAmount[rewards.Length];
        for(int i=0;i<rewards.Length;i++) saved[i]=new RewardAmount {RewardId=rewards[i].RewardId,Amount=rewards[i].Amount};
        next.RewardInventory.Receipts.Add(new RewardGrantReceipt {GrantId=grantId,Rewards=saved});
        return next;
    }
}
