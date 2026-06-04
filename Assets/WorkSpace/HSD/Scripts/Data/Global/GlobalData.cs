using UnityEngine;

public static class GlobalData
{
    public static ButtonReactionData ButtonReactionData;
    public static PartyDataSO SelectedParty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        ButtonReactionData = RM.Load<ButtonReactionData>("GlobalData/ButtonReactionData");
    }
}
