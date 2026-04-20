using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ProfileData
{
    [Header("Back Save & Get")]
    public int curIconID;
    public int curFrameID;
    public List<int> unLockIconList;
    public List<int> unLockFrameList;
    

    [Header("Back Get")]
    public int level;
    public string nickName;
}
