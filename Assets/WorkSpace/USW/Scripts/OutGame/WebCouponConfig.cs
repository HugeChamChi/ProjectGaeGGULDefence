using UnityEngine;

/// <summary>Published BACKND web coupon page. This asset contains no login credentials.</summary>
[CreateAssetMenu(menuName = "Game/Web Coupon", fileName = "WebCouponConfig")]
public sealed class WebCouponConfig : ScriptableObject
{
    /// <summary>Console-generated HTTPS coupon.html URL; the current UID is added at launch.</summary>
    [TextArea(3, 6)] public string PageUrl;
}
