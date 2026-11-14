using System;
using System.Threading.Tasks;

namespace GGD.RewardedAdsLab
{
    public enum Placement { StaminaRefill, BattleRewardDouble, DailyReward, DailyShopRefresh, WeeklyShopRefresh }
    public enum RewardChoice { Gold, UpgradeToken }
    public enum RequestStatus { Registered, AwaitingVerification, Granted, Cancelled }
    public enum AdOutcome { Earned, Skipped, LoadFailed, ShowFailed }

    [Serializable]
    public sealed class AdRequest
    {
        public string Id, Account, Period, RunId;
        public Placement Placement;
        public int Gold, Tokens, Stamina, StockRevision;
        public RequestStatus Status;
    }

    // Production implementation registers immutable rewards on the server, never from UI amounts.
    public interface IRewardedAdGateway
    {
        Task<AdRequest> RegisterAsync(string account, Placement placement, RewardChoice choice, string runId);
        Task<AdRequest> ObserveAsync(string account, string requestId, AdOutcome outcome);
        Task<AdRequest> FindAsync(string account, string requestId);
    }

    // Adapter must resolve once per showing, after reward/close ordering is settled.
    // It initializes/loads/shows/disposes SDK objects and passes request ID as SSV custom_data.
    public interface IRewardedAdAdapter
    {
        Task<AdOutcome> ShowAsync(AdRequest registeredRequest);
    }

    public sealed class RewardedAdService
    {
        private readonly IRewardedAdGateway gateway;
        private readonly IRewardedAdAdapter adapter;
        private bool busy;
        public RewardedAdService(IRewardedAdGateway gateway, IRewardedAdAdapter adapter)
        { this.gateway = gateway; this.adapter = adapter; }

        public async Task<AdRequest> ShowAsync(string account, Placement placement, RewardChoice choice, string runId)
        {
            if (busy) throw new InvalidOperationException("An ad is already in progress.");
            busy = true;
            try
            {
                var request = await gateway.RegisterAsync(account, placement, choice, runId);
                // Copy prevents the adapter from modifying the authoritative request.
                var outcome = await adapter.ShowAsync(request);
                return await gateway.ObserveAsync(account, request.Id, outcome);
            }
            finally { busy = false; }
        }

        public Task<AdRequest> FindAsync(string account, string requestId) => gateway.FindAsync(account, requestId);
    }
}
