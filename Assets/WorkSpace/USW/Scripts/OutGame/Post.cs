using System.Collections.Generic;
using BackEnd;

public class Post
{
    public PostType Type;
    public string Author;
    public long ExpiresAt;
    public bool IsRead;
    public string ValidationError;
    public CouponReceipt Reward;
    /// <summary>Namespaced receipt identity separate from coupon campaign UUIDs.</summary>
    public string Key => "mail:" + Type + ":" + inDate;
    /// <summary>Display/preflight only; the server enforces expiry.</summary>
    public bool IsExpired => ExpiresAt > 0 && System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= ExpiresAt;
    /// <summary>Player-facing reward summary.</summary>
    public string RewardSummary => postReward.Count == 0 ? (string.IsNullOrEmpty(ValidationError) ? "첨부 보상 없음" : "보상 확인 필요") : string.Join(" · ", System.Linq.Enumerable.Select(postReward, pair => pair.Key + " +" + pair.Value));
    /// <summary>Player-facing expiry text.</summary>
    public string ExpiryLabel => IsExpired ? "기간 만료" : ExpiresAt > 0 ? System.DateTimeOffset.FromUnixTimeSeconds(ExpiresAt).ToLocalTime().ToString("MM.dd HH:mm") + "까지" : Type == PostType.Coupon ? "기간 제한 없음" : "기간 확인 필요";
    public bool   isCanReceive = false;
    public string title;
    public string content;
    public string inDate;
    public Dictionary<string, int> postReward = new Dictionary<string, int>();

    public override string ToString()
    {
        string result = string.Empty;
        result += $"title : {title}\n";
        result += $"content : {content}\n";
        result += $"inDate : {inDate}\n";

        if (isCanReceive)
        {
            result += "우편 아이템\n";
            foreach (string itemKey in postReward.Keys)
                result += $"| {itemKey} : {postReward[itemKey]}개\n";
        }
        else
        {
            result += "수령 가능한 아이템이 없습니다.";
        }
        return result;
    }
}
