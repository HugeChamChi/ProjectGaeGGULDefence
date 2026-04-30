using UnityEngine;

[CreateAssetMenu(fileName = "FrameItemDataSO", menuName = "Data/Profile/Frame")]
public class FrameItemDataSO : ProfileItemDataSO
{
    public override ProfileItemType ProfileItemType => ProfileItemType.Frame;
}