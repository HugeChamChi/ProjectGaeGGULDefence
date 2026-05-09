using System;
using System.Collections.Generic;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using UnityEngine;

public class GachaSystem<T> where T : IGachaData
{
    protected virtual GachaChanceCalculator _calculator { get; private set; }
    public IReadOnlyList<GachaChanceCalculator.ProbabilityItem> ProbabilityItems => _calculator.ProbabilityItems;

    private Func<int, T> _dataResolver;
    public int Cost { get; private set; }

    private readonly string _gachaTableName;
    private readonly string _costChartName;
    private const string GACHA_TABLE_NAME = "gachaTableName";
    private const string COST_AMOUNT = "costAmount";

    public GachaSystem(string gachaTableName, string costChartName, Func<int, T> dataResolver)
    {
        _gachaTableName = gachaTableName;
        _calculator = new GachaChanceCalculator(gachaTableName);
        _dataResolver = dataResolver;
        _costChartName = costChartName;
    }

    public async UniTask InitializeAsync()
    {
        await UniTask.WhenAll(
            _calculator.LoadChanceDataAsync(),
            LoadGachaCostAsync()
        );
    }

    public async UniTask<T[]> GetDatas(int count)
    {
        T[] results = new T[count];
        int[] selectedIds = await _calculator.GetRandomIDsAsync(count);

        for (int i = 0; i < count; i++)
        {
            if (_dataResolver != null)
            {
                results[i] = _dataResolver(selectedIds[i]);
            }
        }

        return results;
    }

    private async UniTask LoadGachaCostAsync()
    {
        try
        {
            JsonData rows = Chart.GetChartByName(_costChartName);
            if (rows != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];

                    // 데이터 존재 여부와 매칭 확인
                    if (row.ContainsKey(GACHA_TABLE_NAME) && row[GACHA_TABLE_NAME].ToString() == _gachaTableName)
                    {
                        if (row.ContainsKey(COST_AMOUNT))
                        {
                            Cost = int.Parse(row[COST_AMOUNT].ToString());
                            Debug.Log($"[Gacha] {_gachaTableName} 비용 로드 완료: {Cost}");
                            return;
                        }
                    }
                }
                Debug.LogWarning($"[Gacha] {_costChartName}에서 GACHA_TABLE_NAME '{_gachaTableName}'를 찾을 수 없습니다.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Gacha] 가챠 비용 파싱 중 오류 발생: {e.Message}");
        }
    }
}
