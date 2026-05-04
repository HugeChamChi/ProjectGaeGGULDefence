using UnityEngine;
using Cysharp.Threading.Tasks;
using System;
using GaeGGUL.Core;

public class Test_DailyReset : MonoBehaviour
{
    [Header("테스트 설정")]
    [SerializeField] private bool _runInitOnStart = true;

    private void Start()
    {
        if (_runInitOnStart)
        {
            FullInitialize().Forget();
        }
    }

    [ContextMenu("1. 전체 초기화 실행 (Full Initialize)")]
    public async UniTask FullInitialize()
    {
        Debug.Log("<color=yellow><b>[Test]</b></color> 전체 초기화 시작...");
        
        // 테이블 데이터 초기화 (아이템/상점 데이터 로드)
        await Table.InitializeAsync();

        // 플레이어 데이터 및 데일리/상점 매니저 초기화
        await Player.InitializeAsync();
        
        LogStatus();
    }

    [ContextMenu("2. 강제 리셋 테스트 (날짜 조작)")]
    public async UniTask ForceNewDayTest()
    {
        if (Player.PlayerData?.Data == null)
        {
            Debug.LogError("PlayerData가 초기화되지 않았습니다! 먼저 초기화를 실행하세요.");
            return;
        }

        Debug.Log("<color=cyan><b>[Test]</b></color> 날짜를 과거(2000-01-01)로 변경하여 리셋을 유도합니다...");

        // 1. 데이터를 과거 날짜로 조작
        Player.PlayerData.Data.LastResetDate = "2000-01-01";
        
        // 2. 서버에 저장 (리셋 전 상태 시뮬레이션)
        BackendGameData.Instance.GameDataUpdate(Player.PlayerData.Data);
        Debug.Log("날짜 조작 완료 및 서버 저장 성공. 1초 후 다시 초기화를 시도합니다...");
        
        await UniTask.Delay(1000);

        // 3. 다시 초기화 실행 (이때 DailyManager가 IsNewDay를 true로 판단해야 함)
        await Player.InitializeAsync();

        LogStatus();
        
        if (Player.Daily.IsNewDay)
            Debug.Log("<color=green><b>[Success]</b></color> 리셋 로직이 성공적으로 작동했습니다!");
        else
            Debug.LogError("[Fail] 리셋 로직이 작동하지 않았습니다. IsNewDay가 false입니다.");
    }

    [ContextMenu("3. 현재 상태 출력 (Log Status)")]
    public void LogStatus()
    {
        if (Player.PlayerData?.Data == null)
        {
            Debug.LogWarning("Player 데이터가 초기화되지 않았습니다.");
            return;
        }

        var data = Player.PlayerData.Data;
        var daily = Player.Daily;
        var shop = Player.Shop;

        Debug.Log("========================================");
        Debug.Log($"<b>[현재 서버 날짜]</b> {Server.GetServerTime():yyyy-MM-dd}");
        Debug.Log($"<b>[마지막 리셋 기록]</b> {data.LastResetDate}");
        Debug.Log($"<b>[오늘 첫 접속 여부]</b> {daily.IsNewDay}");
        
        if (shop != null && shop.Daily.DailyItems != null)
        {
            string items = "";
            foreach (var item in shop.Daily.DailyItems) items += $"[{item.ShopID}] ";
            Debug.Log($"<b>[상점 아이템 목록]</b> {items}");
        }
        Debug.Log("========================================");
    }
}
