using System;

/// <summary>Server coupon identity and granted amounts, stored together with account balances.</summary>
[Serializable]
public sealed class CouponReceipt
{
    /// <summary>Server campaign UUID; one redemption per account.</summary>
    public string Id;
    /// <summary>Confirmed coupon mail title; older records may have no title.</summary>
    public string Title;
    /// <summary>Submitted code fingerprint for account-history duplicate checks.</summary>
    public string CodeFingerprint;
    /// <summary>Redemption UTC timestamp.</summary>
    public long RedeemedAt;
    /// <summary>Gold amount returned by the server.</summary>
    public int Gold;
    /// <summary>Diamond amount returned by the server.</summary>
    public int Diamond;
    /// <summary>Stamina amount returned by the server.</summary>
    public int Stamina;
}
