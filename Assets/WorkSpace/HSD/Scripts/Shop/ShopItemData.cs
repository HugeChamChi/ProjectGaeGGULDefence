using UnityEngine;

public enum CurrencyType
{
    Diamond,
    Gold,
    Cash,
    Free
}

public enum ItemType
{
    Item,
    Currency,
    Character
}

[System.Serializable]
public class ShopItemData
{
    public int ShopID;
    public ItemType Type;
    public int ItemID;
    public int Amount;

    public CurrencyType CurrencyType;
    public int Price;

    public ShopItemData() { }

    public ShopItemData(int shopID, ItemType type, int itemID, int amount, CurrencyType currencyType, int price)
    {
        ShopID = shopID;
        Type = type;
        ItemID = itemID;
        Amount = amount;
        CurrencyType = currencyType;
        Price = price;
    }

    public Sprite GetIcon()
    {
        return Type switch
        {
            ItemType.Item => Table.Item.Get(ItemID).Icon,
            ItemType.Currency => Table.Item.Get(ItemID).Icon,
            ItemType.Character => Table.Character.GetCharacterData(ItemID).Icon,
            _ => null
        };
    }

    public Sprite GetCurrencyIcon()
    {
        if (CurrencyType == CurrencyType.Free)
            return null;

        return Table.Item.Get((int)CurrencyType).Icon;
    }

    public IReward GetReward()
    {
        return Type switch
        {
            ItemType.Item => new ItemReward(ItemID, Amount),
            ItemType.Character => new CharacterReward(ItemID, Amount),
            ItemType.Currency => new CurrencyReward((CurrencyType)ItemID, Amount), // Assuming ItemID represents CurrencyType for Currency reward
            _ => null
        };
    }
}

public interface IReward
{
    void GetReward();
}

public struct ItemReward : IReward
{
    public int ItemID;
    public int Amount;

    public ItemReward(int itemID, int amount)
    {
        ItemID = itemID;
        Amount = amount;
    }

    public void GetReward()
    {
        // TODO: Implement actual item addition logic
        Debug.Log($"Claimed item reward: {Amount} of item {ItemID}");
    }
}

public struct CharacterReward : IReward
{
    public int CharacterID;
    public int Amount;

    public CharacterReward(int characterID, int amount)
    {
        CharacterID = characterID;
        Amount = amount;
    }

    public void GetReward()
    {
        Player.Character.AddCharacter(CharacterID, Amount);
        Debug.Log($"Claimed character reward: Character ID {CharacterID}, Amount {Amount}");
    }
}

public struct CurrencyReward : IReward
{
    public CurrencyType CurrencyType;
    public int Amount;

    public CurrencyReward(CurrencyType currencyType, int amount)
    {
        CurrencyType = currencyType;
        Amount = amount;
    }

    public void GetReward()
    {
        switch (CurrencyType)
        {
            case CurrencyType.Diamond:
                Player.PlayerData.AddDiamond(Amount);
                break;
            case CurrencyType.Gold:
                Player.PlayerData.AddGold(Amount);
                break;
            case CurrencyType.Cash:
                // Handle cash if needed
                break;
        }
        Debug.Log($"Claimed {Amount} {CurrencyType}");
    }
}
