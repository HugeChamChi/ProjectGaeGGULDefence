using System;

/// <summary>Durable request/result envelope; preserves accepted rewards even if their account has changed.</summary>
[Serializable]
public sealed class CouponRedemptionJournal
{
    /// <summary>One-way code fingerprint for retry correlation.</summary>
    public string Fingerprint;
    /// <summary>Raw success response, persisted before balance mutation. Empty means outcome is unknown.</summary>
    public string Response;
}
