using UnityEngine;

[CreateAssetMenu(fileName = "IconItemDataSO", menuName = "Data/Profile/Icon")]
public class IconItemDataSO : ProfileItemDataSO
{
    public override ProfileItemType ProfileItemType => ProfileItemType.Icon;
}
