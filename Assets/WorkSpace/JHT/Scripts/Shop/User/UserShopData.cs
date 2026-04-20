using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class UserShopData
{
    public Dictionary<int, int> all_Shop = new Dictionary<int, int>();
    public Dictionary<int, int> all_ItemID = new Dictionary<int, int>();

    public List<int> daily_Shop = new List<int>();
    public List<int> weekly_Shop = new List<int>();
    public List<int> monthly_Shop = new List<int>();

    public override string ToString()
    {
        StringBuilder result = new StringBuilder();

        result.AppendLine($"daily_Shop");
        foreach (var data in daily_Shop)
        {
            result.AppendLine($"| {data}");
        }

        result.AppendLine($"weekly_Shop");
        foreach (var data in weekly_Shop)
        {
            result.AppendLine($"| {data}");

        }

        result.AppendLine($"monthly_Shop");
        foreach (var data in monthly_Shop)
        {
            result.AppendLine($"| {data}");
        }

        return base.ToString();
    }

}
