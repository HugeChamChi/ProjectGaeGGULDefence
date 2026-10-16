using System;
using LitJson;

/// <summary>Strict coupon reward contract; unsupported item IDs never silently become a currency.</summary>
public static class CouponRewardParser
{
    /// <summary>Parses explicit Gold/Diamond/Dia/Stamina JSON or chart items with those exact currency identifiers.</summary>
    public static bool TryParse(string response, out CouponReceipt receipt, out string error)
    {
        receipt = null; error = "쿠폰 보상 형식을 확인할 수 없습니다. 고객지원에 문의해 주세요.";
        try
        {
            var json = JsonMapper.ToObject(response);
            if (!json.IsObject || !json.ContainsKey("uuid")) return false;
            var result = new CouponReceipt { Id = json["uuid"].ToString(), RedeemedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            if (string.IsNullOrEmpty(result.Id)) return false;
            if (json.ContainsKey("itemObject"))
            {
                var items = json["itemObject"];
                if (!items.IsArray) return false;
                for (int i = 0; i < items.Count; i++)
                {
                    var entry = items[i];
                    var item = entry.ContainsKey("item") ? entry["item"] : entry;
                    if (item.ContainsKey("currency") || item.ContainsKey("itemID"))
                    {
                        string key = item.ContainsKey("currency") ? item["currency"].ToString() : item["itemID"].ToString();
                        if (!entry.ContainsKey("itemCount") || !int.TryParse(entry["itemCount"].ToString(), out int count) || !Add(result, key, count)) return false;
                    }
                    else if (!ReadAmounts(item, result, entry.ContainsKey("itemCount") ? int.Parse(entry["itemCount"].ToString()) : 1)) return false;
                }
            }
            else if (json.ContainsKey("rewards")) { if (!ReadAmounts(json["rewards"], result, 1)) return false; }
            else
            {
                // An explicitly rewardless coupon is valid; only the UUID is required by the SDK.
                foreach (string key in json.Keys)
                    if (key != "uuid" && !Add(result, key, int.Parse(json[key].ToString()))) return false;
            }
            receipt = result; error = ""; return true;
        }
        catch (Exception) { return false; }
    }
    private static bool ReadAmounts(JsonData data, CouponReceipt receipt, int multiplier)
    {
        if (!data.IsObject || multiplier < 0) return false;
        foreach (string key in data.Keys)
        {
            if (key == "chartFileName") continue;
            if (!int.TryParse(data[key].ToString(), out int amount) || !Add(receipt, key, checked(amount * multiplier))) return false;
        }
        return true;
    }
    private static bool Add(CouponReceipt receipt, string key, int amount)
    {
        if (amount < 0) return false;
        switch (key)
        {
            case "Gold": receipt.Gold = checked(receipt.Gold + amount); return true;
            case "Dia": case "Diamond": receipt.Diamond = checked(receipt.Diamond + amount); return true;
            case "Stamina": receipt.Stamina = checked(receipt.Stamina + amount); return true;
            default: return false;
        }
    }
}
