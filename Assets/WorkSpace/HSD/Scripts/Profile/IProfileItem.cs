using UnityEngine;

public interface IProfileItem
{
    public ProfileItemType Type { get; }
    public int Id { get; }
    public Sprite Sprite { get; }
    public string Name { get; }
    public string UnlockCondition { get; }
    public bool IsUnlocked { get; }

    public void Unlock();
}