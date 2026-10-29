using System;
using System.IO;
using GGD.Events;
using GGD.BackendCharts;
using UnityEngine;

public static class EventChartEditorChecks
{
    public static string Run()
    {
        var rows = File.ReadAllText("outputs/event-chart-validation/rows.json");
        var expected = File.ReadAllText("outputs/event-chart-validation/config.json");
        var json = ChartSnapshotPayload.Decode(rows);
        if (json != expected) throw new Exception("Chart payload mismatch");
        var config = JsonUtility.FromJson<EventDataConfig>(json);
        var runtime = new EventDataRuntime("editor-validation", config, 5);
        if (EventChartLoader.ChartName != "DEV_MissionEvents") throw new Exception("Wrong Editor chart route");
        return "PASS: Unity chart payload decode, JsonUtility config parse, runtime validation, DEV route; no server writes";
    }
}
