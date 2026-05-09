using UnityEngine;

public interface IItemData : IMetaData
{
    Rarity Rarity { get; }
    public int Id { get; }
}
