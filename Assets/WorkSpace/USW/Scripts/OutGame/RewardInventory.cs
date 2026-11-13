using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public sealed class RewardOwnedEntry { public RewardType Type; public int TargetId; public int Count; }
[Serializable] public sealed class RewardGrantReceipt { public string GrantId; public RewardAmount[] Rewards; }
[Serializable] public sealed class RewardInventory
{
    public List<RewardOwnedEntry> Owned = new();
    public List<RewardGrantReceipt> Receipts = new();
    public int BattleGoldRemainder;
    public int BattleUpgradeTokenRemainder;
    public int Count(RewardType type,int target) { int count = Owned.Find(x=>x.Type==type && x.TargetId==target)?.Count ?? 0; return type == RewardType.Unit && count > 0 ? 1 : count; }

    // Apply to a detached snapshot first: any invalid item leaves the original untouched.
    public static PlayerData Apply(PlayerData current,RewardCatalog catalog,string grantId,RewardAmount[] rewards,
        int battleGoldBasisPoints = 0, int battleTokenBasisPoints = 0)
    {
        if (battleGoldBasisPoints < 0 || battleTokenBasisPoints < 0) throw new ArgumentOutOfRangeException("Battle reward bonus");
        if(current==null || catalog==null || string.IsNullOrWhiteSpace(grantId)) throw new ArgumentException("Missing grant context");
        if(current.RewardInventory.Receipts.Exists(x=>x.GrantId==grantId)) return null;
        var expanded=catalog.Expand(rewards);
        var next=JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(current));
        foreach(var (definition,baseAmount) in expanded) {
            int amount = baseAmount;
            if (definition.Type == RewardType.Currency && definition.TargetId == 1)
                amount = WithBattleBonus(amount, battleGoldBasisPoints, ref next.RewardInventory.BattleGoldRemainder);
            if (definition.Type == RewardType.Currency && definition.TargetId == 4)
                amount = WithBattleBonus(amount, battleTokenBasisPoints, ref next.RewardInventory.BattleUpgradeTokenRemainder);
            if(definition.Type==RewardType.Currency) {
                switch(definition.TargetId) {
                    case 1: next.Gold=checked(next.Gold+amount); break;
                    case 2: next.Diamond=checked(next.Diamond+amount); break;
                    case 3: next.Stamina=checked(next.Stamina+amount); break;
                    case 4:
                        var token = next.RewardInventory.Owned.Find(x => x.Type == RewardType.Currency && x.TargetId == 4);
                        if (token == null) { token = new RewardOwnedEntry { Type = RewardType.Currency, TargetId = 4 }; next.RewardInventory.Owned.Add(token); }
                        token.Count = checked(token.Count + amount); break;
                    default: throw new ArgumentException("Unsupported currency");
                }
            } else {
                var item=next.RewardInventory.Owned.Find(x=>x.Type==definition.Type && x.TargetId==definition.TargetId);
                if(item==null) {item=new RewardOwnedEntry {Type=definition.Type,TargetId=definition.TargetId};next.RewardInventory.Owned.Add(item);}
                item.Count=1;
            }
        }
        var saved=new RewardAmount[rewards.Length];
        for(int i=0;i<rewards.Length;i++) saved[i]=new RewardAmount {RewardId=rewards[i].RewardId,Amount=rewards[i].Amount};
        next.RewardInventory.Receipts.Add(new RewardGrantReceipt {GrantId=grantId,Rewards=saved});
        return next;
    }

    private static int WithBattleBonus(int amount, int basisPoints, ref int remainder)
    {
        if (basisPoints == 0) return amount;
        if (remainder < 0 || remainder >= 10000) throw new InvalidOperationException("Invalid battle reward remainder");
        long value = checked((long)amount * basisPoints + remainder);
        int result = checked(amount + (int)(value / 10000));
        remainder = (int)(value % 10000);
        return result;
    }
}
