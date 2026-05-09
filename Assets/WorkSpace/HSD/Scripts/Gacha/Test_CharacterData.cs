using UnityEngine;

[CreateAssetMenu(fileName = "Test_CharacterData", menuName = "Data/Test_CharacterData")]
public class Test_CharacterData : ScriptableObject, ICharacterData, IGachaData
{
    public int Id { get => id; }
    public string Name { get => charName; }
    public string Description { get => description; }
    public Sprite Icon { get => icon; }
    public Rarity Rarity { get => rarity; }

    [SerializeField] private int id;
    [SerializeField] private string charName;
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private Rarity rarity;
}
