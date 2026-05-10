using UnityEngine;
using UnityEngine.UI;

public class UI_ChiefButton : MonoBehaviour
{
    [SerializeField] UI_Base ui_Chief_Artifact_Panel;
    [SerializeField] Image img_Chief;
    [SerializeField] Button btn_Chief;

    private void Awake()
    {
        Player.Chief.ChangeSelectId += ChiefChange;
        btn_Chief.onClick.AddListener(() => ui_Chief_Artifact_Panel.Open());
        img_Chief.sprite = Player.Chief.SelectChiefData.Icon;
    }

    private void ChiefChange(int id)
    {
        img_Chief.sprite = Table.Character.Chief.GetChief(id).Icon;
    }

    private void OnDestroy()
    {
        Player.Chief.ChangeSelectId -= ChiefChange;
    }
}