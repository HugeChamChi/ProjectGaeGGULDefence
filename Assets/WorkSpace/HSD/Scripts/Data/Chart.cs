using System.Collections.Generic;
using BackEnd;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 뒤끝 서버의 차트 및 확률 ID 관리 클래스
public static class Chart
{
    private const string MASTER_CHART_ID = "238986";
    private const string CHART_NAME = "ChartName";
    private const string CHART_ID = "ChartID";

    private static Dictionary<string, string> _chartDict = new Dictionary<string, string>();

    public static async UniTask InitializeAsync()
    {
        var bro = await UniTask.RunOnThreadPool(() => Backend.Chart.GetChartContents(MASTER_CHART_ID));
        
        if (bro.IsSuccess())
        {
            var rows = bro.FlattenRows();
            _chartDict.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Keys.Contains(CHART_NAME) && rows[i].Keys.Contains(CHART_ID))
                {
                    string name = rows[i][CHART_NAME].ToString();
                    string id = rows[i][CHART_NAME].ToString();
                    _chartDict[name] = id;
                }
            }
            Debug.Log($"[Chart] Dynamic IDs loaded. Count: {_chartDict.Count}");
        }
        else
        {
            Debug.LogWarning($"[Chart] Failed to load Master Chart. Using fallback IDs. Error: {bro}");
        }
    }

    public static string GetID(string key, string defaultValue = null)
    {
        if (_chartDict.TryGetValue(key, out string id))
            return id;
        return defaultValue;
    }
}
