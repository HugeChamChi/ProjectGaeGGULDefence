using Cysharp.Threading.Tasks;
using UnityEngine;

// 아이템, 유닛, 스테이지 데이터 등을 관리하는 테이블
public static class Table
{
    public static CouponManager Coupon { get; private set; } = new();
    public static ProfileItemManager Profile { get; private set; } = new();
    public static CharacterManager Character { get; private set; } = new();
    public static ItemManager Item { get; private set; } = new();
    public static GachaManager Gacha { get; private set; } = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static async UniTask InitializeAsync()
    {
        await UniTask.WhenAll(
            Profile.InitializeAsync(),
            Character.InitializeAsync(),
            Item.InitializeAsync()
        );

        // GachaManager는 CharacterManager의 데이터에 의존할 수 있으므로 (Data Resolver)
        // 안전하게 Character 초기화 완료 후 실행하거나, 혹은 병렬로 실행합니다.
        // 현재 GachaSystem의 InitializeAsync는 확률/비용만 로드하므로 병렬 실행이 가능합니다.
        await Gacha.InitializeAsync();
        
        Debug.Log("<color=green><b>Table</b></color> initialized successfully.");
    }
}