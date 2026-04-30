using UnityEngine;

public abstract class ProfileItemDataSO : ScriptableObject, IProfileItem
{
    public abstract ProfileItemType ProfileItemType { get; }
    public int Id => id;
    public Sprite Sprite => sprite;
    public string Key => key;
    public string UnlockDescription => unlockDescription;
    public bool IsUnlocked => isUnlocked;

    [SerializeField] int id;
    [SerializeField] Sprite sprite;
    [SerializeField] string key;
    [SerializeField] string unlockDescription;
    [SerializeField] bool isUnlocked;

    public void Unlock() => isUnlocked = true;
}
