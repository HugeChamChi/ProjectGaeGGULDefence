using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class Test_Initalizer : MonoBehaviour
{
    private void Awake()
    {
        StartAsync().Forget();
    }

    private async UniTask StartAsync()
    {
        // 0.1초 대기는 백엔드 초기화 등을 위한 최소한의 안전 장치
        await UniTask.WaitForSeconds(0.1f);

        // 중앙 집중식 초기화 호출 (순서 보장)
        await GaeGGUL.Core.GameInitializer.InitializeAsync();
    }
}