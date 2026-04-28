using UnityEngine;

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
