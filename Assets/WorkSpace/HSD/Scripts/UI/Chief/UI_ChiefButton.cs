using UnityEngine;
using UnityEngine.UI;

public class UI_ChiefButton : MonoBehaviour
{
    [SerializeField] UI_Base ui_Chief_Artifact_Panel;
    [SerializeField] Image img_Chief;
    [SerializeField] Button btn_Chief;

    private void Awake()
    {
        if (Player.Chief != null)
        {
            Player.Chief.ChangeSelectId += ChiefChange;
            if (Player.Chief.SelectChiefData != null && img_Chief != null)
            {
                img_Chief.sprite = Player.Chief.SelectChiefData.Icon;
            }
        }
        
        if (btn_Chief != null)
        {
            btn_Chief.onClick.AddListener(() => ui_Chief_Artifact_Panel.Open());
        }
    }

    private void ChiefChange(int id)
    {
        img_Chief.sprite = Table.Character.Chief.GetChief(id).Icon;
    }

    private void OnDestroy()
    {
        if (Player.Chief != null)
        {
            Player.Chief.ChangeSelectId -= ChiefChange;
        }
    }
}