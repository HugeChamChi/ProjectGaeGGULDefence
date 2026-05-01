using UnityEngine;

public interface ICharacterData
{
    public int Id { get; }
    public string Name { get; }
    public string Description { get; }
    public Sprite Icon { get; }
    public Rarity Rarity { get; }
}