using UnityEngine;

public abstract class ProfileItemDataSO : ScriptableObject, IProfileItem
{
    public abstract ProfileItemType Type { get; }
    public int Id => id;
    public Sprite Sprite => sprite;
    public string Name => key;
    public string UnlockCondition => unlockDescription;
    public bool IsUnlocked => isUnlocked;

    [SerializeField] int id;
    [SerializeField] Sprite sprite;
    [SerializeField] string key;
    [SerializeField] string unlockDescription;
    [SerializeField] bool isUnlocked;

    public void Unlock() => isUnlocked = true;
}
