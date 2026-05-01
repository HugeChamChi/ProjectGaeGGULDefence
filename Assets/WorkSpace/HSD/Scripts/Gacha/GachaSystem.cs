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

    private async UniTask LoadGachaCostAsync()
    {
        bool isCompleted = false;

        Backend.Chart.GetChartContents(_costChartName, callback =>
        {
            try
            {
                if (callback.IsSuccess())
                {
                    JsonData rows = callback.FlattenRows();
                    for (int i = 0; i < rows.Count; i++)
                    {
                        if (rows[i].ContainsKey("gachaID") && rows[i]["gachaID"].ToString() == _gachaTableName)
                        {
                            if (rows[i].ContainsKey("costAmount"))
                            {
                                Cost = int.Parse(rows[i]["costAmount"].ToString());
                                Debug.Log($"[Gacha] {_gachaTableName} 비용 로드 완료: {Cost}");
                            }
                            return;
                        }
                    }
                    Debug.LogWarning($"[Gacha] {_costChartName}에서 gachaID '{_gachaTableName}'를 찾을 수 없습니다.");
                }
                else
                {
                    Debug.LogError($"[Gacha] 가챠 비용 차트 로드 실패: {callback.GetStatusCode()} - {callback.GetErrorMessage()}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Gacha] 가챠 비용 파싱 중 오류 발생: {e.Message}");
            }
            finally
            {
                isCompleted = true;
            }
        });

        await UniTask.WaitUntil(() => isCompleted);
    }

    public T Draw()
    {
        int selectedId = _calculator.GetRandomId();
        if (_dataResolver != null)
        {
            return _dataResolver(selectedId);
        }
        return default;
    }
}
