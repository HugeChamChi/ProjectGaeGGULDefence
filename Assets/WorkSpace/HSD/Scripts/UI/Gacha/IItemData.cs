using UnityEngine;

public interface IItemData
{
    string Name { get; }
    Sprite Icon { get; }
    Rarity Rarity { get; }
    public int Id { get; }
    public string Description { get; }
}
