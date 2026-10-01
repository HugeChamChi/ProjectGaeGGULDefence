using System;
using UnityEngine;

/// <summary>결과 화면 보상 슬롯 하나 (아이콘 + 수량).</summary>
[Serializable]
public struct ResultReward
{
    public Sprite Icon;
    public int Amount;

    public ResultReward(Sprite icon, int amount)
    {
        Icon = icon;
        Amount = amount;
    }
}
