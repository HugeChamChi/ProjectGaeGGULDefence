using System;
using System.Collections.Generic;
using UnityEngine;

public enum RewardType { None=0, Currency=1, Unit=2, Chief=3, DeckUnlock=4, Bundle=5 }
[Serializable] public sealed class RewardAmount { public int RewardId; public int Amount; }
[Serializable] public sealed class RewardDefinition
{
    public int RewardId;
    public RewardType Type;
    public int TargetId;
    public string Name;
    public Sprite Icon;
    public RewardAmount[] Contents = Array.Empty<RewardAmount>();
}
[CreateAssetMenu(menuName="Game/Reward Catalog")]
public sealed class RewardCatalog : ScriptableObject
{
    public RewardDefinition[] Entries = Array.Empty<RewardDefinition>();
    public RewardDefinition Find(int id)
    {
        RewardDefinition found=null;
        foreach(var entry in Entries)
            if(entry != null && entry.RewardId==id) {
                if(found!=null) throw new InvalidOperationException("Duplicate reward ID: "+id);
                found=entry;
            }
        return found ?? throw new InvalidOperationException("Unknown reward ID: "+id);
    }
    public List<(RewardDefinition definition,int amount)> Expand(RewardAmount[] rewards)
    {
        if(rewards==null || rewards.Length==0) throw new ArgumentException("Empty rewards");
        var result=new List<(RewardDefinition,int)>();
        void Add(RewardAmount reward,int multiplier,bool nested)
        {
            if(reward==null || reward.RewardId<=0 || reward.Amount<=0) throw new ArgumentException("Invalid reward amount");
            var definition=Find(reward.RewardId);
            int amount=checked(reward.Amount*multiplier);
            if(definition.Type==RewardType.Bundle) {
                if(nested || definition.Contents==null || definition.Contents.Length==0) throw new ArgumentException("Invalid/nested bundle");
                foreach(var child in definition.Contents) Add(child,amount,true);
            } else {
                if(definition.TargetId<=0 || (int)definition.Type<(int)RewardType.Currency || (int)definition.Type>(int)RewardType.DeckUnlock) throw new ArgumentException("Invalid reward target/type");
                if((definition.Type==RewardType.DeckUnlock || definition.Type==RewardType.Chief) && amount!=1) throw new ArgumentException("Unlock amount must be one");
                result.Add((definition,amount));
            }
        }
        foreach(var reward in rewards) Add(reward,1,false);
        return result;
    }
}
