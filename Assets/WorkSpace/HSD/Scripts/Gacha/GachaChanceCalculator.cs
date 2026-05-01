using System;
using System.Collections.Generic;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using UnityEngine;

public class GachaChanceCalculator
{
    private readonly string _gachaTableName;
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
        bool isCompleted = false;

        Backend.Probability.GetProbability(_gachaTableName, callback =>
        {
            try
            {
                if (callback.IsSuccess())
                {
                    JsonData json = callback.GetFlattenJSON();
                    JsonData elements = json["elements"];

                    _probabilityItems.Clear();
                    _totalProbability = 0;

                    for (int i = 0; i < elements.Count; i++)
                    {
                        ProbabilityItem item = new ProbabilityItem
                        {
                            // 뒤끝 확률 테이블의 컬럼명에 맞춰 파싱 (itemID, itemName, percent)
                            // 뒤끝 데이터는 숫라도 문자열로 들어오는 경우가 많아 안전하게 파싱
                            itemID = int.Parse(elements[i]["itemID"].ToString()),
                            itemName = elements[i]["itemName"].ToString(),
                            percent = double.Parse(elements[i]["percent"].ToString())
                        };

                        _totalProbability += item.percent;
                        item.cumulativeProbability = _totalProbability;
                        _probabilityItems.Add(item);
                    }

                    Debug.Log($"[Gacha] {_gachaTableName} 확률 데이터 로드 완료. 아이템 수: {_probabilityItems.Count}, 총 확률: {_totalProbability}");
                }
                else
                {
                    Debug.LogError($"[Gacha] 확률 데이터 로드 실패: {callback.GetStatusCode()} - {callback.GetErrorMessage()}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Gacha] 확률 데이터 파싱 중 오류 발생: {e.Message}");
            }
            finally
            {
                isCompleted = true;
            }
        });

        await UniTask.WaitUntil(() => isCompleted);
    }

    public virtual int GetRandomId()
    {
        if (_probabilityItems.Count == 0)
        {
            Debug.LogError("[Gacha] 확률 데이터가 없습니다.");
            return -1;
        }

        double randomValue = UnityEngine.Random.value * _totalProbability;

        foreach (var item in _probabilityItems)
        {
            if (randomValue <= item.cumulativeProbability)
            {
                return item.itemID;
            }
        }

        return _probabilityItems[_probabilityItems.Count - 1].itemID;
    }
}
