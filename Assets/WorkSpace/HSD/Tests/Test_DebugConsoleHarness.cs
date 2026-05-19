using UnityEngine;

public class Test_DebugConsoleHarness : MonoBehaviour
{
    [SerializeField] private UI_DebugConsole debugConsole;

    [Button]
    public void Test_OpenConsole()
    {
        if (debugConsole != null)
        {
            debugConsole.Open();
            Debug.Log("[Harness] Debug Console Open() called.");
        }
        else
        {
            Debug.LogError("[Harness] debugConsole is not assigned!");
        }
    }

    [Button]
    public void Test_ToggleModeAndRefresh()
    {
        if (debugConsole != null)
        {
            // For testing, we invoke the private ToggleMode via reflection or just call Open which refreshes.
            // Since ToggleMode is private, let's just test Open and manual RefreshList.
            debugConsole.RefreshList();
            Debug.Log("[Harness] Debug Console RefreshList() called.");
        }
    }
}