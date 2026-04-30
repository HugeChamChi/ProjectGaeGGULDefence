using System.Collections.Generic;
using BackEnd;
using Cysharp.Threading.Tasks;
/// <summary>
/// 뒤끝 차트(Chart)에서 데이터를 로드하는 프로바이더 (예시 구현)
/// </summary>
public class ServerDataProvider<T> : IDataProvider<T>
{
    private readonly string _chartName;

    public ServerDataProvider(string chartName)
    {
        _chartName = chartName;
    }

    public async UniTask<Dictionary<int, T>> LoadAsync()
    {
        var dictionary = new Dictionary<int, T>();
        
        // 뒤끝 차트 불러오기 로직 (실제 차트 구조에 따라 파싱 로직 필요)
        var bro = Backend.Chart.GetChartContents(_chartName);
        if (bro.IsSuccess())
        {
            // Json 파싱 및 객체 생성 로직...
        }

        return dictionary;
    }
}
