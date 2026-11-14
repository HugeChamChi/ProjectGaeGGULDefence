using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using BackendFunction;

var data=JsonDocument.Parse(File.ReadAllText(args[0])).RootElement;
var row=data.GetProperty("config").EnumerateObject().ToDictionary(x=>x.Name,x=>x.Value.ToString());
var model=new OutgameEconomy(row);int checks=0;
void Equal(int actual,int expected){checks++;if(actual!=expected)throw new Exception(actual+" != "+expected);}
void Reject(Action action){checks++;try{action();}catch(ArgumentException){return;}throw new Exception("Expected rejection");}
int level=2;foreach(var cost in data.GetProperty("unitCosts").EnumerateArray())Equal(model.UnitUpgradeCost(level++),cost.GetInt32());
int total=0;
foreach(var n in data.GetProperty("nodes").EnumerateArray()){
    int max=n.GetProperty("MaxLevel").GetInt32(),first=n.GetProperty("BaseCost").GetInt32(),step=n.GetProperty("CostStep").GetInt32();
    for(int current=0;current<max;current++){
        var cost=model.ResearchUpgradeCost("BASIC",n.GetProperty("Row").GetInt32(),n.GetProperty("Stat").GetString(),n.GetProperty("ValuePerLevel").GetDouble(),current,max);
        Equal(cost,first+step*current);total+=cost;
    }
}
Equal(total,1684450);
foreach(var sample in data.GetProperty("rewards").EnumerateArray()){
    int count=sample[0].GetInt32();var bosses=Enumerable.Range(0,count).Select(i=>new OutgameEconomy.BossProgress{SpawnMaxHp=100*Math.Pow(1.13,i)*(new[]{3,6,0}.Contains((i+1)%10)?1.1*Math.Pow(1.13,2):1),LowestHp=0});
    var reward=model.BattleReward(100,bosses,0,0);Equal(reward.Gold,sample[1].GetInt32());Equal(reward.UpgradeTokenA,sample[2].GetInt32());
}
var half=new OutgameEconomy.BossProgress{SpawnMaxHp=400,LowestHp=200};
var partial=model.BattleReward(100,new[]{half,half},0,0);Equal(partial.Gold,79);Equal(partial.UpgradeTokenA,7);
var capped=model.BattleReward(100,new[]{new OutgameEconomy.BossProgress{SpawnMaxHp=100,LowestHp=-200},new OutgameEconomy.BossProgress{SpawnMaxHp=100,LowestHp=200}},.1,.2);Equal(capped.Gold,66);Equal(capped.UpgradeTokenA,7);
Reject(()=>model.UnitUpgradeCost(21));Reject(()=>model.BattleReward(0,new[]{half},0,0));Reject(()=>model.BattleReward(100,new[]{half},double.NaN,0));
Reject(()=>model.ResearchUpgradeCost("CHOICE",0,"AttackPercent",.002,0,3));Reject(()=>model.ResearchUpgradeCost("BASIC",0,"SkillCooldownReduction",1,0,3));
foreach(var key in row.Keys){var missing=new Dictionary<string,string>(row);missing.Remove(key);Reject(()=>new OutgameEconomy(missing));}
var extra=new Dictionary<string,string>(row);extra.Add("Unknown","1");Reject(()=>new OutgameEconomy(extra));
foreach(var invalid in new[]{"NaN","Infinity","-1",""}){var bad=new Dictionary<string,string>(row);bad["BaseGold"]=invalid;Reject(()=>new OutgameEconomy(bad));}
Console.WriteLine("PASS C# Function parity and rejection: "+checks+" checks");
