using System;
using System.Threading.Tasks;
using GGD.RewardedAdsLab;
using UnityEngine;

public static class RewardedAdsLabChecks
{
    private static int checks;
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    private static async Task Blocked(Func<Task> action, string label)
    {
        bool rejected = false;
        try { await action(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, label);
    }
    public static async Task<string> Run()
    {
        checks = 0;
        var g = new SimulatedAdGateway(); var a = new SimulatedAdAdapter(); var service = new RewardedAdService(g, a);
        const string account = "lab-account";
        async Task<AdRequest> Show(Placement p, RewardChoice c = RewardChoice.Gold, string run = "lab-run-1") => await service.ShowAsync(account, p, c, run);
        void Commit(AdRequest r) => Check(g.Verify(r.Id, "tx-" + r.Id), "verified grant");
        foreach (AdOutcome failed in new[] { AdOutcome.Skipped, AdOutcome.LoadFailed, AdOutcome.ShowFailed })
        {
            a.NextOutcome = failed; var r = await Show(Placement.StaminaRefill);
            Check(r.Status == RequestStatus.Cancelled && g.Used(Placement.StaminaRefill) == 0 && g.Ledger.Stamina == 0, "failure has no reward or usage");
        }
        a.NextOutcome = AdOutcome.Earned;
        var stamina = await Show(Placement.StaminaRefill);
        Check(stamina.Status == RequestStatus.AwaitingVerification && g.Ledger.Stamina == 0, "reward callback is not grant");
        await Blocked(() => Show(Placement.DailyReward), "pending locks all placements");
        Commit(stamina);
        Check(!g.Verify(stamina.Id, "different-tx") && g.Ledger.Stamina == 10, "duplicate request does not pay twice");
        await Blocked(() => Show(Placement.StaminaRefill), "stamina daily max one");
        for (int i = 0; i < 3; i++) Commit(await Show(Placement.DailyReward, i == 1 ? RewardChoice.UpgradeToken : RewardChoice.Gold));
        Check(g.Ledger.Gold == 200 && g.Ledger.Tokens == 2, "selected daily rewards");
        await Blocked(() => Show(Placement.DailyReward, RewardChoice.UpgradeToken), "daily choices share max three");
        var battle = await Show(Placement.BattleRewardDouble); g.SettledGold = 999;
        Commit(battle);
        Check(g.Ledger.Gold == 320 && g.Ledger.Tokens == 4, "battle uses final frozen snapshot");
        g.UtcNow = g.UtcNow.AddDays(1);
        await Blocked(() => Show(Placement.BattleRewardDouble), "run receipt survives daily reset");
        await Blocked(() => Show(Placement.BattleRewardDouble, RewardChoice.Gold, "unknown"), "unsettled run blocked");
        var pending = await Show(Placement.StaminaRefill);
        g.FailNextCommit = true;
        Check(!g.Verify(pending.Id, "retry-tx") && g.Ledger.Stamina == 10 && g.Used(Placement.StaminaRefill) == 0, "failed atomic commit consumes nothing");
        var restored = new SimulatedAdGateway(account, g.Snapshot()) { UtcNow = g.UtcNow };
        Check((await restored.FindAsync(account, pending.Id)).Status == RequestStatus.AwaitingVerification, "restart retains pending");
        Check(restored.Verify(pending.Id, "retry-tx") && restored.Ledger.Stamina == 20, "retry without another ad");
        await Blocked(() => restored.FindAsync("other-account", pending.Id), "account isolation");
        Commit(pending);
        g.Ledger.DailyFreePurchased = true; g.Ledger.WeeklyFreePurchased = true;
        Commit(await Show(Placement.DailyShopRefresh)); Commit(await Show(Placement.WeeklyShopRefresh));
        Check(g.Ledger.DailyRevision == 1 && g.Ledger.WeeklyRevision == 1 && g.Ledger.DailyFreePurchased && g.Ledger.WeeklyFreePurchased, "refresh leaves fixed purchase flags");
        await Blocked(() => Show(Placement.DailyShopRefresh), "daily refresh limit");
        await Blocked(() => Show(Placement.WeeklyShopRefresh), "weekly refresh limit");
        g.UtcNow = new DateTime(2026, 11, 16, 19, 59, 59, DateTimeKind.Utc);
        var late = await Show(Placement.DailyShopRefresh);
        string oldPeriod = late.Period;
        g.UtcNow = g.UtcNow.AddSeconds(1);
        Commit(late);
        Check(g.Period(Placement.DailyShopRefresh) != oldPeriod && g.Used(Placement.DailyShopRefresh) == 0 && g.Ledger.DailyRefreshCredits == 1, "05 KST boundary defers refresh without overwriting stock");
        Check(g.Period(Placement.WeeklyShopRefresh) == "2026-11-16" && g.Used(Placement.WeeklyShopRefresh) == 0, "Monday 05 KST weekly reset");
        var early = await g.RegisterAsync(account, Placement.DailyReward, RewardChoice.Gold, null);
        Commit(early);
        Check((await g.ObserveAsync(account, early.Id, AdOutcome.Skipped)).Status == RequestStatus.Granted, "SSV before client close remains granted");
        var duplicateTx = await Show(Placement.DailyReward);
        Check(!g.Verify(duplicateTx.Id, "tx-" + early.Id), "transaction reuse rejected");
        Check(g.Ledger.Requests.Find(r => r.Id == duplicateTx.Id).Status == RequestStatus.AwaitingVerification, "duplicate transaction preserves pending");
        string result = $"PASS {checks} rewarded ads contract checks (simulation only)";
        System.IO.Directory.CreateDirectory("outputs/rewarded-ads-lab");
        System.IO.File.WriteAllText("outputs/rewarded-ads-lab/checks.txt", result);
        return result;
    }
    public static string Play()
    {
        if (!Application.isPlaying) throw new Exception("Play required.");
        Check(UnityEngine.SceneManagement.SceneManager.sceneCount == 1 && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "RewardedAdsLab", "only isolated scene loaded");
        var screen = UnityEngine.Object.FindFirstObjectByType<RewardedAdsLabScreen>();
        if (screen == null) throw new Exception("Lab screen missing.");
        screen.ResetLab();
        screen.Show(Placement.DailyReward, RewardChoice.UpgradeToken);
        Check(screen.LastRequest.Status == RequestStatus.AwaitingVerification && screen.Gateway.Ledger.Tokens == 0, "play pending");
        screen.Verify(); screen.Verify();
        Check(screen.Gateway.Ledger.Tokens == 2 && screen.Gateway.Used(Placement.DailyReward) == 1, "play duplicate blocked");
        screen.RestartClient();
        Check(screen.Gateway.Ledger.Tokens == 2, "play snapshot preserved");
        return "PASS Play: scene component, request, pending, verify, duplicate, memory restart";
    }
    public static async Task<string> Capture()
    {
        var view = UnityEditor.EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
        view.Show(); view.Focus(); view.Repaint();
        await Task.Delay(500);
        ScreenCapture.CaptureScreenshot("outputs/rewarded-ads-lab/play-ui.png");
        await Task.Delay(1500);
        return System.IO.File.Exists("outputs/rewarded-ads-lab/play-ui.png") ? "Captured GameView including GUI" : "Capture pending";
    }
}
