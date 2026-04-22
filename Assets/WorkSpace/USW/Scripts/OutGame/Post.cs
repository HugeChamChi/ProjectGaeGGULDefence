using System.Collections.Generic;
using BackEnd;

public class Post
{
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
