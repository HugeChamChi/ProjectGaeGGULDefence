using System;
using Cysharp.Threading.Tasks;

// 아이템, 유닛, 스테이지 데이터 등을 관리하는 테이블
public static class Table
{
    public static CouponManager Coupon { get; private set; } = new();

    public static async UniTask InitializeAsync()
    {
        
    }
}