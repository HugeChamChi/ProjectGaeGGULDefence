using UnityEngine;

public interface ICharacterData : IGachaData
{
    public int Id { get; }
    public string Name { get; }
    public string Description { get; }
    public Sprite Icon { get; }
}