using UnityEngine;

public interface IProfileItem
{
    public ProfileItemType ProfileItemType { get; }
    public Sprite Sprite { get; }
    public string Key { get; }
    public string UnlockDescription { get; }
    public bool IsUnlocked { get; }

    public void Unlock();
}