using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChiefData : ScriptableObject, IMetaData
{
    // MetaData
    [SerializeField] Sprite icon;
    [SerializeField] string chiefName;
    [SerializeField] string description;

    public Sprite Icon => icon;
    public string Name => chiefName;
    public string Description => description;

    // SkillData (추후 추가)
}
