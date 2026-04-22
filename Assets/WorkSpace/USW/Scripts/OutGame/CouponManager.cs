using UnityEngine;
using BackEnd;

public class CouponManager : MonoBehaviour
{
    public static CouponManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 쿠폰 코드 사용. 성공 시 우편함 자동 갱신.
    /// </summary>
    public void UseCoupon(string couponCode, System.Action<bool, string> callback = null)
    {
        if (string.IsNullOrEmpty(couponCode))
        {
            callback?.Invoke(false, "쿠폰 코드를 입력해주세요");
            return;
        }

        if (!BackendManager.Instance.IsLoggedIn()) return;

        var bro = Backend.Coupon.UseCoupon(couponCode);

        if (bro.IsSuccess())
        {
            Debug.Log("쿠폰 사용 성공 : " + bro);
            callback?.Invoke(true, "쿠폰 사용 성공! 우편함을 확인해주세요");
            MailManager.Instance.GetPostList(PostType.Coupon);
        }
        else
        {
            string msg = bro.StatusCode switch
            {
                404 => "존재하지 않는 쿠폰입니다",
                409 => "이미 사용한 쿠폰입니다",
                410 => "만료된 쿠폰입니다",
                _   => "쿠폰 사용 실패"
            };
            Debug.LogError("쿠폰 사용 실패 : " + bro);
            callback?.Invoke(false, msg);
        }
    }
}
