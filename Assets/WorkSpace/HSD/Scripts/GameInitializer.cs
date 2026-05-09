using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GaeGGUL.Core
{
    /// <summary>
    /// 게임 시작 시 필요한 모든 정적 데이터와 유저 데이터를 
    /// 올바른 순서대로 초기화하는 중앙 제어 클래스입니다.
    /// </summary>
    public static class GameInitializer
    {
        public static bool IsInitialized { get; private set; }

        public static async UniTask InitializeAsync()
        {
            if (IsInitialized)
            {
                Debug.Log("Non Init");
                return;
            }

            Debug.Log("<color=cyan>[GameInitializer]</color> 시작 중...");

            // 1. 차트 ID 로드 (모든 데이터의 기초)
            await Chart.InitializeAsync();
            Debug.Log("<color=cyan>[GameInitializer]</color> Chart 초기화 완료");

            // 2. 테이블 데이터 로드 (게임 정보)
            await Table.InitializeAsync();
            Debug.Log("<color=cyan>[GameInitializer]</color> Table 초기화 완료");

            // 3. 플레이어 데이터 로드 (유저 정보)
            // Player.InitializeAsync는 내부적으로 DailyManager를 포함함
            await Player.InitializeAsync();
            Debug.Log("<color=cyan>[GameInitializer]</color> Player 초기화 완료");

            IsInitialized = true;
            Debug.Log("<color=green>[GameInitializer] 모든 초기화 완료!</color>");
        }
    }
}