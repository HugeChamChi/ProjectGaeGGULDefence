using System.Collections.Generic;
using BackEnd;
using LitJson;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 뒤끝 서버의 차트 및 확률 ID 관리 클래스
public static class Chart
{
    // 이름(Key)과 ID(Value)를 매핑하는 딕셔너리
    private static Dictionary<string, string> _chartNameIdMap = new Dictionary<string, string>();
    private static Dictionary<string, string> _probNameIdMap = new Dictionary<string, string>();

    // 이미 로드한 데이터(JSON)를 담아두는 캐시
    private static Dictionary<string, JsonData> _chartDataCache = new Dictionary<string, JsonData>();
    private static Dictionary<string, JsonData> _probDataCache = new Dictionary<string, JsonData>();

    public static async UniTask InitializeAsync()
    {
        _chartNameIdMap.Clear();
        _probNameIdMap.Clear();
        _chartDataCache.Clear();
        _probDataCache.Clear();

        // 1. 차트 리스트 로드
        var chartBro = Backend.Chart.GetChartListV2();
        if (chartBro.IsSuccess())
        {
            JsonData rows = chartBro.FlattenRows();

            for (int i = 0; i < rows.Count; i++)
            {
                _chartNameIdMap[rows[i]["chartName"].ToString()] = rows[i]["selectedChartFileId"].ToString();
                Debug.Log($"{rows[i]["chartName"].ToString()}_{rows[i]["selectedChartFileId"]}");
            }
        }
        else
        {
            Debug.Log("Chart Error");
        }

        // 2. 확률 리스트 로드
        var probBro = Backend.Probability.GetProbabilityCardListV2();
        if (probBro.IsSuccess())
        {
            JsonData rows = probBro.FlattenRows();

            for (int i = 0; i < rows.Count; i++)
            {
                _probNameIdMap[rows[i]["probabilityName"].ToString()] = rows[i]["selectedProbabilityFileId"].ToString();
                Debug.Log($"{rows[i]["probabilityName"].ToString()}_{rows[i]["selectedProbabilityFileId"]}");
            }
        }
        else
        {
            Debug.Log("Probability Error");
        }

        Debug.Log($"[Chart] Loaded {_chartNameIdMap.Count} Charts, {_probNameIdMap.Count} Probabilities.");
    }

    /// <summary>
    /// 차트 이름으로 실제 데이터(JsonData)를 가져옵니다.
    /// </summary>
    public static JsonData GetChartByName(string chartName)
    {
        if (!_chartNameIdMap.TryGetValue(chartName, out string chartId))
        {
            Debug.LogError($"[Chart] 존재하지 않는 차트 이름입니다: {chartName}");
            return null;
        }

        if (_chartDataCache.TryGetValue(chartId, out JsonData cachedData))
            return cachedData;

        var bro = Backend.Chart.GetChartContents(chartId);
        if (bro.IsSuccess())
        {
            JsonData data = bro.FlattenRows();
            _chartDataCache[chartId] = data;
            return data;
        }

        Debug.LogError($"[Chart] {chartName}({chartId}) 데이터 로드 실패");
        return null;
    }

    /// <summary>
    /// 확률 테이블 이름으로 실제 데이터(JsonData)를 가져옵니다.
    /// </summary>
    public static JsonData GetProbabilityDataByName(string probName)
    {
        if (!_probNameIdMap.TryGetValue(probName, out string probId))
        {
            Debug.LogError($"[Chart] 존재하지 않는 확률 이름입니다: {probName}");
            return null;
        }

        if (_probDataCache.TryGetValue(probId, out JsonData cachedData))
            return cachedData;

        var bro = Backend.Probability.GetProbabilityContents(probId);
        if (bro.IsSuccess())
        {
            JsonData data = bro.FlattenRows();
            _probDataCache[probId] = data;
            return data;
        }

        Debug.LogError($"[Chart] {probName}({probId}) 데이터 로드 실패");
        return null;
    }

    public static string GetChartIdByName(string chartName) => _chartNameIdMap.GetValueOrDefault(chartName);
    public static string GetProbabilityIdByName(string probName) => _probNameIdMap.GetValueOrDefault(probName);
    
    // 기존 GetID 호환용
    public static string GetID(string name) => GetChartIdByName(name);
}
