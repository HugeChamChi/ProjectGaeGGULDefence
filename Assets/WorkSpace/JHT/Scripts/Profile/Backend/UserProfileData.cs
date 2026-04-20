using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class UserProfileData
{
    public int level;
    public string nickName = string.Empty;
    public int iconID;
    public int frameID;
    public HashSet<int> unLockIconHash= new HashSet<int>();
    public HashSet<int> unLockFrameHash = new HashSet<int>();

    public override string ToString()
    {
        StringBuilder result = new StringBuilder();
        result.AppendLine($"level : {level}");
        result.AppendLine($"nickName : {nickName}");
        result.AppendLine($"iconID : {iconID}");
        result.AppendLine($"frameID : {frameID}");

        result.AppendLine($"unLockIconHash");
        foreach (var icon in unLockIconHash)
        {
            result.AppendLine($"| {icon}");
        }

        result.AppendLine($"unLockFrameHash");
        foreach (var frame in unLockFrameHash)
        {
            result.AppendLine($"| {frame}");
        }

        return base.ToString();
    }
}
