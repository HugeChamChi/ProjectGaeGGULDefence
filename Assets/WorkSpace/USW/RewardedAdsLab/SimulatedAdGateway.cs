using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace GGD.RewardedAdsLab
{
    // IN-MEMORY TEST AUTHORITY ONLY. No BackendManager, Player, PlayerPrefs or network access.
    [Serializable]
    public sealed class LabLedger
    {
        public List<AdRequest> Requests = new List<AdRequest>();
        public List<string> Transactions = new List<string>();
        public int Gold, Tokens, Stamina, DailyRevision, WeeklyRevision;
        public int DailyRefreshCredits, WeeklyRefreshCredits;
        public bool DailyFreePurchased, WeeklyFreePurchased;
    }

    public sealed class SimulatedAdGateway : IRewardedAdGateway
    {
        public readonly string Account;
        public readonly LabLedger Ledger;
        public DateTime UtcNow = new DateTime(2026, 11, 14, 3, 0, 0, DateTimeKind.Utc);
        // Test fixture: represents an already committed final battle settlement.
        public string SettledRun = "lab-run-1";
        public int SettledGold = 120, SettledTokens = 2;
        public bool FailNextCommit;

        public SimulatedAdGateway(string account = "lab-account", string savedJson = null)
        {
            Account = account;
            Ledger = savedJson == null ? new LabLedger() : JsonUtility.FromJson<LabLedger>(savedJson);
        }
        public string Snapshot() => JsonUtility.ToJson(Ledger);
        private static AdRequest Copy(AdRequest r) => JsonUtility.FromJson<AdRequest>(JsonUtility.ToJson(r));
        private void CheckAccount(string account)
        { if (account != Account) throw new InvalidOperationException("Account mismatch."); }
        private AdRequest Get(string account, string id)
        {
            CheckAccount(account);
            return Ledger.Requests.Find(r => r.Id == id) ?? throw new InvalidOperationException("Unknown request.");
        }
        public string Period(Placement placement) => GGD.Periods.PeriodSerialization.Key(UtcNow, placement == Placement.WeeklyShopRefresh);
        public int Used(Placement placement)
        {
            string period = Period(placement);
            return Ledger.Requests.FindAll(r => r.Placement == placement && r.Period == period && r.Status == RequestStatus.Granted).Count;
        }
        public Task<AdRequest> RegisterAsync(string account, Placement placement, RewardChoice choice, string runId)
        {
            CheckAccount(account);
            if (!Enum.IsDefined(typeof(Placement), placement) || !Enum.IsDefined(typeof(RewardChoice), choice))
                throw new InvalidOperationException("Unknown placement or reward.");
            if (Ledger.Requests.Exists(r => r.Status == RequestStatus.Registered || r.Status == RequestStatus.AwaitingVerification))
                throw new InvalidOperationException("Resolve the pending request first.");
            if (placement == Placement.BattleRewardDouble)
            {
                if (string.IsNullOrEmpty(runId) || runId != SettledRun || SettledGold < 0 || SettledTokens < 0 || (SettledGold == 0 && SettledTokens == 0))
                    throw new InvalidOperationException("No eligible settled battle reward.");
                if (Ledger.Requests.Exists(r => r.Placement == placement && r.RunId == runId && r.Status == RequestStatus.Granted))
                    throw new InvalidOperationException("Battle reward already doubled.");
            }
            else if (Used(placement) >= (placement == Placement.DailyReward ? 3 : 1))
                throw new InvalidOperationException("Placement limit reached.");
            var r = new AdRequest { Id = Guid.NewGuid().ToString("N"), Account = account, Placement = placement,
                Period = Period(placement), RunId = runId, Status = RequestStatus.Registered };
            // Placeholder amounts, deliberately unrelated to the live balance chart.
            if (placement == Placement.StaminaRefill) r.Stamina = 10;
            if (placement == Placement.DailyReward) { r.Gold = choice == RewardChoice.Gold ? 100 : 0; r.Tokens = choice == RewardChoice.UpgradeToken ? 2 : 0; }
            if (placement == Placement.BattleRewardDouble) { r.Gold = SettledGold; r.Tokens = SettledTokens; }
            r.StockRevision = placement == Placement.WeeklyShopRefresh ? Ledger.WeeklyRevision : Ledger.DailyRevision;
            Ledger.Requests.Add(r);
            return Task.FromResult(Copy(r));
        }
        public Task<AdRequest> ObserveAsync(string account, string requestId, AdOutcome outcome)
        {
            var r = Get(account, requestId);
            if (r.Status == RequestStatus.Registered)
                r.Status = outcome == AdOutcome.Earned ? RequestStatus.AwaitingVerification : RequestStatus.Cancelled;
            return Task.FromResult(Copy(r));
        }
        public Task<AdRequest> FindAsync(string account, string requestId) => Task.FromResult(Copy(Get(account, requestId)));

        // Test-only stand-in for verified server callback. Never expose this operation to production UI.
        public bool Verify(string id, string transactionId)
        {
            var r = Get(Account, id);
            if (r.Status == RequestStatus.Granted || r.Status == RequestStatus.Cancelled || string.IsNullOrWhiteSpace(transactionId) || Ledger.Transactions.Contains(transactionId)) return false;
            if (FailNextCommit) { FailNextCommit = false; return false; }
            int gold = checked(Ledger.Gold + r.Gold), tokens = checked(Ledger.Tokens + r.Tokens), stamina = checked(Ledger.Stamina + r.Stamina);
            if (r.Placement == Placement.DailyShopRefresh && r.StockRevision != Ledger.DailyRevision) return false;
            if (r.Placement == Placement.WeeklyShopRefresh && r.StockRevision != Ledger.WeeklyRevision) return false;
            // All test state below commits synchronously, with no partial failure injection.
            Ledger.Gold = gold; Ledger.Tokens = tokens; Ledger.Stamina = stamina;
            if (r.Placement == Placement.DailyShopRefresh) { if (r.Period == Period(r.Placement)) Ledger.DailyRevision++; else Ledger.DailyRefreshCredits++; }
            if (r.Placement == Placement.WeeklyShopRefresh) { if (r.Period == Period(r.Placement)) Ledger.WeeklyRevision++; else Ledger.WeeklyRefreshCredits++; }
            Ledger.Transactions.Add(transactionId);
            r.Status = RequestStatus.Granted;
            return true;
        }
    }

    public sealed class SimulatedAdAdapter : IRewardedAdAdapter
    {
        public AdOutcome NextOutcome = AdOutcome.Earned;
        public Task<AdOutcome> ShowAsync(AdRequest request) => Task.FromResult(NextOutcome);
    }
}
