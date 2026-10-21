using System;
using System.Globalization;
using BackEnd;
using LitJson;

/// <summary>Backend mailbox parsing with explicit reward mapping and fail-closed validation.</summary>
public static class MailParser
{
    /// <summary>Reads optional fields without assuming coupon mail has content or expiry.</summary>
    public static string Text(JsonData json, string key) => json != null && json.IsObject && json.ContainsKey(key) ? json[key]?.ToString() ?? "" : "";
    /// <summary>Parses one mail. Invalid rewards remain visible but unclaimable.</summary>
    public static Post Parse(JsonData json, PostType type, MailRewardConfig config)
    {
        var post = new Post { title = Text(json, "title"), content = Text(json, "content"), inDate = Text(json, "inDate"), Type = type, Author = Text(json, "author") };
        if (string.IsNullOrEmpty(post.inDate)) throw new FormatException("Mail identity missing");
        string expiry = Text(json, "expirationDate");
        if (DateTimeOffset.TryParse(expiry, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)) post.ExpiresAt = date.ToUnixTimeSeconds();
        else if (type != PostType.Coupon) post.ValidationError = "만료 정보를 확인할 수 없습니다.";
        var items = json.IsObject && json.ContainsKey("items") ? json["items"] : null;
        if (!TryRewards(items, post.Key, config, out var reward, out string error)) post.ValidationError = error;
        else
        {
            post.Reward = reward;
            if (reward.Gold > 0) post.postReward["Gold"] = reward.Gold;
            if (reward.Diamond > 0) post.postReward["Dia"] = reward.Diamond;
            if (reward.Stamina > 0) post.postReward["스태미나"] = reward.Stamina;
        }
        post.isCanReceive = string.IsNullOrEmpty(post.ValidationError) && !post.IsExpired;
        return post;
    }
    /// <summary>Converts every item or rejects the entire reward; never infers unknown identifiers.</summary>
    public static bool TryRewards(JsonData items, string id, MailRewardConfig config, out CouponReceipt reward, out string error)
    {
        reward = null; error = "보상 설정 확인이 필요합니다.";
        try
        {
            if (items == null || !items.IsArray) return false;
            var result = new CouponReceipt { Id = id, RedeemedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            foreach (JsonData entry in items)
            {
                if (!entry.IsObject || !entry.ContainsKey("item") || !int.TryParse(Text(entry, "itemCount"), out int amount) || amount <= 0) return false;
                string chart = Text(entry, "chartName");
                MailRewardConfig.Entry match = null;
                foreach (var mapping in config != null ? config.Entries : Array.Empty<MailRewardConfig.Entry>())
                {
                    if (mapping == null || string.IsNullOrEmpty(mapping.ChartName) || string.IsNullOrEmpty(mapping.ItemId) || string.IsNullOrEmpty(mapping.IdColumn)) continue;
                    if (mapping.ChartName != chart || Text(entry["item"], mapping.IdColumn) != mapping.ItemId) continue;
                    if (match != null) return false;
                    match = mapping;
                }
                if (match == null) return false;
                checked
                {
                    switch (match.Currency)
                    {
                        case MailRewardConfig.Currency.Gold: result.Gold += amount; break;
                        case MailRewardConfig.Currency.Diamond: result.Diamond += amount; break;
                        case MailRewardConfig.Currency.Stamina: result.Stamina += amount; break;
                        default: return false;
                    }
                }
            }
            reward = result; error = ""; return true;
        }
        catch (Exception) { return false; }
    }
}
