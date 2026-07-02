using UnityEngine;

public static class GlobalData
{
    private static ButtonReactionData _buttonReactionData;
    public static ButtonReactionData ButtonReactionData
    {
        get
        {
            if (_buttonReactionData == null)
            {
                try
                {
                    _buttonReactionData = RM.Load<ButtonReactionData>("GlobalData/ButtonReactionData");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[GlobalData] Failed to lazy load ButtonReactionData: {ex.Message}");
                }
            }
            return _buttonReactionData;
        }
        set => _buttonReactionData = value;
    }

    public static PartyDataSO SelectedParty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        try
        {
            _buttonReactionData = RM.Load<ButtonReactionData>("GlobalData/ButtonReactionData");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[GlobalData] BeforeSceneLoad failed to load ButtonReactionData: {ex.Message}");
        }
    }
}
