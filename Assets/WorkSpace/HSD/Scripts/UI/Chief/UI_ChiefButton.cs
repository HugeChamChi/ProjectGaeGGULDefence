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
            ChiefChange(Player.Chief.SelectedChiefId);
        }
        
        if (btn_Chief != null)
        {
            btn_Chief.onClick.AddListener(() => ui_Chief_Artifact_Panel.Open());
        }
    }

    private void ChiefChange(int id)
    {
        if (img_Chief == null) return;
        var chief = Table.Character?.Chief?.GetChief(id);
        img_Chief.sprite = chief != null ? chief.Icon : null;
    }

    private void OnDestroy()
    {
        if (Player.Chief != null)
        {
            Player.Chief.ChangeSelectId -= ChiefChange;
        }
    }
}
