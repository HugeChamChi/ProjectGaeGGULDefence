using System;
using System.Collections.Generic;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using UnityEngine;

public class GachaChanceCalculator
{
    private readonly string _gachaTableName;
    private string _gachaTableID;
    public IReadOnlyList<ProbabilityItem> ProbabilityItems => _probabilityItems;
    private List<ProbabilityItem> _probabilityItems = new List<ProbabilityItem>();
    private double _totalProbability = 0;

    public struct ProbabilityItem
    {
        public int itemID;
        public string itemName;
        public double percent;
        public double cumulativeProbability;
    }

    public GachaChanceCalculator(string gachaTableName)
    {
        _gachaTableName = gachaTableName;
    }

    public async UniTask LoadChanceDataAsync()
    {
        _probabilityItems.Clear();
        _totalProbability = 0;

        _gachaTableID = Chart.GetProbabilityIdByName(_gachaTableName);
        JsonData rows = Chart.GetProbabilityDataByName(_gachaTableName);

        if (rows == null)
        {
            Debug.LogError($"[Gacha] 확률 데이터 로드 실패: {_gachaTableName}");
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            if (!row.IsObject || 
                !row.Keys.Contains("itemID") ||
                !row.Keys.Contains("itemName") ||
                !row.Keys.Contains("percent"))
            {
                Debug.LogWarning($"[Gacha] row[{i}] 데이터 누락 또는 형식 오류, 스킵합니다.");
                continue;
            }

            if (!int.TryParse(row["itemID"].ToString(), out int itemID) ||
                !double.TryParse(row["percent"].ToString(), out double percent))
            {
                Debug.LogWarning($"[Gacha] row[{i}] 파싱 실패, 스킵합니다.");
                continue;
            }

            _totalProbability += percent;

            _probabilityItems.Add(new ProbabilityItem
            {
                itemID = itemID,
                itemName = row["itemName"].ToString(),
                percent = percent,
                cumulativeProbability = _totalProbability
            });
        }

        Debug.Log($"[Gacha] {_gachaTableName} 로드 완료 | 항목 수: {_probabilityItems.Count}, 총 확률: {_totalProbability}");
    }

    // 1회 뽑기 - 서버 확률 사용
    public UniTask<int> GetRandomIDAsync()
    {
        var tcs = new UniTaskCompletionSource<int>();

        Backend.Probability.GetProbability(_gachaTableID, callback =>
        {
            if (!callback.IsSuccess())
            {
                Debug.LogError($"[Gacha] 뽑기 실패: {callback}");
                tcs.TrySetResult(-1);
                return;
            }

            JsonData result = callback.FlattenRows();

            if (result == null || !result.IsArray || result.Count == 0)
            {
                Debug.LogError("[Gacha] 뽑기 결과가 비어있습니다.");
                tcs.TrySetResult(-1);
                return;
            }

            var firstRow = result[0];
            if (firstRow.IsObject && firstRow.Keys.Contains("itemID") &&
                int.TryParse(firstRow["itemID"].ToString(), out int itemID))
            {
                tcs.TrySetResult(itemID);
            }
            else
            {
                tcs.TrySetResult(-1);
            }
        });

        return tcs.Task;
    }

    // N회 뽑기 - 서버 확률 사용
    public UniTask<int[]> GetRandomIDsAsync(int count)
    {
        var tcs = new UniTaskCompletionSource<int[]>();

        Backend.Probability.GetProbabilitys(_gachaTableID, count, callback =>
        {
            if (!callback.IsSuccess())
            {
                Debug.LogError($"[Gacha] 다중 뽑기 실패: {callback}");
                tcs.TrySetResult(new int[count]); 
                return;
            }

            string rawJson = callback.GetReturnValue();
            Debug.Log($"[Gacha] Raw Response: {rawJson}");

            JsonData json = callback.GetReturnValuetoJSON();
            JsonData results = null;

            if (json.Keys.Contains("elements"))
            {
                results = json["elements"];
            }
            else
            {
                results = callback.FlattenRows();
            }

            if (results == null || !results.IsArray || results.Count == 0)
            {
                Debug.LogError($"[Gacha] 다중 뽑기 결과 데이터 오류. Flattened: {results?.ToJson() ?? "null"}");
                tcs.TrySetResult(new int[count]);
                return;
            }

            var ids = new int[results.Count];

            for (int i = 0; i < results.Count; i++)
            {
                var row = results[i];
                if (row.IsObject)
                {
                    // 뒤끝 데이터는 대문자/소문자 구분이 있을 수 있으므로 둘 다 확인
                    string key = row.Keys.Contains("itemID") ? "itemID" : (row.Keys.Contains("itemid") ? "itemid" : null);

                    if (key != null)
                    {
                        string idStr = row[key].ToString();
                        
                        // {"N":"123"} 또는 {"S":"123"} 구조 대응
                        if (row[key].IsObject)
                        {
                            if (row[key].Keys.Contains("N")) idStr = row[key]["N"].ToString();
                            else if (row[key].Keys.Contains("S")) idStr = row[key]["S"].ToString();
                        }
                        
                        if (int.TryParse(idStr, out int itemID))
                        {
                            ids[i] = itemID;
                        }
                        else
                        {
                            Debug.LogWarning($"[Gacha] row[{i}]의 {key} 파싱 실패: {idStr}");
                            ids[i] = -1;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[Gacha] row[{i}]에 itemID 필드가 없습니다. Keys: {string.Join(", ", row.Keys)}");
                        ids[i] = -1;
                    }
                }
                else
                {
                    Debug.LogWarning($"[Gacha] row[{i}]가 오브젝트 형식이 아닙니다: {row.ToJson()}");
                    ids[i] = -1;
                }
            }

            tcs.TrySetResult(ids);
        });

        return tcs.Task;
    }
}
