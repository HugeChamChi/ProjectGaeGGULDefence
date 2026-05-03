using UnityEngine;

[CreateAssetMenu(fileName = "ItemData", menuName = "Data/ItemData", order = 1)]
public class ItemData : ScriptableObject, IItemData
{
    [SerializeField] private int id;
    [SerializeField] private string itemName;
    [SerializeField] private Sprite icon;
    [SerializeField] private Rarity rarity;
    [SerializeField, TextArea] private string description;

    public int Id => id;
    public string Name => itemName;
    public Sprite Icon => icon;
    public Rarity Rarity => rarity;
    public string Description => description;
}
