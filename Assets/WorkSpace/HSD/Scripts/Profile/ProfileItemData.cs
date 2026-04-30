using System;
using UnityEngine;

public enum ProfileItemType
{
    Icon,
    Frame
}

[Serializable]
public class ProfileItemData : IProfileItem
{
    public ProfileItemType Type => profileItemType;
    public int Id => id;
    public Sprite Sprite => sprite;
    public string Name => key;
    public string UnlockCondition => unlockDescription;
    public bool IsUnlocked => isUnlocked;

    [SerializeField] ProfileItemType profileItemType;
    [SerializeField] int id;
    [SerializeField] Sprite sprite;
    [SerializeField] string key;
    [SerializeField] string unlockDescription;
    [SerializeField] bool isUnlocked;

    public ProfileItemData(int id, Sprite sprite, string key, string unlockDescription, bool isUnlocked, ProfileItemType profileItemType)
    {
        this.id = id;
        this.sprite = sprite;
        this.key = key;
        this.unlockDescription = unlockDescription;
        this.isUnlocked = isUnlocked;
        this.profileItemType = profileItemType;
    }

    public void Unlock()
    {
        isUnlocked = true;
    }
}
