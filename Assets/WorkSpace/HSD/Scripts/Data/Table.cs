using Cysharp.Threading.Tasks;
using UnityEngine;

// 아이템, 유닛, 스테이지 데이터 등을 관리하는 테이블
public static class Table
{
    public static CouponManager Coupon { get; private set; } = new();
    public static ProfileItemManager Profile { get; private set; } = new();
    public static CharacterManager Character { get; private set; } = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static async UniTask InitializeAsync()
    {
        await Profile.InitializeAsync();
        Debug.Log("<color=green><b>Table</b></color> initialized successfully.");
    }
}