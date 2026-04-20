using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;

public class ShopItemData
{
    public List<int> daily_Item = new List<int>();
    public List<int> weekly_Item = new List<int>();
    public List<int> monthly_Item = new List<int>();

    public override string ToString()
    {
        StringBuilder result = new StringBuilder();

        result.AppendLine($"daily_Item");
        foreach (var data in daily_Item)
        {
            result.AppendLine($"| {data}");
        }

        result.AppendLine($"weekly_Item");
        foreach (var data in weekly_Item)
        {
            result.AppendLine($"| {data}");

        }

        result.AppendLine($"monthly_Item");
        foreach (var data in monthly_Item)
        {
            result.AppendLine($"| {data}");
        }


        return base.ToString();
    }
}
