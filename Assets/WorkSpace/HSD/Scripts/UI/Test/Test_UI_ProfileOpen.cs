using UnityEngine;

public class Test_UI_ProfileOpen : MonoBehaviour
{
    [SerializeField] private UI_PlayerProfileOpenButton openButton;
    [SerializeField] private UI_PlayerProfilePanel profilePanel;

    [Button]
    public void SimulateButtonClick()
    {
        Debug.Log("Simulating Profile Button Click...");
        if (openButton != null)
        {
            openButton.OnButtonClick();
        }
        else
        {
            Debug.LogError("Open Button is not assigned in Test script.");
        }
    }

    [Button]
    public void DirectOpenPanel()
    {
        Debug.Log("Directly Opening Profile Panel...");
        if (profilePanel != null)
        {
            profilePanel.Open();
        }
        else
        {
            Debug.LogError("Profile Panel is not assigned in Test script.");
        }
    }
}
