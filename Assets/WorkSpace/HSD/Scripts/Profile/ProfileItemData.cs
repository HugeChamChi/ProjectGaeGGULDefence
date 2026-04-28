using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public enum ProfileItemType
{
    Icon,
    Frame
}

public class ProfileTable
{
    Dictionary<string, IProfileItem> profileItems = new();
    Dictionary<string, IProfileItem> frameItems = new();

    public async UniTask InitializeAsync()
    {
        
    }
}

[CreateAssetMenu(fileName = "ProfileItemDataSO", menuName = "Data/Profile/ProfileItemDataSO", order = 0)]
public class ProfileItemDataSO : ScriptableObject, IProfileItem
{
    public ProfileItemType ProfileItemType => profileItemType;
    public Sprite Sprite => sprite;
    public string Key => key;
    public string UnlockDescription => unlockDescription;
    public bool IsUnlocked => isUnlocked;

    [SerializeField] ProfileItemType profileItemType;
    [SerializeField] Sprite sprite;
    [SerializeField] string key;
    [SerializeField] string unlockDescription;
    [SerializeField] bool isUnlocked;

    public void Unlock()
    {
        isUnlocked = true;
    }
}

[Serializable]
public class ProfileItemData : IProfileItem
{
    public ProfileItemType ProfileItemType => profileItemType;
    public Sprite Sprite => sprite;
    public string Key => key;
    public string UnlockDescription => unlockDescription;
    public bool IsUnlocked => isUnlocked;

    [SerializeField] ProfileItemType profileItemType;
    [SerializeField] Sprite sprite;
    [SerializeField] string key;
    [SerializeField] string unlockDescription;
    [SerializeField] bool isUnlocked;

    public ProfileItemData(Sprite sprite, string key, string unlockDescription, bool isUnlocked, ProfileItemType profileItemType)
    {
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

public interface IProfileItem
{
    public ProfileItemType ProfileItemType { get; }
    public Sprite Sprite { get; }
    public string Key { get; }
    public string UnlockDescription { get; }
    public bool IsUnlocked { get; }

    public void Unlock();
}