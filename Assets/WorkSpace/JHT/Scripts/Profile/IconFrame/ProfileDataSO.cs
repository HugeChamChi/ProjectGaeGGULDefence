using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ProfileType
{
    Icon,
    Frame
}

public class ProfileDataSO : ScriptableObject
{
    public int ID;
    public string dataName;
    public Sprite dataSprite;
    public ProfileType profileType;
}
