using System;
using System.Collections.Generic;
using System.Globalization;
using LitJson;

namespace GGD.BackendCharts
{
    public static class ReadableChartRows
    {
        static string Text(JsonData row, string key) => row.ContainsKey(key) && row[key] != null ? row[key].ToString() : "";
        static double Number(JsonData row, string key)
        {
            if (!double.TryParse(Text(row,key), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || double.IsNaN(n) || double.IsInfinity(n)) throw new FormatException("Invalid number: " + key);
            return n;
        }
        static bool Bool(JsonData row,string key) => Text(row,key) switch {"true" or "True" or "1" => true, "false" or "False" or "0" => false, _=>throw new FormatException("Invalid bool: "+key)};
        static Dictionary<string,object> Strings(JsonData row,params string[] keys)
        { var value=new Dictionary<string,object>();foreach(var key in keys)value[key]=Text(row,key);return value; }
        public static string Decode(JsonData rows)
        {
            if(rows[0].ContainsKey("Tier"))return Units(rows);
            if(rows[0].ContainsKey("RecordType"))return Missions(rows);
            throw new FormatException("Unknown readable chart columns");
        }
        static string Units(JsonData rows)
        {
            var units=new Dictionary<string,Dictionary<string,object>>();
            foreach(JsonData row in rows)
            {
                string key=Text(row,"UnitKey");
                if(!units.TryGetValue(key,out var unit)) {unit=Strings(row,"UnitKey","AssetKey","Name");unit["Stats"]=new List<object>();units.Add(key,unit);}
                if((string)unit["AssetKey"]!=Text(row,"AssetKey") || (string)unit["Name"]!=Text(row,"Name"))throw new FormatException("Mixed unit metadata");
                var stat=Strings(row,"Tier");foreach(var k in new[]{"Attack","AttackFrequency","SkillCooldown","FoodProduction"})stat[k]=Number(row,k);
                var count=Number(row,"MaxDroneCount");if(count!=Math.Truncate(count)||count<0||count>int.MaxValue)throw new FormatException("MaxDroneCount");stat["MaxDroneCount"]=(int)count;
                ((List<object>)unit["Stats"]).Add(stat);
            }
            return JsonMapper.ToJson(new {SchemaVersion=1,Units=new List<Dictionary<string,object>>(units.Values)});
        }
        static string Missions(JsonData rows)
        {
            var bindings=new List<object>();var events=new List<object>();var missions=new Dictionary<string,Dictionary<string,object>>();var rewards=new List<JsonData>();
            foreach(JsonData row in rows)switch(Text(row,"RecordType"))
            {
                case "Binding": var binding=Strings(row,"BindingKey","HookKey","DataKey","ValueSource");binding["ConstantValue"]=Number(row,"ConstantValue");binding["Enabled"]=Bool(row,"Enabled");bindings.Add(binding);break;
                case "Event": var evt=Strings(row,"EventKey","Name","StartAt","EndAt");evt["Enabled"]=Bool(row,"Enabled");events.Add(evt);break;
                case "Mission": var mission=Strings(row,"MissionKey","EventKey","Name","DataKey","Period");mission["Target"]=Number(row,"Target");mission["Rewards"]=new List<object>();missions.Add(Text(row,"MissionKey"),mission);break;
                case "Reward": rewards.Add(row);break;
                default: throw new FormatException("Unknown mission row type");
            }
            var seen=new HashSet<string>();foreach(var row in rewards)
            {
                string key=Text(row,"MissionKey");if(!missions.TryGetValue(key,out var mission))throw new FormatException("Unknown reward mission");
                double index=Number(row,"RewardIndex"),amount=Number(row,"Amount");if(index<0||index!=Math.Truncate(index)||!seen.Add(key+"/"+index)||amount<1||amount!=Math.Truncate(amount)||amount>int.MaxValue)throw new FormatException("Invalid reward");
                ((List<object>)mission["Rewards"]).Add(new {itemId=Text(row,"ItemKey"),amount=(int)amount});
            }
            return JsonMapper.ToJson(new {CatalogVersion=1,DataBindings=bindings,MissionEvents=events,Missions=new List<Dictionary<string,object>>(missions.Values)});
        }
        public static JsonData Research(JsonData rows)
        {
            var nodes=new List<Dictionary<string,object>>();var parents=new Dictionary<string,SortedDictionary<int,string>>();var lanes=new Dictionary<string,SortedDictionary<int,string>>();
            foreach(JsonData row in rows)
            {
                string tree=Text(row,"TreeKey"),key=tree+"/"+Text(row,"NodeId");
                switch(Text(row,"RecordType")) {
                    case "Node": var node=new Dictionary<string,object>();foreach(string k in row.Keys)if(k!="RecordType")node[k]=Text(row,k);node["SchemaVersion"]=1;node["ReleaseKey"]="readable";nodes.Add(node);break;
                    case "Parent": if(!parents.ContainsKey(key))parents.Add(key,new SortedDictionary<int,string>());parents[key].Add(Index(row,"ParentIndex"),Text(row,"ParentNodeId"));break;
                    case "Lane": if(!lanes.ContainsKey(tree))lanes.Add(tree,new SortedDictionary<int,string>());lanes[tree].Add(Index(row,"LaneIndex"),Text(row,"LaneName"));break;
                    default:throw new FormatException("Unknown research row type");
                }
            }
            var keys=new HashSet<string>();foreach(var node in nodes){string tree=(string)node["TreeKey"],key=tree+"/"+node["NodeId"];keys.Add(key);node["Parents"]=JsonMapper.ToJson(parents.TryGetValue(key,out var p)?new List<string>(p.Values):new List<string>());node["LaneNames"]=JsonMapper.ToJson(lanes.TryGetValue(tree,out var l)?new List<string>(l.Values):new List<string>());}
            foreach(var key in parents.Keys)if(!keys.Contains(key))throw new FormatException("Orphan research relation");
            return JsonMapper.ToObject(JsonMapper.ToJson(nodes));
        }
        static int Index(JsonData row,string key){double n=Number(row,key);if(n<0||n>int.MaxValue||n!=Math.Truncate(n))throw new FormatException(key);return (int)n;}
    }
}
