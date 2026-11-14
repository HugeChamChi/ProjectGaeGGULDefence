using System;
using UnityEngine;

namespace GGD.RewardedAdsLab
{
    public sealed class RewardedAdsLabScreen : MonoBehaviour
    {
        public SimulatedAdGateway Gateway { get; private set; }
        public SimulatedAdAdapter Adapter { get; private set; }
        public RewardedAdService Service { get; private set; }
        public AdRequest LastRequest { get; private set; }
        public string Message { get; private set; } = "Choose a placement. Earned waits for simulated verification.";
        private Vector2 scroll;
        private void Awake() => ResetLab();
        public void ResetLab()
        {
            Gateway = new SimulatedAdGateway(); Adapter = new SimulatedAdAdapter();
            Service = new RewardedAdService(Gateway, Adapter); LastRequest = null;
        }
        public async void Show(Placement placement, RewardChoice choice)
        {
            try
            {
                LastRequest = await Service.ShowAsync(Gateway.Account, placement, choice, Gateway.SettledRun);
                Message = LastRequest.Status.ToString();
            }
            catch (Exception e) { Message = e.Message; }
        }
        public async void Verify()
        {
            if (LastRequest == null) return;
            try
            {
                bool committed = Gateway.Verify(LastRequest.Id, "lab-tx-" + LastRequest.Id);
                LastRequest = await Service.FindAsync(Gateway.Account, LastRequest.Id);
                Message = $"Commit={committed}; {LastRequest.Status}";
            }
            catch (Exception e) { Message = e.Message; }
        }
        public void RestartClient()
        {
            Gateway = new SimulatedAdGateway(Gateway.Account, Gateway.Snapshot()) { UtcNow = Gateway.UtcNow };
            Service = new RewardedAdService(Gateway, Adapter);
            Message = "Simulated restart: ledger restored in memory; no disk/account writes.";
        }
        private void OnGUI()
        {
            // Fixed design canvas scales down on narrow editor views.
            float scale = Mathf.Min(Screen.width / 760f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUILayout.BeginArea(new Rect(20, 20, 720, 860), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("REWARDED ADS LAB - SIMULATOR ONLY", new GUIStyle(GUI.skin.label) { fontSize = 23 });
            GUILayout.Label("No AdMob SDK / login / live rewards / live shop writes");
            var s = Gateway.Ledger;
            GUILayout.Label($"UTC {Gateway.UtcNow:u} | KST reset 05:00, weekly Monday");
            GUILayout.Label($"Gold {s.Gold} | Upgrade tokens {s.Tokens} | Stamina {s.Stamina}");
            GUILayout.Label($"Daily stock revision {s.DailyRevision} | Weekly {s.WeeklyRevision}");
            GUILayout.Label($"Deferred refresh credits: daily {s.DailyRefreshCredits}, weekly {s.WeeklyRefreshCredits}");
            GUILayout.Label("TEST amounts: stamina +10 / daily gold +100 OR tokens +2 / battle +120,+2");
            GUILayout.Space(14);
            foreach (AdOutcome outcome in Enum.GetValues(typeof(AdOutcome)))
                if (GUILayout.Toggle(Adapter.NextOutcome == outcome, outcome.ToString())) Adapter.NextOutcome = outcome;
            GUILayout.Space(14);
            if (GUILayout.Button($"Stamina refill ({Gateway.Used(Placement.StaminaRefill)}/1 daily)", GUILayout.Height(40))) Show(Placement.StaminaRefill, RewardChoice.Gold);
            if (GUILayout.Button("Battle reward x2 (once per settled run)", GUILayout.Height(40))) Show(Placement.BattleRewardDouble, RewardChoice.Gold);
            GUILayout.Label($"Daily shop ad rewards: {Gateway.Used(Placement.DailyReward)}/3 shared");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Choose gold +100", GUILayout.Height(40))) Show(Placement.DailyReward, RewardChoice.Gold);
            if (GUILayout.Button("Choose tokens +2", GUILayout.Height(40))) Show(Placement.DailyReward, RewardChoice.UpgradeToken);
            GUILayout.EndHorizontal();
            if (GUILayout.Button($"Daily stock refresh ({Gateway.Used(Placement.DailyShopRefresh)}/1 daily)", GUILayout.Height(40))) Show(Placement.DailyShopRefresh, RewardChoice.Gold);
            if (GUILayout.Button($"Weekly stock refresh ({Gateway.Used(Placement.WeeklyShopRefresh)}/1 weekly)", GUILayout.Height(40))) Show(Placement.WeeklyShopRefresh, RewardChoice.Gold);
            GUILayout.Space(12);
            GUILayout.Label("TEST CONTROLS");
            if (GUILayout.Button("Simulate verified server commit / retry same receipt")) Verify();
            Gateway.FailNextCommit = GUILayout.Toggle(Gateway.FailNextCommit, "Fail next commit (keep request pending)");
            if (GUILayout.Button("Simulate client restart (memory snapshot)")) RestartClient();
            if (GUILayout.Button("Advance clock by 1 day")) Gateway.UtcNow = Gateway.UtcNow.AddDays(1);
            if (GUILayout.Button("Advance clock by 7 days")) Gateway.UtcNow = Gateway.UtcNow.AddDays(7);
            if (GUILayout.Button("Reset all test data")) ResetLab();
            GUILayout.Label("Last request: " + (LastRequest == null ? "none" : LastRequest.Id));
            GUILayout.Label(Message);
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
    }
}
