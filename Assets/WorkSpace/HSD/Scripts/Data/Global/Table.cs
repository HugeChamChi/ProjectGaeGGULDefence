using Cysharp.Threading.Tasks;
using UnityEngine;

// 아이템, 유닛, 스테이지 데이터 등을 관리하는 테이블
public static class Table
{
    public static CouponManager Coupon          { get; private set; } = new();
    public static ProfileItemTable Profile      { get; private set; } = new();
    public static CharacterTable Character      { get; private set; } = new();
    public static ItemTable Item                { get; private set; } = new();
    public static ShopTables Shop               { get; private set; } = new();
    public static ProjectileTable Projectile    { get; private set; } = new();

    public static async UniTask InitializeAsync()
    {
        // 1. 차트 ID를 먼저 로드합니다.
        await Chart.InitializeAsync();

        // 2. 로드된 ID를 사용하여 나머지 테이블들을 초기화합니다.
        await UniTask.WhenAll(
            Profile.InitializeAsync(),
            Character.InitializeAsync(),
            Item.InitializeAsync(),
            Shop.InitializeAsync(),
            Projectile.InitializeAsync()
        );

        Debug.Log("<color=green><b>Table</b></color> initialized successfully.");
    }
}