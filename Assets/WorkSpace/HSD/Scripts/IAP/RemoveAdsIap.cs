namespace GaeGGUL.IAP
{
    /// <summary>
    /// Non-consumable "remove ads" store product (once per account, permanent). It grants no currency,
    /// so the diamond and package catalogs skip it.
    /// </summary>
    public static class RemoveAdsIap
    {
        public const string ProductId = "remove_ads";
        public static bool IsRemoveAds(string productId) => productId == ProductId;
    }
}
