using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using GGD.BackendCharts;
using LitJson;

/// <summary>Account-wide, once-only NORMAL reached-round rewards from the applied chart.</summary>
public static class RoundReachRewards
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || INTERNAL_TEST_BUILD
    public const string ChartName = "DEV_RoundRewards";
#else
    public const string ChartName = "LIVE_RoundRewards";
#endif
    public sealed class Definition
    {
        public string RewardKey, Currency;
        public int Round, Amount;
    }
    public static Definition[] Parse(string json)
    {
        var rows = JsonMapper.ToObject(json);
        if (!rows.IsArray) throw new FormatException("RoundRewards must be an array");
        var result = new List<Definition>();
        var keys = new HashSet<string>();
        var rounds = new HashSet<int>();
        foreach (JsonData row in rows)
        {
            var d = new Definition {
                RewardKey = row["RewardKey"].ToString(), Currency = row["Currency"].ToString(),
                Round = int.Parse(row["Round"].ToString(), CultureInfo.InvariantCulture),
                Amount = int.Parse(row["Amount"].ToString(), CultureInfo.InvariantCulture)
            };
            if (string.IsNullOrWhiteSpace(d.RewardKey) || !keys.Add(d.RewardKey) ||
                d.Round < 1 || !rounds.Add(d.Round) || d.Amount < 1 ||
                (d.Currency != "Gold" && d.Currency != "Diamond"))
                throw new FormatException("Invalid round reward: " + d.RewardKey);
            result.Add(d);
        }
        result.Sort((a,b) => a.Round.CompareTo(b.Round));
        return result.ToArray();
    }
    public static async UniTask<Definition[]> LoadAsync(CancellationToken token)
        => Parse((await BackendChartReader.ReadAsync(ChartName, token)).ToJson());

    public static bool Apply(PlayerData data, Definition reward, int? bestRound)
    {
        if (data == null || reward == null || !bestRound.HasValue || bestRound.Value < reward.Round ||
            reward.Round < 1 || reward.Amount < 1 || string.IsNullOrWhiteSpace(reward.RewardKey) ||
            (reward.Currency != "Gold" && reward.Currency != "Diamond")) return false;
        data.RoundRewardClaims ??= new List<string>();
        if (data.RoundRewardClaims.Contains(reward.RewardKey)) return false;
        long balance = (reward.Currency == "Gold" ? data.Gold : data.Diamond) + (long)reward.Amount;
        if (balance > int.MaxValue) return false;
        if (reward.Currency == "Gold") data.Gold = (int)balance;
        else data.Diamond = (int)balance;
        data.RoundRewardClaims.Add(reward.RewardKey);
        return true;
    }
}
