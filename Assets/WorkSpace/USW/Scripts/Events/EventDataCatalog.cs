using System;
using System.Collections.Generic;
using System.Linq;

namespace GGD.Events
{
    public enum EventDataKey { None, BattleCount_Normal, BattleCount_Hard, BattleCount_All, PlaySeconds_Normal, BestRound_Normal }
    public enum EventHookKey { None, BattleEnd_Normal, BattleEnd_Hard }
    public enum DataOperation { Add, Max }
    public enum EventValueSource { Constant, PlaySeconds, ReachedRound }

    // Immutable developer-owned definitions; the exporter executes this same catalog.
    public sealed class EventDataCatalog
    {
        public const int Version = 1;
        public static EventDataCatalog Default { get; } = new EventDataCatalog();
        public IReadOnlyList<DataDefinition> Data { get; }
        public IReadOnlyList<HookDefinition> Hooks { get; }
        private readonly Dictionary<string, DataDefinition> dataByKey;
        private readonly Dictionary<string, HookDefinition> hooksByKey;

        private EventDataCatalog()
        {
            Data = Array.AsReadOnly(new[] {
                new DataDefinition(EventDataKey.BattleCount_Normal, "일반 모드 전투 완료", DataOperation.Add, "회"),
                new DataDefinition(EventDataKey.BattleCount_Hard, "어려움 모드 전투 완료", DataOperation.Add, "회"),
                new DataDefinition(EventDataKey.BattleCount_All, "전체 모드 전투 완료", DataOperation.Add, "회"),
                new DataDefinition(EventDataKey.PlaySeconds_Normal, "일반 모드 플레이 시간", DataOperation.Add, "초"),
                new DataDefinition(EventDataKey.BestRound_Normal, "일반 모드 최고 도달 라운드", DataOperation.Max, "라운드")
            });
            Hooks = Array.AsReadOnly(new[] {
                new HookDefinition(EventHookKey.BattleEnd_Normal, "일반 모드 정상 종료", EventValueSource.Constant, EventValueSource.PlaySeconds, EventValueSource.ReachedRound),
                new HookDefinition(EventHookKey.BattleEnd_Hard, "어려움 모드 정상 종료", EventValueSource.Constant, EventValueSource.PlaySeconds, EventValueSource.ReachedRound)
            });
            dataByKey = Data.ToDictionary(definition => definition.Key.ToString(), StringComparer.Ordinal);
            hooksByKey = Hooks.ToDictionary(definition => definition.Key.ToString(), StringComparer.Ordinal);
        }

        // Wire keys are matched exactly: numeric enum strings and different casing are invalid.
        public DataDefinition FindData(string key) => key != null && dataByKey.TryGetValue(key, out var data) ? data : null;
        public HookDefinition FindHook(string key) => key != null && hooksByKey.TryGetValue(key, out var hook) ? hook : null;
        public HookDefinition FindHook(EventHookKey key) => Hooks.FirstOrDefault(hook => hook.Key == key);
    }

    public sealed class DataDefinition
    {
        public EventDataKey Key { get; }
        public string Name { get; }
        public DataOperation Operation { get; }
        public string Unit { get; }
        internal DataDefinition(EventDataKey key, string name, DataOperation operation, string unit)
        { Key = key; Name = name; Operation = operation; Unit = unit; }
    }

    public sealed class HookDefinition
    {
        public EventHookKey Key { get; }
        public string Name { get; }
        public IReadOnlyList<EventValueSource> Sources { get; }
        internal HookDefinition(EventHookKey key, string name, params EventValueSource[] sources)
        { Key = key; Name = name; Sources = Array.AsReadOnly((EventValueSource[])sources.Clone()); }
        public bool TryResolveSource(string key, out EventValueSource source)
        {
            foreach (var candidate in Sources)
                if (candidate.ToString() == key) { source = candidate; return true; }
            source = default;
            return false;
        }
    }
}
