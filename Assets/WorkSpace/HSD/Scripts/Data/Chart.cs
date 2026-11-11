using System.Collections.Generic;
using BackEnd;
using LitJson;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 뒤끝 서버의 확률 ID 관리 클래스.
// 차트 내용은 GGD.BackendCharts.BackendChartReader(새 차트 API)로 읽는다. 옛 Backend.Chart 호출은 2026-11-11 제거했다.
public static class Chart
{
    // 이름(Key)과 ID(Value)를 매핑하는 딕셔너리
    private static Dictionary<string, string> _probNameIdMap = new Dictionary<string, string>();

    // 이미 로드한 데이터(JSON)를 담아두는 캐시
    private static Dictionary<string, JsonData> _probDataCache = new Dictionary<string, JsonData>();

    public static async UniTask InitializeAsync()
    {
        _probNameIdMap.Clear();
        _probDataCache.Clear();

        var probabilities = new UniTaskCompletionSource<BackendReturnObject>();
        Backend.Probability.GetProbabilityCardListV2(result => probabilities.TrySetResult(result));
        var probBro = await probabilities.Task;

        // 확률 리스트 로드
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

        Debug.Log($"[Chart] Loaded {_probNameIdMap.Count} Probabilities.");
    }

    /// <summary>
    /// 확률 테이블 이름으로 실제 데이터(JsonData)를 가져옵니다.
    /// </summary>
    public static async UniTask<JsonData> GetProbabilityDataByNameAsync(string probName)
    {
        if (!_probNameIdMap.TryGetValue(probName, out string probId))
        {
            Debug.LogError($"[Chart] 존재하지 않는 확률 이름입니다: {probName}");
            return null;
        }

        if (_probDataCache.TryGetValue(probId, out JsonData cachedData))
            return cachedData;

        var completion = new UniTaskCompletionSource<BackendReturnObject>();
        Backend.Probability.GetProbabilityContents(probId, result => completion.TrySetResult(result));
        var bro = await completion.Task;
        if (bro.IsSuccess())
        {
            JsonData data = bro.FlattenRows();
            _probDataCache[probId] = data;
            return data;
        }

        Debug.LogError($"[Chart] {probName}({probId}) 데이터 로드 실패");
        return null;
    }

    public static string GetProbabilityIdByName(string probName) => _probNameIdMap.GetValueOrDefault(probName);
}
