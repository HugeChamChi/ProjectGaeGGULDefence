using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ChiefData", menuName = "Data/ChiefData")]
public class ChiefData : ScriptableObject, IMetaData
{
    // MetaData
    [SerializeField] int id;
    [SerializeField] Sprite icon;
    [SerializeField] string chiefName;
    [SerializeField] string description;

    public int Id => id;
    public Sprite Icon => icon;
    public string Name => chiefName;
    public string Description => description;

    // SkillData (추후 추가)
}
