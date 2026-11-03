using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using GGD.Events;

var json = File.ReadAllText(args[0]);
EventDataConfig Config() => JsonSerializer.Deserialize<EventDataConfig>(json, new JsonSerializerOptions { IncludeFields = true });
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected rejection"); }
var config = Config(); config.MissionEvents[0].Enabled = true;
var runtime = new EventDataRuntime("account", config, 5);
var start = DateTimeOffset.Parse(config.MissionEvents[0].StartAt);
DataOccurrence Hit(string id, DateTimeOffset at, EventHookKey hook = EventHookKey.BattleEnd_Normal) => new DataOccurrence {AccountKey="account",OccurrenceKey=id,Hook=hook,OccurredAt=at,PlaySeconds=120,ReachedRound=20};
runtime.Apply(Hit("before", start.AddSeconds(-1)));
Check(runtime.GetProgress("total_all_30",start)==0,"Before event");
var updates=runtime.Apply(Hit("first",start));Check(updates.Count==4,"Normal routing");
Check(runtime.Apply(Hit("first",start)).Count==0,"Duplicate receipt");
Check(runtime.GetProgress("daily_normal_1",start)==1,"Daily progress");
Check(runtime.GetProgress("daily_normal_1",start.AddDays(1).AddSeconds(-1))==1,"Before reset");
Check(runtime.GetProgress("daily_normal_1",start.AddDays(1))==0,"Reset boundary");
runtime.Apply(Hit("hard",start,EventHookKey.BattleEnd_Hard));
Check(runtime.GetProgress("total_all_30",start)==2,"Shared all-mode data");
runtime.Apply(Hit("end",DateTimeOffset.Parse(config.MissionEvents[0].EndAt)));
Check(runtime.GetProgress("total_all_30",start)==2,"End exclusive");
var wrong=Hit("wrong",start);wrong.AccountKey="other";Reject(()=>runtime.Apply(wrong));
var bad=Config();bad.DataBindings[0].DataKey="Unknown";Reject(()=>EventDataRuntime.Validate(bad));
var disabled=new EventDataRuntime("account",Config(),5);disabled.Apply(Hit("disabled",start));Check(disabled.GetProgress("total_all_30",start)==0,"Disabled event");
config=Config();config.MissionEvents[0].Enabled=true;
config.Missions=new[]{new MissionDefinition{MissionKey="max",EventKey=config.MissionEvents[0].EventKey,Name="Max",DataKey="BestRound_Normal",Target=100,Period="Event",Rewards=new[]{new MissionReward{itemId="Gold",amount=1}}}};
var max=new EventDataRuntime("account",config,5);max.Apply(Hit("one",start));var lower=Hit("two",start);lower.ReachedRound=5;max.Apply(lower);Check(max.GetProgress("max",start)==20,"Max not sum");
Check(updates.Single(update=>update.DataKey==EventDataKey.PlaySeconds_Normal).Value==120,"Typed play seconds source");
Check(updates.Single(update=>update.DataKey==EventDataKey.BestRound_Normal).Value==20,"Typed reached round source");
Check(updates.Single(update=>update.DataKey==EventDataKey.BestRound_Normal).Operation==DataOperation.Max,"Typed aggregation");
foreach(var key in new[]{"Unknown","1","battlecount_normal"}) {var invalid=Config();invalid.DataBindings[0].DataKey=key;Reject(()=>EventDataRuntime.Validate(invalid));}
foreach(var key in new[]{"Unknown","1","battleend_normal"}) {var invalid=Config();invalid.DataBindings[0].HookKey=key;Reject(()=>EventDataRuntime.Validate(invalid));}
foreach(var key in new[]{"Unknown","0","constant"}) {var invalid=Config();invalid.DataBindings[0].ValueSource=key;Reject(()=>EventDataRuntime.Validate(invalid));}
var version=Config();version.CatalogVersion=EventDataCatalog.Version+1;Reject(()=>EventDataRuntime.Validate(version));
Reject(()=>runtime.Apply(Hit("invalid_hook",start,EventHookKey.None)));
var catalog=EventDataCatalog.Default;
void Immutable(Action change) {try{change();}catch(NotSupportedException){return;}throw new Exception("Catalog was mutable");}
Immutable(()=>((IList<DataDefinition>)catalog.Data).Clear());
Immutable(()=>((IList<HookDefinition>)catalog.Hooks).Clear());
Immutable(()=>((IList<EventValueSource>)catalog.Hooks[0].Sources).Clear());
Console.WriteLine("PASS: exported JSON -> C# runtime, routing, duplicate rejection, dates, daily reset, account isolation, disabled event, typed Add/Max/sources, immutable catalog, exact keys and version rejection");
