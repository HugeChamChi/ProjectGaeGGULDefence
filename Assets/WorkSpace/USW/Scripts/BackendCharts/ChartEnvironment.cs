namespace GGD.BackendCharts
{
    /// <summary>
    /// BACKND chart naming rule (user decision 2026-11-12): every chart and probability card is named
    /// <c>DEV_Content</c> or <c>LIVE_Content</c>. Editor, Development Build and INTERNAL_TEST_BUILD read DEV_;
    /// every other player build reads LIVE_. Chosen at compile time: no runtime switch and no fallback to the other environment.
    /// </summary>
    public static class ChartEnvironment
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || INTERNAL_TEST_BUILD
        public const string Prefix = "DEV_";
#else
        public const string Prefix = "LIVE_";
#endif
    }
}
